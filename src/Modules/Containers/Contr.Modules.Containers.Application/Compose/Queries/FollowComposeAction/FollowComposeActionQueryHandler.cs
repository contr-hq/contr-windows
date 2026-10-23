using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;

namespace Contr.Modules.Containers.Application.Compose.Queries.FollowComposeAction;

public sealed class FollowComposeActionQueryHandler
    : IRequestHandler<FollowComposeActionQuery, IAsyncEnumerable<ComposeActionEvent>>
{
    private readonly IComposeGateway _compose;

    public FollowComposeActionQueryHandler(IComposeGateway compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        _compose = compose;
    }

    public Task<IAsyncEnumerable<ComposeActionEvent>> Handle(
        FollowComposeActionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(_compose.FollowActionAsync(request.OperationId, cancellationToken));
    }
}
