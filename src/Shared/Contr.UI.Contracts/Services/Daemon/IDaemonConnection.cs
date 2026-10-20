using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.SharedKernel;

namespace Contr.UI.Contracts.Services.Daemon;

/// <summary>The application's one connection to the Contr daemon, without SDK types.</summary>
public interface IDaemonConnection : IUiContractService, ISingletonDependency
{
    /// <summary>The latest status.</summary>
    DaemonStatus Status { get; }

    /// <summary>The connected daemon; <see langword="null"/> while not connected.</summary>
    DaemonIdentity? Identity { get; }

    /// <summary>Whether connecting starts a stopped or unprovisioned engine; the choice persists.</summary>
    bool StartsEngineOnConnect { get; set; }

    /// <summary>
    /// The engine's resource usage about every two seconds while it runs; absent while it does not, or when the
    /// daemon does not report it. The first sample can show no CPU or memory yet.
    /// </summary>
    /// <remarks>Emits on a background thread; subscribers share one stream from the daemon.</remarks>
    IObservable<EngineUsage?> UsageChanges { get; }

    /// <summary>Emits the current status on subscription, then every change.</summary>
    /// <remarks>Emits on a background thread; UI subscribers marshal to their dispatcher.</remarks>
    IObservable<DaemonStatus> StatusChanges { get; }

    /// <summary>
    /// Connects, starting the daemon when none answers at the per-user endpoint. Concurrent calls
    /// share one attempt.
    /// </summary>
    /// <returns>A failure when no daemon was reached; the connection then retries in the background.</returns>
    Task<Result> ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Provisions the engine when needed and starts it; completes when the operation ends.</summary>
    Task<Result> StartEngineAsync(CancellationToken cancellationToken);

    /// <summary>Starts the engine, as the overload without progress does, reporting each step.</summary>
    /// <param name="progress">Receives the daemon's steps, on the thread that reads them.</param>
    /// <param name="cancellationToken">Stops waiting; the daemon goes on with the operation.</param>
    Task<Result> StartEngineAsync(IProgress<EngineProgress> progress, CancellationToken cancellationToken);

    /// <summary>Stops the engine and its containers.</summary>
    Task<Result> StopEngineAsync(CancellationToken cancellationToken);

    /// <summary>Stops the engine, then starts it.</summary>
    Task<Result> RestartEngineAsync(CancellationToken cancellationToken);
}
