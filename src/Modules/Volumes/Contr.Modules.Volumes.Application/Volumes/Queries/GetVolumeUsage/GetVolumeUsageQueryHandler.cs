using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Volumes.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Application.Volumes.Queries.GetVolumeUsage;

public sealed class GetVolumeUsageQueryHandler
    : IRequestHandler<GetVolumeUsageQuery, Result<IReadOnlyList<VolumeMountDto>>>
{
    private readonly IVolumesGateway _volumes;

    public GetVolumeUsageQueryHandler(IVolumesGateway volumes)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        _volumes = volumes;
    }

    public Task<Result<IReadOnlyList<VolumeMountDto>>> Handle(
        GetVolumeUsageQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _volumes.GetUsageAsync(cancellationToken);
    }
}
