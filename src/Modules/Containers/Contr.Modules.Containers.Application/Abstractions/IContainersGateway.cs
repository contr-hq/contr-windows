using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Abstractions;

/// <summary>The engine's containers, as the module's adapter reaches them through the daemon.</summary>
/// <remarks>Streams never throw for daemon failures; they end with a failed item instead.</remarks>
public interface IContainersGateway
{
    /// <summary>The list, then every change to it, until cancelled or broken.</summary>
    IAsyncEnumerable<ListChange<ContainerDto>> WatchAsync(CancellationToken cancellationToken);

    Task<Result> StartAsync(ContainerId id, CancellationToken cancellationToken);

    Task<Result> StopAsync(ContainerId id, CancellationToken cancellationToken);

    Task<Result> RestartAsync(ContainerId id, CancellationToken cancellationToken);

    /// <param name="force">Kills a running container first.</param>
    Task<Result> RemoveAsync(ContainerId id, bool force, CancellationToken cancellationToken);

    /// <summary>Creates a container from a local image and starts it; one that cannot start is removed.</summary>
    Task<Result<CreatedContainer>> CreateAsync(ContainerSpec spec, CancellationToken cancellationToken);

    /// <summary>Deletes every stopped container.</summary>
    Task<Result<ContainersPruned>> PruneAsync(CancellationToken cancellationToken);

    Task<Result> PauseAsync(ContainerId id, CancellationToken cancellationToken);

    Task<Result> UnpauseAsync(ContainerId id, CancellationToken cancellationToken);

    /// <summary>Sends SIGKILL to the container's main process.</summary>
    Task<Result> KillAsync(ContainerId id, CancellationToken cancellationToken);

    Task<Result> RenameAsync(ContainerId id, ContainerName name, CancellationToken cancellationToken);

    /// <summary>The log <paramref name="range"/> selects, then new output until the container stops.</summary>
    IAsyncEnumerable<ContainerLogEvent> StreamLogsAsync(
        ContainerId id, LogRange range, CancellationToken cancellationToken);

    /// <summary>Usage samples, about one per second, until the container stops.</summary>
    IAsyncEnumerable<ContainerStatsEvent> StreamStatsAsync(ContainerId id, CancellationToken cancellationToken);

    Task<Result<ContainerDetailsDto>> InspectAsync(ContainerId id, CancellationToken cancellationToken);

    /// <summary>Starts <paramref name="shell"/> in the container with a terminal on its input and output.</summary>
    /// <param name="user">Such as <c>root</c>; empty runs as the container's own user.</param>
    Task<Result<ITerminalSession>> OpenTerminalAsync(
        ContainerId id, string shell, string user, int columns, int rows, CancellationToken cancellationToken);
}
