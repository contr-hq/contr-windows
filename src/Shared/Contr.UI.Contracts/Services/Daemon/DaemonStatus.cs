using System;
using System.Collections.Generic;

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

    /// <summary>When this client saw the engine enter <see cref="Engine"/>; absent until it did.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>The running engine's identity; absent unless <see cref="Engine"/> is running.</summary>
    public EngineDetails? Details { get; init; }

    /// <summary>Why the engine failed, such as <c>VIRTUALIZATION_DISABLED</c>; empty without a reason.</summary>
    public string ProblemCode { get; init; } = string.Empty;
}

/// <summary>The running engine as the daemon describes it.</summary>
/// <param name="Version">The engine's release version, such as <c>29.9.1</c>.</param>
/// <param name="DockerContext">The Docker CLI context targeting the engine; empty when the daemon made none.</param>
public sealed record EngineDetails(string Version, string DockerContext);

/// <summary>One step of a long engine operation, such as provisioning it.</summary>
/// <param name="Message">The daemon's description of the step.</param>
/// <param name="Fraction">The share done, 0 to 1; absent while the daemon cannot tell.</param>
public sealed record EngineProgress(string Message, double? Fraction);

/// <summary>The daemon the client is connected to, as its handshake described it.</summary>
/// <param name="Endpoint">The local endpoint, such as <c>npipe:////./pipe/contr-S-1-5-21-…-api</c>.</param>
/// <param name="Version">The daemon's release version.</param>
/// <param name="ApiRevision">The daemon's <c>proto/API_REVISION</c>.</param>
/// <param name="Backend">The engine backend, such as <c>WSL 2</c> or <c>Fake</c>.</param>
public sealed record DaemonIdentity(string Endpoint, string Version, uint ApiRevision, string Backend)
{
    /// <summary>The optional features the daemon offers; a client hides every feature not listed.</summary>
    public IReadOnlySet<DaemonCapability> Capabilities { get; init; } = new HashSet<DaemonCapability>();
}

/// <summary>An optional feature a daemon may offer, as its handshake lists them.</summary>
public enum DaemonCapability
{
    /// <summary>Runs Compose projects for the client.</summary>
    Compose,

    /// <summary>Runs a Kubernetes cluster on the engine.</summary>
    Kubernetes,

    /// <summary>Limits the CPU, memory and disk the engine may use.</summary>
    ResourceLimits,

    /// <summary>Lets the user's WSL distributions reach the engine.</summary>
    WslIntegration,

    /// <summary>Forwards published ports to the host.</summary>
    HostPortForwarding,

    /// <summary>Resets the engine to a clean state.</summary>
    Reset,

    /// <summary>Gives the engine the proxy, certificate and registry mirror settings.</summary>
    EngineConfiguration,
}

/// <summary>What a reset of the engine keeps.</summary>
public enum EngineResetScope
{
    /// <summary>Replaces the engine's runtime and keeps images, containers and volumes; it ends stopped.</summary>
    Runtime,

    /// <summary>Deletes every image, container, volume and the build cache; the engine needs setting up anew.</summary>
    Factory,
}

/// <summary>The resources the engine uses: its running containers' CPU and memory, and its data's disk.</summary>
/// <param name="CpuPercent">100 per fully used CPU, summed over running containers.</param>
/// <param name="Cpus">CPUs of the machine the engine runs on; on Windows, the WSL virtual machine.</param>
/// <param name="MemoryUsedBytes">Summed over running containers, as <c>docker stats</c> counts it.</param>
/// <param name="MemoryTotalBytes">Memory of the machine the engine runs on.</param>
/// <param name="DiskUsedBytes">Images, containers, volumes and build cache; zero until first measured.</param>
/// <param name="RunningContainers">The containers the CPU and memory are summed over.</param>
public sealed record EngineUsage(
    double CpuPercent,
    int Cpus,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    long DiskUsedBytes,
    int RunningContainers)
{
    /// <summary>The share of the engine's CPUs in use, 0 to 100.</summary>
    public double CpuShare => Cpus > 0 ? Math.Clamp(CpuPercent / Cpus, 0, 100) : CpuPercent;
}
