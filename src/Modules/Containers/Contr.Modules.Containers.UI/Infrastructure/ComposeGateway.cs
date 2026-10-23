using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Daemon.UI;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Application.Compose;
using Contr.Sdk.Operations.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using ComposeV1 = Contr.Sdk.Compose.V1;
using OperationState = Contr.UI.Contracts.Services.Daemon.OperationState;

namespace Contr.Modules.Containers.UI.Infrastructure;

/// <summary>The <see cref="IComposeGateway"/> over the daemon's ComposeService.</summary>
public sealed class ComposeGateway : IComposeGateway
{
    private readonly IDaemonClient _daemon;

    public ComposeGateway(IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public IAsyncEnumerable<ListChange<ComposeProjectDto>> WatchProjectsAsync(CancellationToken cancellationToken)
    {
        return _daemon.StreamAsync(
            (client, token) => client.Compose.WatchProjects(
                new ComposeV1.WatchProjectsRequest(), cancellationToken: token),
            ComposeMapping.ToChange,
            error => new ListChange<ComposeProjectDto>.Failed(error),
            cancellationToken);
    }

    public async Task<Result<ComposeProjectDto>> AddProjectAsync(
        IReadOnlyList<string> configFiles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configFiles);

        var request = new ComposeV1.AddProjectRequest { ConfigFiles = { configFiles } };
        var result = await _daemon
            .CallAsync(
                (client, token) => client.Compose
                    .AddProjectAsync(request, cancellationToken: token)
                    .ResponseAsync,
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Result.Success(ComposeMapping.ToDto(result.Value.Project))
            : Result.Failure<ComposeProjectDto>(result.Error);
    }

    public Task<Result> RemoveProjectAsync(string project, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(project);

        var request = new ComposeV1.RemoveProjectRequest { Name = project };

        return _daemon.RunAsync(
            (client, token) => client.Compose
                .RemoveProjectAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public IAsyncEnumerable<ComposeActionEvent> RunActionAsync(
        string project,
        ComposeAction action,
        IReadOnlyList<string> services,
        bool removeVolumes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(project);
        ArgumentNullException.ThrowIfNull(services);

        var request = new ComposeV1.RunActionRequest {
            Project = project,
            Action = ComposeMapping.ToWire(action),
            Services = { services },
            RemoveVolumes = removeVolumes,
        };

        return _daemon.Offers()
            ? RunActionOperationAsync(request, cancellationToken)
            : _daemon.StreamAsync(
                (client, token) => client.Compose.RunAction(request, cancellationToken: token),
                ComposeMapping.ToEvent,
                error => new ComposeActionEvent.Failed(error),
                cancellationToken);
    }

    public async IAsyncEnumerable<ComposeActionEvent> FollowActionAsync(
        string operationId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(operationId);

        var exited = false;
        var events = _daemon.FollowOperationAsync(
            operationId,
            ComposeMapping.ToFollowed,
            error => new ComposeMapping.Followed(new ComposeActionEvent.Failed(error), null),
            cancellationToken);

        await foreach (var followed in events.ConfigureAwait(false))
        {
            if (followed.Event is { } actionEvent)
            {
                exited |= actionEvent is ComposeActionEvent.Exited;

                yield return actionEvent;
            }

            if (followed.Finished is { } finished && Ending(finished, exited) is { } last)
            {
                yield return last;
            }
        }
    }

    /// <summary>The last event of a Compose action that ended; absent when Compose's exit came already.</summary>
    public static ComposeActionEvent? Ending(DaemonOperation finished, bool exited)
    {
        ArgumentNullException.ThrowIfNull(finished);

        return finished.State switch {
            _ when exited => null,
            OperationState.Succeeded => new ComposeActionEvent.Exited(0),
            OperationState.Cancelled => new ComposeActionEvent.Cancelled(),
            _ => new ComposeActionEvent.Failed(DaemonOperations.Ended(finished)),
        };
    }

    private async IAsyncEnumerable<ComposeActionEvent> RunActionOperationAsync(
        ComposeV1.RunActionRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var started = await _daemon
            .StartOperationAsync(new OperationRequest { ComposeAction = request }, cancellationToken)
            .ConfigureAwait(false);

        if (started.IsFailure)
        {
            yield return new ComposeActionEvent.Failed(started.Error);
            yield break;
        }

        yield return new ComposeActionEvent.Started(started.Value.Id);

        var events = FollowActionAsync(started.Value.Id, cancellationToken);
        await foreach (var actionEvent in events.ConfigureAwait(false))
        {
            yield return actionEvent;
        }
    }

    public IAsyncEnumerable<ComposeLogEvent> StreamLogsAsync(
        string project, int tailLines, bool follow, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(project);

        var request = new ComposeV1.StreamLogsRequest {
            Project = project,
            Follow = follow,
            TailLines = (uint)Math.Max(0, tailLines),
        };
        var decoder = new ComposeLogDecoder();

        return _daemon.StreamAsync(
            (client, token) => client.Compose.StreamLogs(request, cancellationToken: token),
            decoder.Decode,
            error => new ComposeLogEvent.Failed(error),
            cancellationToken);
    }
}
