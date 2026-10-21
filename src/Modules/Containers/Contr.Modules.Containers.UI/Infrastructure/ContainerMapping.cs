using System;
using System.Collections.Generic;
using System.Linq;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using ContainersV1 = Contr.Sdk.Containers.V1;
using EventCase = Contr.Sdk.Containers.V1.WatchContainersResponse.EventOneofCase;

namespace Contr.Modules.Containers.UI.Infrastructure;

/// <summary>Maps the daemon's container messages to the application's DTOs.</summary>
internal static class ContainerMapping
{
    /// <summary>The label Docker Compose puts on every container it creates.</summary>
    private const string _composeProjectLabel = "com.docker.compose.project";

    private const string _composeFilesLabel = "com.docker.compose.project.config_files";
    private const string _composeServiceLabel = "com.docker.compose.service";
    private const string _composeFolderLabel = "com.docker.compose.project.working_dir";

    /// <summary>Returns <see langword="null"/> for an event kind this client does not know.</summary>
    public static ListChange<ContainerDto>? ToChange(ContainersV1.WatchContainersResponse response)
    {
        return response.EventCase switch {
            EventCase.Snapshot => new ListChange<ContainerDto>.Snapshot(
                [.. response.Snapshot.Containers.Select(ToDto)]),
            EventCase.Upserted => new ListChange<ContainerDto>.Upserted(ToDto(response.Upserted)),
            EventCase.RemovedId => new ListChange<ContainerDto>.Removed(response.RemovedId),
            _ => null,
        };
    }

    public static ContainerDto ToDto(ContainersV1.Container container)
    {
        var id = new ContainerId(container.Id);
        var name = container.Names.Count > 0 ? container.Names[0] : id.Short;
        var project = container.Labels.GetValueOrDefault(_composeProjectLabel, string.Empty);
        var files = container.Labels.GetValueOrDefault(_composeFilesLabel, string.Empty);

        return new ContainerDto(
            id,
            name,
            container.Image,
            ToState(container.State),
            container.Status,
            container.CreatedAt?.ToDateTimeOffset() ?? DateTimeOffset.UnixEpoch,
            [.. container.Ports.Select(ToPort)],
            project,
            files,
            container.Labels.GetValueOrDefault(_composeServiceLabel, string.Empty),
            container.Labels.GetValueOrDefault(_composeFolderLabel, string.Empty));
    }

    /// <summary>Returns <see langword="null"/> for a response without a sample.</summary>
    public static ContainerStatsEvent? ToStatsEvent(ContainersV1.StreamStatsResponse response)
    {
        return response.Stats is { } stats ? new ContainerStatsEvent.Sample(ToStats(stats)) : null;
    }

    public static ContainerStatsDto ToStats(ContainersV1.ContainerStats stats)
    {
        return new ContainerStatsDto(
            stats.ReadAt?.ToDateTimeOffset() ?? DateTimeOffset.Now,
            stats.CpuPercent,
            ToLong(stats.MemoryUsageBytes),
            ToLong(stats.MemoryLimitBytes),
            ToLong(stats.NetworkRxBytes),
            ToLong(stats.NetworkTxBytes),
            ToLong(stats.BlockReadBytes),
            ToLong(stats.BlockWriteBytes),
            ToLong(stats.Pids));
    }

    public static ContainerDetailsDto ToDetails(ContainerId id, ContainersV1.InspectContainerResponse response)
    {
        var details = response.Details ?? new ContainersV1.ContainerDetails();

        return new ContainerDetailsDto(
            ToDto(response.Container ?? new ContainersV1.Container { Id = id.Value }),
            details.StartedAt?.ToDateTimeOffset(),
            details.FinishedAt?.ToDateTimeOffset(),
            details.ExitCode,
            (int)details.RestartCount,
            ToHealth(details.Health),
            details.WorkingDir,
            [.. details.Env],
            [.. details.Mounts.Select(ToMount)],
            response.RawJson);
    }

    private static ContainerHealth ToHealth(ContainersV1.HealthStatus health)
    {
        return health switch {
            ContainersV1.HealthStatus.Starting => ContainerHealth.Starting,
            ContainersV1.HealthStatus.Healthy => ContainerHealth.Healthy,
            ContainersV1.HealthStatus.Unhealthy => ContainerHealth.Unhealthy,
            _ => ContainerHealth.None,
        };
    }

    private static MountDto ToMount(ContainersV1.Mount mount)
    {
        var kind = mount.Kind switch {
            ContainersV1.MountKind.Bind => "bind",
            ContainersV1.MountKind.Volume => "volume",
            ContainersV1.MountKind.Tmpfs => "tmpfs",
            ContainersV1.MountKind.Npipe => "npipe",
            ContainersV1.MountKind.Cluster => "cluster",
            ContainersV1.MountKind.Image => "image",
            _ => "unknown",
        };

        return new MountDto(kind, mount.Name, mount.Source, mount.Destination, mount.ReadOnly);
    }

    private static long ToLong(ulong value)
    {
        return value > long.MaxValue ? long.MaxValue : (long)value;
    }

    public static ContainerState ToState(ContainersV1.ContainerState state)
    {
        return state switch {
            ContainersV1.ContainerState.Created => ContainerState.Created,
            ContainersV1.ContainerState.Running => ContainerState.Running,
            ContainersV1.ContainerState.Paused => ContainerState.Paused,
            ContainersV1.ContainerState.Restarting => ContainerState.Restarting,
            ContainersV1.ContainerState.Removing => ContainerState.Removing,
            ContainersV1.ContainerState.Exited => ContainerState.Exited,
            ContainersV1.ContainerState.Dead => ContainerState.Dead,
            ContainersV1.ContainerState.Stopping => ContainerState.Stopping,
            _ => ContainerState.Unknown,
        };
    }

    private static PortDto ToPort(ContainersV1.PortMapping port)
    {
        var protocol = port.Protocol switch {
            ContainersV1.Protocol.Udp => "udp",
            ContainersV1.Protocol.Sctp => "sctp",
            _ => "tcp",
        };

        return new PortDto((int)port.ContainerPort, (int)port.HostPort, port.HostIp, protocol);
    }
}
