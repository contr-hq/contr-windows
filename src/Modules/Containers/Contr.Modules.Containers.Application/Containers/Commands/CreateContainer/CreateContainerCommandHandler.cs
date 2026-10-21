using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.CreateContainer;

public sealed class CreateContainerCommandHandler : IRequestHandler<CreateContainerCommand, Result<CreatedContainer>>
{
    private readonly IContainersGateway _containers;

    public CreateContainerCommandHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result<CreatedContainer>> Handle(CreateContainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var spec = request.Spec;

        if (spec.Image.Trim().Length == 0)
        {
            return Task.FromResult(Result.Failure<CreatedContainer>(ContainersErrors.ImageRequired));
        }

        var outside = spec.Mounts.FirstOrDefault(mount => !mount.Destination.StartsWith('/'));

        return outside is null
            ? _containers.CreateAsync(spec with { Image = spec.Image.Trim() }, cancellationToken)
            : Task.FromResult(Result.Failure<CreatedContainer>(ContainersErrors.PathInvalid(outside.Destination)));
    }
}
