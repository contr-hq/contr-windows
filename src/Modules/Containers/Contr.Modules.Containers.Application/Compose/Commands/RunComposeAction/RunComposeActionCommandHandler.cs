using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;

namespace Contr.Modules.Containers.Application.Compose.Commands.RunComposeAction;

public sealed class RunComposeActionCommandHandler
    : IRequestHandler<RunComposeActionCommand, IAsyncEnumerable<ComposeActionEvent>>
{
    private readonly IComposeGateway _compose;

    public RunComposeActionCommandHandler(IComposeGateway compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        _compose = compose;
    }

    public Task<IAsyncEnumerable<ComposeActionEvent>> Handle(
        RunComposeActionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(_compose.RunActionAsync(
            request.Project, request.Action, request.Services, request.RemoveVolumes, cancellationToken));
    }
}
