using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Volumes.Application.Abstractions;

namespace Contr.Modules.Volumes.Application.Volumes.Queries.WatchVolumes;

public sealed class WatchVolumesQueryHandler
    : IRequestHandler<WatchVolumesQuery, IAsyncEnumerable<ListChange<VolumeDto>>>
{
    private readonly IVolumesGateway _volumes;

    public WatchVolumesQueryHandler(IVolumesGateway volumes)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        _volumes = volumes;
    }

    public Task<IAsyncEnumerable<ListChange<VolumeDto>>> Handle(
        WatchVolumesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_volumes.WatchAsync(cancellationToken));
    }
}
