using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Commands.ConnectContainer;

public sealed class ConnectContainerCommandHandler : IRequestHandler<ConnectContainerCommand, Result>
{
    private readonly INetworksGateway _networks;

    public ConnectContainerCommandHandler(INetworksGateway networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        _networks = networks;
    }

    public Task<Result> Handle(ConnectContainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _networks.ConnectAsync(request.Network, request.Container, cancellationToken);
    }
}
