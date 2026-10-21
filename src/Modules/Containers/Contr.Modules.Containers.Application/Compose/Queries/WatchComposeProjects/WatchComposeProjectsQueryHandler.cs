using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Abstractions;

namespace Contr.Modules.Containers.Application.Compose.Queries.WatchComposeProjects;

public sealed class WatchComposeProjectsQueryHandler
    : IRequestHandler<WatchComposeProjectsQuery, IAsyncEnumerable<ListChange<ComposeProjectDto>>>
{
    private readonly IComposeGateway _compose;

    public WatchComposeProjectsQueryHandler(IComposeGateway compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        _compose = compose;
    }

    public Task<IAsyncEnumerable<ListChange<ComposeProjectDto>>> Handle(
        WatchComposeProjectsQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_compose.WatchProjectsAsync(cancellationToken));
    }
}
