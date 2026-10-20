using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;

namespace Contr.Modules.Containers.Application.Containers.Queries.StreamContainerLogs;

public sealed class StreamContainerLogsQueryHandler
    : IRequestHandler<StreamContainerLogsQuery, IAsyncEnumerable<ContainerLogEvent>>
{
    private readonly IContainersGateway _containers;

    public StreamContainerLogsQueryHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<IAsyncEnumerable<ContainerLogEvent>> Handle(
        StreamContainerLogsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(_containers.StreamLogsAsync(request.Id, request.Range, cancellationToken));
    }
}
