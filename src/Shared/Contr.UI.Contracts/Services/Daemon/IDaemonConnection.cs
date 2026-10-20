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

    /// <summary>Stops the engine and its containers.</summary>
    Task<Result> StopEngineAsync(CancellationToken cancellationToken);

    /// <summary>Stops the engine, then starts it.</summary>
    Task<Result> RestartEngineAsync(CancellationToken cancellationToken);
}
