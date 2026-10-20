using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Commands.CreateNetwork;

public sealed class CreateNetworkCommandHandler : IRequestHandler<CreateNetworkCommand, Result>
{
    private readonly INetworksGateway _networks;

    public CreateNetworkCommandHandler(INetworksGateway networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        _networks = networks;
    }

    public Task<Result> Handle(CreateNetworkCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name.Trim();

        if (name.Length == 0 || PredefinedNetworks.IsPredefined(name))
        {
            return Task.FromResult(Result.Failure(NetworksErrors.InvalidName(name)));
        }

        return _networks.CreateAsync(name, cancellationToken);
    }
}
