using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.PruneContainers;

public sealed class PruneContainersCommandHandler : IRequestHandler<PruneContainersCommand, Result<ContainersPruned>>
{
    private readonly IContainersGateway _containers;

    public PruneContainersCommandHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result<ContainersPruned>> Handle(PruneContainersCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _containers.PruneAsync(cancellationToken);
    }
}
