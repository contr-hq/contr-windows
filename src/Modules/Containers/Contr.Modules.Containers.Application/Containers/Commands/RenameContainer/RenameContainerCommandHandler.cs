using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.RenameContainer;

public sealed class RenameContainerCommandHandler : IRequestHandler<RenameContainerCommand, Result>
{
    private readonly IContainersGateway _containers;

    public RenameContainerCommandHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result> Handle(RenameContainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = ContainerName.Parse(request.Name);

        return name.IsSuccess
            ? _containers.RenameAsync(request.Id, name.Value, cancellationToken)
            : Task.FromResult(Result.Failure(name.Error));
    }
}
