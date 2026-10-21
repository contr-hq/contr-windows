using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Queries.InspectNetwork;

public sealed class GetNetworkEndpointsQueryHandler
    : IRequestHandler<GetNetworkEndpointsQuery, Result<IReadOnlyList<NetworkEndpointDto>>>
{
    private readonly INetworksGateway _networks;

    public GetNetworkEndpointsQueryHandler(INetworksGateway networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        _networks = networks;
    }

    public Task<Result<IReadOnlyList<NetworkEndpointDto>>> Handle(
        GetNetworkEndpointsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _networks.GetEndpointsAsync(request.Id, cancellationToken);
    }
}
