using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Daemon;
using Contr.Sdk.Common.V1;
using Contr.Sdk.Operations.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Grpc.Core;
using ComposeV1 = Contr.Sdk.Compose.V1;
using EngineV1 = Contr.Sdk.Engine.V1;
using Event = Contr.Sdk.Operations.V1.FollowOperationResponse.EventOneofCase;
using Kind = Contr.Sdk.Operations.V1.OperationRequest.KindOneofCase;
using OperationState = Contr.UI.Contracts.Services.Daemon.OperationState;
using Remediation = Contr.UI.Contracts.Services.Daemon.Remediation;
using WireOperation = Contr.Sdk.Operations.V1.Operation;
using WireState = Contr.Sdk.Operations.V1.OperationState;

namespace Contr.Daemon.UI;

/// <summary>Starts and follows the daemon's operations (API revision 22), for the clients and module gateways.
/// </summary>
public static class DaemonOperations
{
    /// <summary>Whether the connected daemon runs long work as operations.</summary>
    public static bool Offers(this IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);

        return daemon.Identity?.ApiRevision >= IActivity.OperationsRevision;
    }

    /// <summary>Starts an operation, or finds the running one doing the same work.</summary>
    public static async Task<Result<WireOperation>> StartOperationAsync(
        this IDaemonClient daemon, OperationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var started = new StartOperationRequest { Request = request };
        var result = await daemon
            .CallAsync(
                async (client, token) => await client.Operations
                    .StartOperationAsync(started, cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Result.Success(result.Value.Operation ?? new WireOperation())
            : Result.Failure<WireOperation>(result.Error);
    }

    /// <summary>Reads an operation's events, the kept ones first, until it ends with <c>finished</c>.</summary>
    /// <param name="map">Returns <see langword="null"/> for an event the caller skips.</param>
    /// <param name="cancellationToken">Stops reading; the operation goes on.</param>
    public static IAsyncEnumerable<TItem> FollowOperationAsync<TItem>(
        this IDaemonClient daemon,
        string id,
        Func<FollowOperationResponse, TItem?> map,
        Func<Error, TItem> fail,
        CancellationToken cancellationToken)
        where TItem : class
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var request = new FollowOperationRequest { Id = id };

        return daemon.StreamAsync(
            (client, token) => client.Operations.FollowOperation(request, cancellationToken: token),
            map,
            fail,
            cancellationToken);
    }

    /// <summary>Starts an operation, or finds the running one doing the same work, and follows it to its end.
    /// </summary>
    /// <param name="progress">Receives each step.</param>
    /// <param name="cancellationToken">Stops following; the operation goes on.</param>
    /// <returns>Success once the operation succeeded; otherwise why it failed or ended.</returns>
    public static async Task<Result> RunOperationAsync(
        this IDaemonClient daemon,
        OperationRequest request,
        IProgress<EngineProgress>? progress,
        CancellationToken cancellationToken)
    {
        var started = await daemon.StartOperationAsync(request, cancellationToken).ConfigureAwait(false);

        if (started.IsFailure)
        {
            return Result.Failure(started.Error);
        }

        var ended = await daemon
            .FollowToEndAsync(started.Value.Id, progress, destination: null, cancellationToken)
            .ConfigureAwait(false);

        if (ended.IsFailure)
        {
            return Result.Failure(ended.Error);
        }

        return ended.Value.State == OperationState.Succeeded
            ? Result.Success()
            : Result.Failure(Ended(ended.Value));
    }

    /// <summary>Follows an operation to its end, reporting its steps.</summary>
    /// <param name="destination">Receives a bundle's chunks; absent skips them.</param>
    /// <param name="cancellationToken">Stops following; the operation goes on.</param>
    /// <returns>The operation in its final state; a failure when the stream broke or the daemon does not keep it.
    /// </returns>
    public static async Task<Result<DaemonOperation>> FollowToEndAsync(
        this IDaemonClient daemon,
        string id,
        IProgress<EngineProgress>? progress,
        Stream? destination,
        CancellationToken cancellationToken)
    {
        var events = daemon.FollowOperationAsync(
            id,
            response => response.EventCase switch {
                Event.Finished => new Followed(null, ToOperation(response.Finished), null),
                Event.CreateBundle when response.CreateBundle.Chunk.Length > 0 =>
                    new Followed(null, null, null) { Chunk = response.CreateBundle.Chunk.Memory },
                _ => Step(response) is { } step ? new Followed(step, null, null) : null,
            },
            error => new Followed(null, null, error),
            cancellationToken);

        await foreach (var item in events.ConfigureAwait(false))
        {
            if (item.Error is { } error)
            {
                return Result.Failure<DaemonOperation>(error);
            }

            if (item.Operation is { } operation)
            {
                return Result.Success(operation);
            }

            if (item.Step is { } step)
            {
                progress?.Report(step);
            }

            if (destination is not null && !item.Chunk.IsEmpty)
            {
                await destination.WriteAsync(item.Chunk, cancellationToken).ConfigureAwait(false);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Result.Failure<DaemonOperation>(
            new Error(DaemonErrors.FailedCode, "The daemon ended the operation's stream early."));
    }

    /// <summary>A step in words, for an event that carries one.</summary>
    public static EngineProgress? Step(FollowOperationResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.EventCase switch {
            Event.EngineProgress => ToProgress(response.EngineProgress),
            Event.RunRemediation when response.RunRemediation.Progress is { } step =>
                ToProgress(step),
            Event.CreateBundle when response.CreateBundle.Progress is { } step => ToProgress(step),
            Event.ImportImages => new EngineProgress(
                response.ImportImages.Error.Length == 0
                    ? $"Imported {response.ImportImages.Image}"
                    : $"{response.ImportImages.Image}: {response.ImportImages.Error}",
                null),
            Event.PullImage when response.PullImage.Status.Length > 0 =>
                new EngineProgress(response.PullImage.Status, null),
            Event.PushImage when response.PushImage.Status.Length > 0 =>
                new EngineProgress(response.PushImage.Status, null),
            Event.ComposeAction when response.ComposeAction.Output.Length > 0 =>
                new EngineProgress(response.ComposeAction.Output, null),
            _ => null,
        };
    }

    public static Task<Result> CancelOperationAsync(
        this IDaemonClient daemon, string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var request = new CancelOperationRequest { Id = id };

        return daemon.RunAsync(
            (client, token) => client.Operations
                .CancelOperationAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public static DaemonOperation ToOperation(WireOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return new DaemonOperation(
            operation.Id,
            ToWork(operation.Request ?? new OperationRequest()),
            ToState(operation.State),
            operation.StartedAt?.ToDateTimeOffset() ?? DateTimeOffset.UnixEpoch) {
            Progress = operation.Progress is { } progress ? ToProgress(progress) : null,
            FinishedAt = operation.FinishedAt?.ToDateTimeOffset(),
            IsCancellable = operation.Cancellable,
            Failure = operation.Error is { } error
                ? new OperationFailure(
                    ToError(error),
                    [.. error.Remediations.Select(fix => new Remediation(fix.ActionId, fix.RequiresElevation))])
                : null,
        };
    }

    public static EngineProgress ToProgress(Progress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new EngineProgress(progress.Message, progress.HasFraction ? progress.Fraction : null);
    }

    /// <summary>The error the RPC doing the same work would have failed with.</summary>
    public static Error ToError(OperationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Reason == ErrorReason.EngineNotRunning
            ? DaemonErrors.EngineNotRunning
            : new RpcException(new Status((StatusCode)error.Code, error.Message)).ToError() with {
                Message = error.Message,
            };
    }

    /// <summary>The failure of an operation that did not succeed: its error, or why it ended otherwise.</summary>
    public static Error Ended(DaemonOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation.State switch {
            OperationState.Failed when operation.Failure is { } failure => failure.Error,
            OperationState.Cancelled => new Error(DaemonErrors.FailedCode, "It was cancelled."),
            _ => new Error(DaemonErrors.FailedCode, "The daemon didn't say how it ended."),
        };
    }

    /// <summary>One event of a followed operation: a step, its final state, or why the stream broke.</summary>
    private sealed record Followed(EngineProgress? Step, DaemonOperation? Operation, Error? Error)
    {
        public ReadOnlyMemory<byte> Chunk { get; init; }
    }

    private static OperationState ToState(WireState state)
    {
        return state switch {
            WireState.Succeeded => OperationState.Succeeded,
            WireState.Failed => OperationState.Failed,
            WireState.Cancelled => OperationState.Cancelled,
            _ => OperationState.Running,
        };
    }

    private static OperationWork ToWork(OperationRequest request)
    {
        return request.KindCase switch {
            Kind.PullImage => new OperationWork.PullImage(request.PullImage.Reference, request.PullImage.Platform),
            Kind.PushImage => new OperationWork.PushImage(request.PushImage.Reference),
            Kind.ComposeAction => new OperationWork.ComposeAction(
                request.ComposeAction.Project,
                ToVerb(request.ComposeAction.Action),
                [.. request.ComposeAction.Services],
                request.ComposeAction.RemoveVolumes),
            Kind.ImportImages => new OperationWork.ImportImages([.. request.ImportImages.Images]),
            Kind.CreateBundle => new OperationWork.CreateBundle(),
            Kind.RunRemediation => new OperationWork.RunRemediation(request.RunRemediation.ActionId),
            Kind.StartEngine => new OperationWork.StartEngine(),
            Kind.StopEngine => new OperationWork.StopEngine(),
            Kind.RestartEngine => new OperationWork.RestartEngine(),
            Kind.ResetEngine => new OperationWork.ResetEngine(
                request.ResetEngine.Scope == EngineV1.ResetScope.Factory
                    ? EngineResetScope.Factory
                    : EngineResetScope.Runtime),
            _ => new OperationWork.Unknown(),
        };
    }

    private static string ToVerb(ComposeV1.Action action)
    {
        return action switch {
            ComposeV1.Action.Up => "up",
            ComposeV1.Action.Pull => "pull",
            ComposeV1.Action.Start => "start",
            ComposeV1.Action.Stop => "stop",
            ComposeV1.Action.Restart => "restart",
            ComposeV1.Action.Down => "down",
            _ => string.Empty,
        };
    }
}
