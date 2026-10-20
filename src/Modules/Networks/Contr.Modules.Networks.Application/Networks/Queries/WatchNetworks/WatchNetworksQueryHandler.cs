using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Networks.Application.Abstractions;

namespace Contr.Modules.Networks.Application.Networks.Queries.WatchNetworks;

public sealed class WatchNetworksQueryHandler
    : IRequestHandler<WatchNetworksQuery, IAsyncEnumerable<ListChange<NetworkDto>>>
{
    private readonly INetworksGateway _networks;

    public WatchNetworksQueryHandler(INetworksGateway networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        _networks = networks;
    }

    public Task<IAsyncEnumerable<ListChange<NetworkDto>>> Handle(
        WatchNetworksQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_networks.WatchAsync(cancellationToken));
    }
}
