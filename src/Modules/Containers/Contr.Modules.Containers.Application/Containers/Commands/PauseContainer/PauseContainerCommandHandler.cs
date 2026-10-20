using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.PauseContainer;

public sealed class PauseContainerCommandHandler : IRequestHandler<PauseContainerCommand, Result>
{
    private readonly IContainersGateway _containers;

    public PauseContainerCommandHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result> Handle(PauseContainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.State.CanPause()
            ? _containers.PauseAsync(request.Id, cancellationToken)
            : Task.FromResult(Result.Failure(ContainersErrors.CannotPause));
    }
}
