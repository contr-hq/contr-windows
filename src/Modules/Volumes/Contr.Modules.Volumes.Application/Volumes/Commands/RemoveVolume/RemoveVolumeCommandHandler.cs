using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Volumes.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Application.Volumes.Commands.RemoveVolume;

public sealed class RemoveVolumeCommandHandler : IRequestHandler<RemoveVolumeCommand, Result>
{
    private readonly IVolumesGateway _volumes;

    public RemoveVolumeCommandHandler(IVolumesGateway volumes)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        _volumes = volumes;
    }

    public Task<Result> Handle(RemoveVolumeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _volumes.RemoveAsync(request.Name, cancellationToken);
    }
}
