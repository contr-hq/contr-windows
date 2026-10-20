namespace Contr.UI.Contracts.Services.Daemon;

/// <summary>Whether the client holds a daemon connection that passed the handshake.</summary>
public enum DaemonConnectionState
{
    /// <summary>A connection attempt is in progress.</summary>
    Connecting,

    /// <summary>The daemon answered the handshake.</summary>
    Connected,

    /// <summary>No daemon answered, or the connection broke; the client keeps retrying.</summary>
    Unavailable,
}

/// <summary>Lifecycle state of the container engine as the daemon reports it.</summary>
public enum EngineState
{
    /// <summary>The daemon has not reported a state.</summary>
    Unknown,

    /// <summary>The engine was never provisioned on this machine.</summary>
    Uninitialized,

    /// <summary>The engine is being installed.</summary>
    Provisioning,

    /// <summary>The engine is installed and stopped.</summary>
    Stopped,

    /// <summary>The engine is starting.</summary>
    Starting,

    /// <summary>The engine accepts requests.</summary>
    Running,

    /// <summary>The engine is stopping.</summary>
    Stopping,

    /// <summary>The engine runtime is being upgraded.</summary>
    Updating,

    /// <summary>The engine's data is being reset.</summary>
    Resetting,

    /// <summary>The engine failed; <see cref="DaemonStatus.Problem"/> says why.</summary>
    Failed,
}

/// <summary>The daemon connection and engine state at one moment.</summary>
/// <param name="Connection">Whether the daemon is reachable.</param>
/// <param name="Engine">The engine state; <see cref="EngineState.Unknown"/> unless connected.</param>
/// <param name="Problem">A user-facing reason when unavailable or failed; empty otherwise.</param>
public sealed record DaemonStatus(DaemonConnectionState Connection, EngineState Engine, string Problem)
{
    /// <summary>The status before the first connection attempt finishes.</summary>
    public static DaemonStatus Connecting { get; } =
        new(DaemonConnectionState.Connecting, EngineState.Unknown, string.Empty);
}

/// <summary>The daemon the client is connected to, as its handshake described it.</summary>
/// <param name="Endpoint">The local endpoint, such as <c>npipe:////./pipe/contr-S-1-5-21-…-api</c>.</param>
/// <param name="Version">The daemon's release version.</param>
/// <param name="ApiRevision">The daemon's <c>proto/API_REVISION</c>.</param>
/// <param name="Backend">The engine backend, such as <c>WSL</c> or <c>Fake</c>.</param>
public sealed record DaemonIdentity(string Endpoint, string Version, uint ApiRevision, string Backend);
