using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.RestartContainer;

public sealed class RestartContainerCommandHandler : IRequestHandler<RestartContainerCommand, Result>
{
    private readonly IContainersGateway _containers;

    public RestartContainerCommandHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result> Handle(RestartContainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.State.CanRestart()
            ? _containers.RestartAsync(request.Id, cancellationToken)
            : Task.FromResult(Result.Failure(ContainersErrors.CannotRestart));
    }
}
