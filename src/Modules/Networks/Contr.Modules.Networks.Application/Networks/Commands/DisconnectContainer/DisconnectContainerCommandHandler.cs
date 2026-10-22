using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Commands.DisconnectContainer;

public sealed class DisconnectContainerCommandHandler : IRequestHandler<DisconnectContainerCommand, Result>
{
    private readonly INetworksGateway _networks;

    public DisconnectContainerCommandHandler(INetworksGateway networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        _networks = networks;
    }

    public Task<Result> Handle(DisconnectContainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _networks.DisconnectAsync(request.Network, request.Container, cancellationToken);
    }
}
