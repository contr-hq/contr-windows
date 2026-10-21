using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Compose.Commands.RemoveComposeProject;

public sealed class RemoveComposeProjectCommandHandler : IRequestHandler<RemoveComposeProjectCommand, Result>
{
    private readonly IComposeGateway _compose;

    public RemoveComposeProjectCommandHandler(IComposeGateway compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        _compose = compose;
    }

    public Task<Result> Handle(RemoveComposeProjectCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _compose.RemoveProjectAsync(request.Project, cancellationToken);
    }
}
