using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.KillContainer;

public sealed class KillContainerCommandHandler : IRequestHandler<KillContainerCommand, Result>
{
    private readonly IContainersGateway _containers;

    public KillContainerCommandHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result> Handle(KillContainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.State.CanKill()
            ? _containers.KillAsync(request.Id, cancellationToken)
            : Task.FromResult(Result.Failure(ContainersErrors.CannotKill));
    }
}
