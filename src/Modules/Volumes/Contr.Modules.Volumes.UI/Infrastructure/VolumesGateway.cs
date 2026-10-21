using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Daemon.UI;
using Contr.Modules.Volumes.Application.Abstractions;
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.Domain;
using Contr.SharedKernel;
using ContainersV1 = Contr.Sdk.Containers.V1;
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

    /// <remarks>The list carries no mounts, so every container is inspected once.</remarks>
    public async Task<Result<IReadOnlyList<VolumeMountDto>>> GetUsageAsync(CancellationToken cancellationToken)
    {
        var listed = await _daemon
            .CallAsync(
                (client, token) => client.Containers
                    .ListContainersAsync(new ContainersV1.ListContainersRequest(), cancellationToken: token)
                    .ResponseAsync,
                cancellationToken)
            .ConfigureAwait(false);

        if (listed.IsFailure)
        {
            return Result.Failure<IReadOnlyList<VolumeMountDto>>(listed.Error);
        }

        var mounts = new List<VolumeMountDto>();
        foreach (var container in listed.Value.Containers)
        {
            var inspected = await InspectAsync(container.Id, cancellationToken).ConfigureAwait(false);

            if (inspected?.Details is not { } details)
            {
                continue;
            }

            var name = container.Names.Count > 0 ? container.Names[0] : container.Id;
            var isRunning = container.State == ContainersV1.ContainerState.Running;
            mounts.AddRange(details.Mounts
                .Where(mount => mount.Kind == ContainersV1.MountKind.Volume)
                .Select(mount => new VolumeMountDto(mount.Name, name, mount.Destination, isRunning)));
        }

        return Result.Success<IReadOnlyList<VolumeMountDto>>(mounts);
    }

    public async Task<Result<VolumePruneResult>> PruneAsync(bool all, CancellationToken cancellationToken)
    {
        var request = new VolumesV1.PruneVolumesRequest { All = all };
        var result = await _daemon
            .CallAsync(
                (client, token) => client.Volumes
                    .PruneVolumesAsync(request, cancellationToken: token)
                    .ResponseAsync,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result.Failure<VolumePruneResult>(result.Error);
        }

        var reclaimed = result.Value.ReclaimedBytes;

        return Result.Success(new VolumePruneResult(
            result.Value.Deleted.Count, reclaimed > long.MaxValue ? long.MaxValue : (long)reclaimed));
    }

    private async Task<ContainersV1.InspectContainerResponse?> InspectAsync(
        string id, CancellationToken cancellationToken)
    {
        var request = new ContainersV1.InspectContainerRequest { Id = id };
        var result = await _daemon
            .CallAsync(
                (client, token) => client.Containers
                    .InspectContainerAsync(request, cancellationToken: token)
                    .ResponseAsync,
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? result.Value : null;
    }
}
