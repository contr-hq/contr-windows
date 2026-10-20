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
}
