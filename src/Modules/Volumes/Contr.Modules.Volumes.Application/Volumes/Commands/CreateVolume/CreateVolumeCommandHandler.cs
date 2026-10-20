using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Volumes.Application.Abstractions;
using Contr.Modules.Volumes.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Application.Volumes.Commands.CreateVolume;

public sealed class CreateVolumeCommandHandler : IRequestHandler<CreateVolumeCommand, Result>
{
    private readonly IVolumesGateway _volumes;

    public CreateVolumeCommandHandler(IVolumesGateway volumes)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        _volumes = volumes;
    }

    public Task<Result> Handle(CreateVolumeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = VolumeName.Parse(request.Name);

        return name.IsSuccess
            ? _volumes.CreateAsync(name.Value, cancellationToken)
            : Task.FromResult(Result.Failure(name.Error));
    }
}
