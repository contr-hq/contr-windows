using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;

namespace Contr.Modules.Containers.Application.Containers.Queries.StreamContainerStats;

public sealed class StreamContainerStatsQueryHandler
    : IRequestHandler<StreamContainerStatsQuery, IAsyncEnumerable<ContainerStatsEvent>>
{
    private readonly IContainersGateway _containers;

    public StreamContainerStatsQueryHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<IAsyncEnumerable<ContainerStatsEvent>> Handle(
        StreamContainerStatsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(_containers.StreamStatsAsync(request.Id, cancellationToken));
    }
}
