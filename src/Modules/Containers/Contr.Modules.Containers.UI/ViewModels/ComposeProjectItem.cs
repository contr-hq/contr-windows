using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Contr.Modules.Containers.Application.Compose;
using Contr.Modules.Containers.Domain;
using Contr.UI.Common.Controls;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>One Compose project in the project list.</summary>
/// <param name="Location">The directory Compose works in; empty when unknown.</param>
/// <param name="Running">Containers in the running state.</param>
/// <param name="Total">Every container of the project.</param>
public sealed record ComposeProjectItem(string Name, string Location, int Running, int Total, bool IsAdded)
{
    public bool HasLocation => Location.Length > 0;

    public PillKind PillKind => Running == 0
        ? PillKind.Hollow
        : Running == Total ? PillKind.Good : PillKind.Caution;

    /// <summary>Such as <c>3 of 4 running</c>, or <c>Stopped</c> while none runs.</summary>
    public string StatusText => Running == 0
        ? "Stopped"
        : $"{Running.ToString(CultureInfo.CurrentCulture)} of {Total.ToString(CultureInfo.CurrentCulture)} running";

    public static ComposeProjectItem From(ComposeProjectDto project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var containers = project.Services
            .SelectMany(service => service.Containers)
            .ToList();

        return new ComposeProjectItem(
            project.Name,
            project.WorkingDir,
            containers.Count(container => container.State == ContainerState.Running),
            containers.Count,
            project.IsAdded);
    }

    public static int Compare(ComposeProjectDto? left, ComposeProjectDto? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return string.CompareOrdinal(left.Name, right.Name);
    }
}

/// <summary>One row of a project's services table.</summary>
/// <param name="Image">The image reference; empty for a service built from its own folder.</param>
/// <param name="BuildText">Such as <c>built from ./worker</c>; empty for a service that pulls its image.</param>
/// <param name="Ports">The published host ports of every container of the service.</param>
public sealed record ComposeServiceRow(
    string Name,
    string Image,
    string BuildText,
    string StatusText,
    PillKind PillKind,
    IReadOnlyList<PortLink> Ports,
    bool CanRestart)
{
    public bool IsBuilt => BuildText.Length > 0;

    public bool IsPulled => BuildText.Length == 0;

    public bool HasPorts => Ports.Count > 0;

    public bool HasNoPorts => Ports.Count == 0;

    public static ComposeServiceRow From(ComposeServiceDto service, string workingDir)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(workingDir);

        var containers = service.Containers
            .Select(container => ContainerRow.From(container, new ContainerUsage()))
            .ToList();
        var (status, kind) = Status(containers);
        var ports = PortLink.From([.. service.Containers.SelectMany(container => container.Ports)]);

        return new ComposeServiceRow(
            service.Name,
            service.BuildContext.Length > 0 ? string.Empty : service.Image,
            service.BuildContext.Length > 0 ? $"built from {Relative(service.BuildContext, workingDir)}" : string.Empty,
            status,
            kind,
            ports,
            containers.Any(container => container.CanRestart));
    }

    /// <summary>Such as <c>Running · healthy</c> for one container, or <c>2 of 3 running</c> for replicas.</summary>
    private static (string Text, PillKind Kind) Status(List<ContainerRow> containers)
    {
        if (containers.Count == 0)
        {
            return ("Not created", PillKind.Hollow);
        }

        if (containers.Count == 1)
        {
            var container = containers[0];
            var healthy = container.State == ContainerState.Running
                && ContainerStatusText.Health(container.Status) == ContainerHealth.Healthy;

            return (healthy ? $"{container.StatusText} · healthy" : container.StatusText, container.PillKind);
        }

        var running = containers.Count(container => container.State == ContainerState.Running);

        if (running == 0)
        {
            return (containers[0].StatusText, containers[0].PillKind);
        }

        var text = string.Create(CultureInfo.CurrentCulture, $"{running} of {containers.Count} running");

        return (text, running == containers.Count ? PillKind.Good : PillKind.Caution);
    }

    /// <summary>Such as <c>./worker</c> for a folder inside <paramref name="workingDir"/>, else the folder as is.
    /// </summary>
    private static string Relative(string folder, string workingDir)
    {
        if (workingDir.Length == 0)
        {
            return folder;
        }

        var relative = Path.GetRelativePath(workingDir, folder);

        return relative == "." || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? folder
            : $"./{relative.Replace('\\', '/')}";
    }
}

/// <summary>What became of the last Compose command run on the selected project.</summary>
/// <param name="Title">Such as <c>Up · shop-api</c> or <c>Restart · api</c>.</param>
public sealed record ComposeRun(string Title, string StatusText, PillKind PillKind)
{
    public bool IsRunning => PillKind == PillKind.Busy;

    public bool IsOver => PillKind != PillKind.Busy;
}

/// <summary>One line of a project's merged logs, prefixed with its service.</summary>
/// <remarks>Compared by reference: a service repeats lines, and the list scrolls to the one just added, which an
/// equal line before it would hide.</remarks>
/// <param name="color">An index into the page's service colours; each service keeps one.</param>
public sealed class ComposeLogLine(string service, int color, string text)
{
    public string Service { get; } = service;

    public int Color { get; } = color;

    public string Text { get; } = text;
}

/// <summary>One line a Compose command wrote.</summary>
/// <remarks>Compared by reference: Compose writes some lines twice, and the list scrolls to the one just added.
/// </remarks>
public sealed class ComposeOutputLine(string text)
{
    public string Text { get; } = text;
}
