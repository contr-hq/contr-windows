using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Commands.PruneNetworks;

public sealed class PruneNetworksCommandHandler : IRequestHandler<PruneNetworksCommand, Result<int>>
{
    private readonly INetworksGateway _networks;

    public PruneNetworksCommandHandler(INetworksGateway networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        _networks = networks;
    }

    public Task<Result<int>> Handle(PruneNetworksCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _networks.PruneAsync(cancellationToken);
    }
}
