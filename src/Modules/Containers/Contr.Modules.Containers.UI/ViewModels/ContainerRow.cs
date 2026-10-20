using System;
using System.Collections.Generic;
using System.Linq;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>One row of the containers list.</summary>
public sealed record ContainerRow(
    ContainerId ContainerId,
    string Name,
    string Project,
    string Image,
    ContainerState State,
    string Status,
    string Ports)
{
    public string Id => ContainerId.Value;

    public string ShortId => ContainerId.Short;

    public bool HasProject => Project.Length > 0;

    public bool CanStart => State.CanStart();

    public bool CanStop => State.CanStop();

    public bool CanRestart => State.CanRestart();

    public bool CanRemove => State.CanRemove();

    public static ContainerRow From(ContainerDto container)
    {
        ArgumentNullException.ThrowIfNull(container);

        return new ContainerRow(
            container.Id,
            container.Name,
            container.ComposeProject,
            container.Image,
            container.State,
            container.Status,
            FormatPorts(container.Ports));
    }

    /// <summary>Compose projects first, grouped and by name, then the rest by name.</summary>
    public static int Compare(ContainerRow? left, ContainerRow? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var byProject = (left.HasProject, right.HasProject) switch {
            (true, false) => -1,
            (false, true) => 1,
            _ => string.Compare(left.Project, right.Project, StringComparison.OrdinalIgnoreCase),
        };

        return byProject != 0
            ? byProject
            : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Published ports as <c>8080→80/tcp</c>, exposed-only ones as <c>5432/tcp</c>.</summary>
    /// <remarks>The engine lists a binding once per address family; each binding is shown once.</remarks>
    public static string FormatPorts(IReadOnlyList<PortDto> ports)
    {
        ArgumentNullException.ThrowIfNull(ports);

        var formatted = ports
            .Select(port => port.HostPort > 0
                ? $"{port.HostPort}→{port.ContainerPort}/{port.Protocol}"
                : $"{port.ContainerPort}/{port.Protocol}")
            .Distinct(StringComparer.Ordinal);

        return string.Join(", ", formatted);
    }
}
