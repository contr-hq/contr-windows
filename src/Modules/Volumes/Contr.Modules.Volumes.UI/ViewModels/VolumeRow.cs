using System;
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.Domain;
using Contr.UI.Common.Formatting;

namespace Contr.Modules.Volumes.UI.ViewModels;

/// <summary>One row of the volumes list.</summary>
public sealed record VolumeRow(VolumeName VolumeName, string Project, string Driver, string Mountpoint, string Created)
{
    public string Name => VolumeName.Value;

    public bool HasProject => Project.Length > 0;

    public static VolumeRow From(VolumeDto volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        var created = volume.CreatedAt is { } createdAt
            ? HumanFormat.Age(createdAt, DateTimeOffset.UtcNow)
            : string.Empty;

        return new VolumeRow(volume.Name, volume.ComposeProject, volume.Driver, volume.Mountpoint, created);
    }

    /// <summary>Compose projects first, grouped and by name, then the rest by name.</summary>
    public static int Compare(VolumeRow? left, VolumeRow? right)
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
}
