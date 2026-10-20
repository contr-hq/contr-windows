using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.RemoveContainer;

public sealed class RemoveContainerCommandHandler : IRequestHandler<RemoveContainerCommand, Result>
{
    private readonly IContainersGateway _containers;

    public RemoveContainerCommandHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result> Handle(RemoveContainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.State.CanRemove()
            ? _containers.RemoveAsync(request.Id, request.State.RemovalNeedsForce(), cancellationToken)
            : Task.FromResult(Result.Failure(ContainersErrors.CannotRemove));
    }
}
