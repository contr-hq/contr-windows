using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Abstractions;

namespace Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;

public sealed class WatchContainersQueryHandler
    : IRequestHandler<WatchContainersQuery, IAsyncEnumerable<ListChange<ContainerDto>>>
{
    private readonly IContainersGateway _containers;

    public WatchContainersQueryHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<IAsyncEnumerable<ListChange<ContainerDto>>> Handle(
        WatchContainersQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_containers.WatchAsync(cancellationToken));
    }
}
