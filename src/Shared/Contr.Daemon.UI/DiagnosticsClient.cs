using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Daemon;
using Contr.Sdk.Diagnostics.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Grpc.Core;
using Outcome = Contr.UI.Contracts.Services.Daemon.RemediationOutcome;
using Remediation = Contr.UI.Contracts.Services.Daemon.Remediation;
using WireCheck = Contr.Sdk.Diagnostics.V1.Check;
using WireOutcome = Contr.Sdk.Diagnostics.V1.RemediationOutcome;
using WireProgress = Contr.Sdk.Common.V1.Progress;
using WireStatus = Contr.Sdk.Diagnostics.V1.CheckStatus;

namespace Contr.Daemon.UI;

/// <summary><see cref="IDiagnostics"/> over DiagnosticsService.</summary>
public sealed class DiagnosticsClient : IDiagnostics
{
    /// <summary>A daemon older than API revision 17 reports a declined administrator prompt by this reason.
    /// </summary>
    private const string _declined = "the administrator prompt was declined";

    private static readonly Error _declinedError =
        new(IDiagnostics.ElevationDeclinedCode, "The administrator prompt was declined.");

    private readonly IDaemonClient _daemon;

    public DiagnosticsClient(IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public async Task<Result<IReadOnlyList<DiagnosticCheck>>> RunChecksAsync(CancellationToken cancellationToken)
    {
        var result = await _daemon
            .CallAsync(
                async (client, token) => await client.Diagnostics
                    .RunChecksAsync(new RunChecksRequest(), cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<IReadOnlyList<DiagnosticCheck>>(result.Error)
            : Result.Success<IReadOnlyList<DiagnosticCheck>>([.. result.Value.Checks.Select(ToCheck)]);
    }

    public async Task<Result<Outcome>> RunRemediationAsync(
        string actionId, IProgress<EngineProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(progress);

        var request = new RunRemediationRequest { ActionId = actionId };
        var dismissed = false;
        var result = await _daemon
            .CallAsync(
                async (client, token) => {
                    using var call = client.Diagnostics.RunRemediation(request, cancellationToken: token);
                    Outcome? outcome = null;
                    try
                    {
                        await foreach (var response in call.ResponseStream.ReadAllAsync(token).ConfigureAwait(false))
                        {
                            if (response.EventCase == RunRemediationResponse.EventOneofCase.Progress)
                            {
                                progress.Report(Step(response.Progress));
                            }
                            else if (response.EventCase == RunRemediationResponse.EventOneofCase.Outcome)
                            {
                                outcome = ToOutcome(response.Outcome);
                            }
                        }
                    }
                    catch (RpcException exception) when (Dismisses(exception, token))
                    {
                        // From API revision 17 the daemon fails a fix whose prompt the user dismissed as CANCELLED.
                        dismissed = true;
                    }

                    return outcome;
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (dismissed)
        {
            return Result.Failure<Outcome>(_declinedError);
        }

        if (result.IsFailure)
        {
            return Result.Failure<Outcome>(Reason(result.Error, actionId));
        }

        return result.Value is { } finished
            ? Result.Success(finished)
            : Result.Failure<Outcome>(new Error(DaemonErrors.FailedCode, "The daemon didn't say how the fix ended."));
    }

    public static DiagnosticCheck ToCheck(WireCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        return new DiagnosticCheck(
            check.Id,
            check.Title,
            check.Status switch {
                WireStatus.Pass => DiagnosticStatus.Pass,
                WireStatus.Warn => DiagnosticStatus.Warn,
                WireStatus.Fail => DiagnosticStatus.Fail,
                WireStatus.Skip => DiagnosticStatus.Skip,
                _ => DiagnosticStatus.Unknown,
            },
            check.Detail,
            [.. check.Remediations.Select(fix => new Remediation(fix.ActionId, fix.RequiresElevation))]);
    }

    /// <summary>Why a fix failed, in words for the user.</summary>
    /// <remarks>The daemon fails a fix with a reason such as <c>install-wsl failed: wsl --install exited with 1</c>.
    /// </remarks>
    public static Error Reason(Error error, string actionId)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error.Code is DaemonErrors.NotFoundCode or DaemonErrors.FeatureUnavailableCode)
        {
            return error with { Message = "Contr's background service doesn't offer this fix." };
        }

        var prefix = $"{actionId} failed: ";
        var reason = error.Message.StartsWith(prefix, StringComparison.Ordinal)
            ? error.Message[prefix.Length..]
            : error.Message;

        if (reason == _declined)
        {
            return _declinedError;
        }

        return reason.Length > 0 ? error with { Message = reason } : error;
    }

    public async Task<Result> CreateBundleAsync(
        Stream destination, IProgress<EngineProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(progress);

        var request = new CreateBundleRequest();
        var result = await _daemon
            .CallAsync(
                async (client, token) => {
                    using var call = client.Diagnostics.CreateBundle(request, cancellationToken: token);
                    await foreach (var response in call.ResponseStream.ReadAllAsync(token).ConfigureAwait(false))
                    {
                        if (response.EventCase == CreateBundleResponse.EventOneofCase.Progress)
                        {
                            progress.Report(Step(response.Progress));
                        }
                        else if (response.EventCase == CreateBundleResponse.EventOneofCase.Chunk)
                        {
                            await destination.WriteAsync(response.Chunk.Memory, token).ConfigureAwait(false);
                        }
                    }

                    await destination.FlushAsync(token).ConfigureAwait(false);

                    return true;
                },
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    /// <summary>Whether the daemon refused the call because the user dismissed its prompt, rather than the caller
    /// cancelling it.</summary>
    public static bool Dismisses(RpcException exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.StatusCode == StatusCode.Cancelled && !cancellationToken.IsCancellationRequested;
    }

    private static EngineProgress Step(WireProgress progress)
    {
        return new EngineProgress(progress.Message, progress.HasFraction ? progress.Fraction : null);
    }

    private static Outcome ToOutcome(WireOutcome outcome)
    {
        return new Outcome(outcome.RestartRequired, outcome.LogoutRequired);
    }
}
