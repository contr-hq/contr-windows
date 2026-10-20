using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Daemon.UI;
using Contr.Modules.Volumes.Application.Abstractions;
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.Domain;
using Contr.SharedKernel;
using VolumesV1 = Contr.Sdk.Volumes.V1;

namespace Contr.Modules.Volumes.UI.Infrastructure;

/// <summary>The <see cref="IVolumesGateway"/> over the daemon's VolumesService.</summary>
public sealed class VolumesGateway : IVolumesGateway
{
    private readonly IDaemonClient _daemon;

    public VolumesGateway(IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public IAsyncEnumerable<ListChange<VolumeDto>> WatchAsync(CancellationToken cancellationToken)
    {
        return _daemon.StreamAsync(
            (client, token) => client.Volumes.WatchVolumes(
                new VolumesV1.WatchVolumesRequest(), cancellationToken: token),
            VolumeMapping.ToChange,
            error => new ListChange<VolumeDto>.Failed(error),
            cancellationToken);
    }

    public Task<Result> CreateAsync(VolumeName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        var request = new VolumesV1.CreateVolumeRequest { Name = name.Value };

        return _daemon.RunAsync(
            (client, token) => client.Volumes
                .CreateVolumeAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public Task<Result> RemoveAsync(VolumeName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        var request = new VolumesV1.RemoveVolumeRequest { Name = name.Value };

        return _daemon.RunAsync(
            (client, token) => client.Volumes
                .RemoveVolumeAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }
}
