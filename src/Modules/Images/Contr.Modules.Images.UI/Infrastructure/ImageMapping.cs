using System;
using System.Linq;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Domain;
using ImagesV1 = Contr.Sdk.Images.V1;
using PullCase = Contr.Sdk.Images.V1.PullImageResponse.EventOneofCase;
using WatchCase = Contr.Sdk.Images.V1.WatchImagesResponse.EventOneofCase;

namespace Contr.Modules.Images.UI.Infrastructure;

/// <summary>Maps the daemon's image messages to the application's DTOs.</summary>
internal static class ImageMapping
{
    /// <summary>Returns <see langword="null"/> for an event kind this client does not know.</summary>
    public static ListChange<ImageDto>? ToChange(ImagesV1.WatchImagesResponse response)
    {
        return response.EventCase switch {
            WatchCase.Snapshot => new ListChange<ImageDto>.Snapshot([.. response.Snapshot.Images.Select(ToDto)]),
            WatchCase.Upserted => new ListChange<ImageDto>.Upserted(ToDto(response.Upserted)),
            WatchCase.RemovedId => new ListChange<ImageDto>.Removed(response.RemovedId),
            _ => null,
        };
    }

    public static ImageDto ToDto(ImagesV1.Image image)
    {
        return new ImageDto(
            new ImageId(image.Id),
            [.. image.RepoTags],
            image.CreatedAt?.ToDateTimeOffset() ?? DateTimeOffset.UnixEpoch,
            ToLong(image.SizeBytes),
            image.Containers,
            image.RepoDigests.Count > 0);
    }

    public static long ToLong(ulong value)
    {
        return value > long.MaxValue ? long.MaxValue : (long)value;
    }

    /// <summary>Returns <see langword="null"/> for an event kind this client does not know.</summary>
    public static PullEvent? ToPullEvent(ImagesV1.PullImageResponse response)
    {
        return response.EventCase switch {
            PullCase.Layer => new PullEvent.Layer(
                response.Layer.LayerId,
                response.Layer.Status,
                (long)response.Layer.CurrentBytes,
                (long)response.Layer.TotalBytes),
            PullCase.Status => new PullEvent.StatusLine(response.Status),
            PullCase.Pulled => new PullEvent.Pulled(response.Pulled.Reference, response.Pulled.ImageId),
            _ => null,
        };
    }
}
