using System;
using System.Collections.Generic;
using System.Linq;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.Domain;
using VolumesV1 = Contr.Sdk.Volumes.V1;
using WatchCase = Contr.Sdk.Volumes.V1.WatchVolumesResponse.EventOneofCase;

namespace Contr.Modules.Volumes.UI.Infrastructure;

/// <summary>Maps the daemon's volume messages to the application's DTOs.</summary>
internal static class VolumeMapping
{
    /// <summary>The label Docker Compose puts on every volume it creates.</summary>
    private const string _composeProjectLabel = "com.docker.compose.project";

    /// <summary>Returns <see langword="null"/> for an event kind this client does not know.</summary>
    public static ListChange<VolumeDto>? ToChange(VolumesV1.WatchVolumesResponse response)
    {
        return response.EventCase switch {
            WatchCase.Snapshot => new ListChange<VolumeDto>.Snapshot([.. response.Snapshot.Volumes.Select(ToDto)]),
            WatchCase.Upserted => new ListChange<VolumeDto>.Upserted(ToDto(response.Upserted)),
            WatchCase.RemovedName => new ListChange<VolumeDto>.Removed(response.RemovedName),
            _ => null,
        };
    }

    public static VolumeDto ToDto(VolumesV1.Volume volume)
    {
        var project = volume.Labels.TryGetValue(_composeProjectLabel, out var label) ? label : string.Empty;

        return new VolumeDto(
            new VolumeName(volume.Name),
            volume.Driver,
            volume.Mountpoint,
            volume.CreatedAt?.ToDateTimeOffset(),
            project,
            new Dictionary<string, string>(volume.Labels, StringComparer.Ordinal));
    }
}
