using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.Domain;
using Contr.UI.Common.Formatting;

namespace Contr.Modules.Volumes.UI.ViewModels;

/// <summary>One volume as the list shows it, without the containers that use it.</summary>
/// <param name="Labels">Such as <c>com.docker.compose.volume=pgdata</c>, sorted.</param>
public sealed partial record VolumeRow(
    VolumeName VolumeName,
    string Project,
    string Driver,
    string Created,
    IReadOnlyList<string> Labels)
{
    public string Name => VolumeName.Value;

    public bool HasProject => Project.Length > 0;

    /// <summary>A volume the engine named itself, for a container that asked for storage without a name.</summary>
    public bool IsAnonymous => !HasProject && AnonymousName().IsMatch(Name);

    /// <summary>Such as <c>shop-api project</c> or <c>Anonymous volume</c>; empty for a plain named volume.</summary>
    public string Subtitle => HasProject ? $"{Project} project" : IsAnonymous ? "Anonymous volume" : string.Empty;

    public static VolumeRow From(VolumeDto volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        var created = volume.CreatedAt is { } createdAt
            ? HumanFormat.Ago(createdAt, DateTimeOffset.UtcNow)
            : string.Empty;
        var labels = volume.Labels
            .Select(label => $"{label.Key}={label.Value}")
            .Order(StringComparer.Ordinal)
            .ToList();

        return new VolumeRow(volume.Name, volume.ComposeProject, volume.Driver, created, labels);
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

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex AnonymousName();
}
