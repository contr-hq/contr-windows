using System;
using System.Collections.Generic;
using System.Linq;
using Contr.Modules.Volumes.Application.Volumes;

namespace Contr.Modules.Volumes.UI.ViewModels;

/// <summary>A volume with the containers that mount it, as the list and the details panel show it.</summary>
/// <param name="Mounts">The containers that mount the volume; absent while that is unknown.</param>
public sealed record VolumeItem(VolumeRow Row, IReadOnlyList<VolumeMountDto>? Mounts, bool IsSelected)
{
    public string Name => Row.Name;

    public bool InUse => Mounts is { Count: > 0 };

    public bool IsUnused => Mounts is { Count: 0 };

    public IReadOnlyList<VolumeMountDto> KnownMounts => Mounts ?? [];

    /// <summary>The containers' names, such as <c>db, worker</c>, or <c>Not used</c>.</summary>
    public string UsedBy => Mounts switch {
        null => "—",
        { Count: 0 } => "Not used",
        _ => string.Join(", ", Names()),
    };

    /// <summary>Why the volume cannot be deleted; empty when it can.</summary>
    public string DeleteNote => Names() switch {
        [] => string.Empty,
        [var name] => $"It's in use by {name}. Delete {name} before you delete this volume.",
        var names => $"It's in use by {string.Join(", ", names)}. "
            + "Delete those containers before you delete this volume.",
    };

    public string Details => Row.Driver.Length > 0 ? $"{Row.Driver} driver" : string.Empty;

    public bool HasLabels => Row.Labels.Count > 0;

    private List<string> Names()
    {
        return [.. KnownMounts
            .Select(mount => mount.Container)
            .Distinct(StringComparer.Ordinal)];
    }
}
