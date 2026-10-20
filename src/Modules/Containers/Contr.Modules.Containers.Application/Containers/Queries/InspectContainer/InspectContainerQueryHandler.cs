using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Queries.InspectContainer;

public sealed class InspectContainerQueryHandler : IRequestHandler<InspectContainerQuery, Result<ContainerDetailsDto>>
{
    private readonly IContainersGateway _containers;

    public InspectContainerQueryHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result<ContainerDetailsDto>> Handle(InspectContainerQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _containers.InspectAsync(request.Id, cancellationToken);
    }
}
