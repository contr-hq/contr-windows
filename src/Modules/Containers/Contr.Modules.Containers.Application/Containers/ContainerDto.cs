using System;
using System.Collections.Generic;
using Contr.Modules.Containers.Domain;

namespace Contr.Modules.Containers.Application.Containers;

/// <summary>A container as the list shows it.</summary>
/// <param name="ComposeProject">The Compose project that created the container; empty for others.</param>
/// <param name="ComposeFiles">The project's Compose files, comma-separated as in the label; may be empty.</param>
public sealed record ContainerDto(
    ContainerId Id,
    string Name,
    string Image,
    ContainerState State,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PortDto> Ports,
    string ComposeProject,
    string ComposeFiles);

/// <summary>An exposed container port and, when published, its host binding.</summary>
/// <param name="HostPort">Zero when the port is exposed but not published.</param>
public sealed record PortDto(int ContainerPort, int HostPort, string HostIp, string Protocol);
