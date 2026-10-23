using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Contr.SharedKernel;

namespace Contr.UI.Contracts.Services.Daemon;

/// <summary>
/// Long-running work the daemon runs as operations, from API revision 22: it goes on when a page closes, the window
/// hides or the app quits, and every client sees it.
/// </summary>
public interface IActivity : IUiContractService
{
    /// <summary>The first API revision whose daemon runs work as operations.</summary>
    const uint OperationsRevision = 22;

    /// <summary>
    /// Every operation the daemon keeps, newest first, on subscription and after every change; empty while no daemon
    /// that runs operations is connected.
    /// </summary>
    /// <remarks>Emits on a background thread; subscribers share one stream from the daemon.</remarks>
    IObservable<IReadOnlyList<DaemonOperation>> Changes { get; }

    /// <summary>The list <see cref="Changes"/> emitted last.</summary>
    IReadOnlyList<DaemonOperation> Current { get; }

    /// <summary>Each operation that finished while the app watched, in its final state.</summary>
    /// <remarks>Emits on a background thread.</remarks>
    IObservable<DaemonOperation> Finished { get; }

    /// <summary>Follows an operation to its end, reporting its steps.</summary>
    /// <param name="id">A <see cref="DaemonOperation.Id"/>.</param>
    /// <param name="progress">Receives each step, the ones the daemon kept first.</param>
    /// <param name="cancellationToken">Stops following; the operation goes on.</param>
    /// <returns>The operation in its final state; a failure with code <c>Daemon.NotFound</c> where the daemon does not
    /// keep it.</returns>
    Task<Result<DaemonOperation>> FollowAsync(
        string id, IProgress<EngineProgress> progress, CancellationToken cancellationToken);

    /// <summary>Cancels a running operation; a finished one stays as it ended.</summary>
    /// <returns>A failure with code <c>Daemon.Conflict</c> for an operation that cannot be cancelled.</returns>
    Task<Result> CancelAsync(string id, CancellationToken cancellationToken);

    /// <summary>Writes the zip of a <see cref="OperationWork.CreateBundle"/> operation, waiting for it to finish.
    /// </summary>
    /// <param name="id">The bundle's <see cref="DaemonOperation.Id"/>.</param>
    /// <param name="destination">Receives the zip, which is whole only when the call succeeds.</param>
    /// <param name="progress">Receives each step of the bundle.</param>
    /// <param name="cancellationToken">Stops writing; the bundle goes on.</param>
    Task<Result> SaveBundleAsync(
        string id, Stream destination, IProgress<EngineProgress> progress, CancellationToken cancellationToken);
}

/// <summary>Work the daemon runs or ran.</summary>
/// <param name="Id">Unique among the operations of every daemon process.</param>
/// <param name="Work">What the operation does.</param>
/// <param name="State">Whether it runs, and how it ended.</param>
/// <param name="StartedAt">When it started.</param>
public sealed record DaemonOperation(string Id, OperationWork Work, OperationState State, DateTimeOffset StartedAt)
{
    /// <summary>The latest step; absent before the first.</summary>
    public EngineProgress? Progress { get; init; }

    /// <summary>When it ended; absent while it runs.</summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>Whether <see cref="IActivity.CancelAsync"/> can stop it while it runs.</summary>
    public bool IsCancellable { get; init; }

    /// <summary>Why it failed; set only in <see cref="OperationState.Failed"/>.</summary>
    public OperationFailure? Failure { get; init; }

    /// <summary>Whether it has not ended yet.</summary>
    public bool IsRunning => State == OperationState.Running;
}

/// <summary>Whether an operation runs, and how it ended.</summary>
public enum OperationState
{
    /// <summary>Not ended yet.</summary>
    Running,

    /// <summary>Did what it was asked.</summary>
    Succeeded,

    /// <summary>Ended early; <see cref="DaemonOperation.Failure"/> says why.</summary>
    Failed,

    /// <summary>Stopped by <see cref="IActivity.CancelAsync"/>, from this client or another.</summary>
    Cancelled,
}

/// <summary>Why an operation failed.</summary>
/// <param name="Error">The daemon's message, with the code the RPC doing the same work would fail with.</param>
/// <param name="Remediations">The fixes the daemon runs for the failure.</param>
public sealed record OperationFailure(Error Error, IReadOnlyList<Remediation> Remediations);

/// <summary>What an operation does, as the request that started it said.</summary>
public abstract record OperationWork
{
    private OperationWork()
    {
    }

    /// <summary>Pulls an image.</summary>
    /// <param name="Reference">The image, such as <c>postgres:17</c>.</param>
    /// <param name="Platform">Such as <c>linux/arm64</c>; empty for the engine's own platform.</param>
    public sealed record PullImage(string Reference, string Platform) : OperationWork;

    /// <summary>Pushes an image.</summary>
    /// <param name="Reference">The image, such as <c>registry.example.com/shop/api:1.2</c>.</param>
    public sealed record PushImage(string Reference) : OperationWork;

    /// <summary>Runs Compose on a project.</summary>
    /// <param name="Project">The project's name.</param>
    /// <param name="Action">Compose's command in lower case, such as <c>up</c> or <c>down</c>.</param>
    /// <param name="Services">The services it acts on; empty for every one.</param>
    /// <param name="RemoveVolumes">A <c>down</c> that also deletes the project's named volumes.</param>
    public sealed record ComposeAction(
        string Project, string Action, IReadOnlyList<string> Services, bool RemoveVolumes) : OperationWork;

    /// <summary>Copies engine images into the Kubernetes cluster.</summary>
    /// <param name="Images">References on the engine, such as <c>shop-api:dev</c>.</param>
    public sealed record ImportImages(IReadOnlyList<string> Images) : OperationWork;

    /// <summary>Writes the diagnostics bundle, which the daemon keeps until it drops the operation.</summary>
    public sealed record CreateBundle : OperationWork;

    /// <summary>Runs a fix a check offered.</summary>
    /// <param name="ActionId">A <see cref="Remediation.ActionId"/>.</param>
    public sealed record RunRemediation(string ActionId) : OperationWork;

    /// <summary>Sets the engine up when needed and starts it.</summary>
    public sealed record StartEngine : OperationWork;

    /// <summary>Stops the engine and its containers.</summary>
    public sealed record StopEngine : OperationWork;

    /// <summary>Stops the engine, then starts it.</summary>
    public sealed record RestartEngine : OperationWork;

    /// <summary>Stops the engine and restores it to a clean state.</summary>
    /// <param name="Scope">What the reset keeps.</param>
    public sealed record ResetEngine(EngineResetScope Scope) : OperationWork;

    /// <summary>Work this client does not know.</summary>
    public sealed record Unknown : OperationWork;
}
