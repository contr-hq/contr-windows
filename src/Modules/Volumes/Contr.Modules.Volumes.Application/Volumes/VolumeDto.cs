using System;
using System.Collections.Generic;
using Contr.Modules.Volumes.Domain;

namespace Contr.Modules.Volumes.Application.Volumes;

/// <summary>A volume as the list shows it.</summary>
/// <param name="Mountpoint">The path on the engine host, inside the engine's distribution or VM.</param>
/// <param name="CreatedAt">Absent when the driver does not report it.</param>
/// <param name="ComposeProject">The Compose project that created the volume; empty for others.</param>
public sealed record VolumeDto(
    VolumeName Name,
    string Driver,
    string Mountpoint,
    DateTimeOffset? CreatedAt,
    string ComposeProject,
    IReadOnlyDictionary<string, string> Labels);
