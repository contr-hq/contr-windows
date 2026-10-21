using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;

namespace Contr.Modules.Containers.Application.Compose.Queries.StreamComposeLogs;

public sealed class StreamComposeLogsQueryHandler
    : IRequestHandler<StreamComposeLogsQuery, IAsyncEnumerable<ComposeLogEvent>>
{
    private readonly IComposeGateway _compose;

    public StreamComposeLogsQueryHandler(IComposeGateway compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        _compose = compose;
    }

    public Task<IAsyncEnumerable<ComposeLogEvent>> Handle(
        StreamComposeLogsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(_compose.StreamLogsAsync(
            request.Project, request.TailLines, request.Follow, cancellationToken));
    }
}
