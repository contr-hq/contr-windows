using System;
using System.Collections.Generic;
using System.Linq;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.UI.Common.Controls;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>One row of the containers list.</summary>
/// <param name="ComposeFile">The first of the project's Compose files; empty when unknown.</param>
/// <param name="Usage">The container's latest CPU and memory sample.</param>
public sealed record ContainerRow(
    ContainerId ContainerId,
    string Name,
    string Project,
    string ComposeFile,
    string Image,
    ContainerState State,
    string Status,
    IReadOnlyList<PortLink> Ports,
    ContainerUsage Usage)
{
    public string Id => ContainerId.Value;

    public string ShortId => ContainerId.Short;

    public bool HasProject => Project.Length > 0;

    public int? ExitCode => ContainerStatusText.ExitCode(Status);

    public bool IsRunning => State is ContainerState.Running or ContainerState.Paused or ContainerState.Restarting;

    /// <summary>Whether the container is down: created, exited or dead.</summary>
    public bool IsStopped => State is ContainerState.Created or ContainerState.Exited or ContainerState.Dead;

    /// <summary>Whether the container exited with a non-zero code or died.</summary>
    public bool HasFailed => State == ContainerState.Dead || ExitCode is { } code && code != 0;

    public StatusKind StatusKind => State switch {
        ContainerState.Running => ContainerStatusText.Health(Status) switch {
            ContainerHealth.Unhealthy => StatusKind.Caution,
            ContainerHealth.Starting => StatusKind.Busy,
            _ => StatusKind.Good,
        },
        ContainerState.Paused => StatusKind.Paused,
        ContainerState.Exited or ContainerState.Dead => HasFailed ? StatusKind.Critical : StatusKind.Neutral,
        ContainerState.Created or ContainerState.Unknown => StatusKind.Neutral,
        _ => StatusKind.Busy,
    };

    public string StatusText => State switch {
        ContainerState.Running => ContainerStatusText.Health(Status) switch {
            ContainerHealth.Unhealthy => "Unhealthy",
            ContainerHealth.Starting => "Starting",
            _ => "Running",
        },
        ContainerState.Exited => ExitCode is { } code ? $"Exited ({code})" : "Exited",
        _ => State.ToString(),
    };

    /// <summary>Such as <c>2 h</c>: how long it has been up, or since it exited.</summary>
    public string Elapsed => ContainerStatusText.Elapsed(Status);

    public bool HasPorts => Ports.Count > 0;

    public bool HasNoPorts => Ports.Count == 0;

    public bool CanStart => State.CanStart();

    public bool CanStop => State.CanStop();

    public bool CanRestart => State.CanRestart();

    public bool CanRemove => State.CanRemove();

    /// <summary>A row offers its logs while the container runs or after it failed.</summary>
    public bool OffersLogs => !IsStopped || HasFailed;

    /// <summary>A row offers deletion after a clean exit, where logs tell little.</summary>
    public bool OffersDelete => IsStopped && !HasFailed;

    public static ContainerRow From(ContainerDto container, ContainerUsage usage)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(usage);

        var composeFile = container.ComposeFiles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;

        return new ContainerRow(
            container.Id,
            container.Name,
            container.ComposeProject,
            composeFile,
            container.Image,
            container.State,
            container.Status,
            PortLink.From(container.Ports),
            usage);
    }

    /// <summary>By name.</summary>
    public static int Compare(ContainerRow? left, ContainerRow? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var byName = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);

        return byName != 0 ? byName : string.CompareOrdinal(left.Id, right.Id);
    }

    /// <summary>Whether the name, image, project or a published port contains <paramref name="filter"/>.</summary>
    public bool Matches(string filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return filter.Length == 0
            || Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || Image.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || Project.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || Ports.Any(port => port.Address.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || port.Target.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }
}
