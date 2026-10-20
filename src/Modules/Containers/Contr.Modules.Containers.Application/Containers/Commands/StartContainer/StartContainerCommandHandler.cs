using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.StartContainer;

public sealed class StartContainerCommandHandler : IRequestHandler<StartContainerCommand, Result>
{
    private readonly IContainersGateway _containers;

    public StartContainerCommandHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result> Handle(StartContainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.State.CanStart()
            ? _containers.StartAsync(request.Id, cancellationToken)
            : Task.FromResult(Result.Failure(ContainersErrors.CannotStart));
    }
}
