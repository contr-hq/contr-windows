using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Abstractions;

/// <summary>The engine's networks, as the module's adapter reaches them through the daemon.</summary>
/// <remarks>Streams never throw for daemon failures; they end with a failed item instead.</remarks>
public interface INetworksGateway
{
    /// <summary>The list, then every change to it, until cancelled or broken.</summary>
    IAsyncEnumerable<ListChange<NetworkDto>> WatchAsync(CancellationToken cancellationToken);

    /// <summary>Deletes a network; the engine refuses while a container is connected to it.</summary>
    Task<Result> RemoveAsync(NetworkId id, CancellationToken cancellationToken);

    /// <summary>Creates a bridge network with a subnet the engine allocates.</summary>
    Task<Result> CreateAsync(string name, CancellationToken cancellationToken);

    /// <summary>The running containers connected to the network.</summary>
    Task<Result<IReadOnlyList<NetworkEndpointDto>>> GetEndpointsAsync(
        NetworkId id, CancellationToken cancellationToken);

    /// <summary>Every container, running or not, with the networks it is connected to.</summary>
    Task<Result<IReadOnlyList<NetworkContainerDto>>> ListContainersAsync(CancellationToken cancellationToken);

    /// <summary>Connects a container to the network.</summary>
    /// <param name="container">The container's ID or name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task<Result> ConnectAsync(NetworkId id, string container, CancellationToken cancellationToken);

    /// <summary>Disconnects a container from the network.</summary>
    /// <param name="container">The container's ID or name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task<Result> DisconnectAsync(NetworkId id, string container, CancellationToken cancellationToken);

    /// <summary>Deletes unused user-defined networks; returns how many it deleted.</summary>
    Task<Result<int>> PruneAsync(CancellationToken cancellationToken);
}
