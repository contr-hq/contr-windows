using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Volumes.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Application.Volumes.Commands.PruneVolumes;

public sealed class PruneVolumesCommandHandler : IRequestHandler<PruneVolumesCommand, Result<VolumePruneResult>>
{
    private readonly IVolumesGateway _volumes;

    public PruneVolumesCommandHandler(IVolumesGateway volumes)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        _volumes = volumes;
    }

    public Task<Result<VolumePruneResult>> Handle(PruneVolumesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _volumes.PruneAsync(request.All, cancellationToken);
    }
}
