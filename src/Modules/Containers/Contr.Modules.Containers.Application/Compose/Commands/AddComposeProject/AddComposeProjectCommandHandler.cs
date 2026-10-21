using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Compose.Commands.AddComposeProject;

public sealed class AddComposeProjectCommandHandler
    : IRequestHandler<AddComposeProjectCommand, Result<ComposeProjectDto>>
{
    private readonly IComposeGateway _compose;

    public AddComposeProjectCommandHandler(IComposeGateway compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        _compose = compose;
    }

    public Task<Result<ComposeProjectDto>> Handle(AddComposeProjectCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _compose.AddProjectAsync(request.ConfigFiles, cancellationToken);
    }
}
