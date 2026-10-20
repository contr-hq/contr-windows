using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.StopContainer;

public sealed class StopContainerCommandHandler : IRequestHandler<StopContainerCommand, Result>
{
    private readonly IContainersGateway _containers;

    public StopContainerCommandHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result> Handle(StopContainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.State.CanStop()
            ? _containers.StopAsync(request.Id, cancellationToken)
            : Task.FromResult(Result.Failure(ContainersErrors.CannotStop));
    }
}
