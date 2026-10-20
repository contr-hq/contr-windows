using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Commands.RemoveNetwork;

public sealed class RemoveNetworkCommandHandler : IRequestHandler<RemoveNetworkCommand, Result>
{
    private readonly INetworksGateway _networks;

    public RemoveNetworkCommandHandler(INetworksGateway networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        _networks = networks;
    }

    public Task<Result> Handle(RemoveNetworkCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return PredefinedNetworks.IsPredefined(request.Name)
            ? Task.FromResult(Result.Failure(NetworksErrors.Predefined))
            : _networks.RemoveAsync(request.Id, cancellationToken);
    }
}
