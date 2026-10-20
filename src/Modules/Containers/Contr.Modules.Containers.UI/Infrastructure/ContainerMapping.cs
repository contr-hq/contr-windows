using System;
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
        var project = container.Labels.TryGetValue(_composeProjectLabel, out var label) ? label : string.Empty;

        return new ContainerDto(
            id,
            name,
            container.Image,
            ToState(container.State),
            container.Status,
            container.CreatedAt?.ToDateTimeOffset() ?? DateTimeOffset.UnixEpoch,
            [.. container.Ports.Select(ToPort)],
            project);
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
