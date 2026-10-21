using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Daemon.UI;
using Contr.Modules.Images.Application.Abstractions;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Domain;
using Contr.SharedKernel;
using ImagesV1 = Contr.Sdk.Images.V1;

namespace Contr.Modules.Images.UI.Infrastructure;

/// <summary>The <see cref="IImagesGateway"/> over the daemon's ImagesService.</summary>
public sealed class ImagesGateway : IImagesGateway
{
    private readonly IDaemonClient _daemon;

    public ImagesGateway(IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public IAsyncEnumerable<ListChange<ImageDto>> WatchAsync(CancellationToken cancellationToken)
    {
        return _daemon.StreamAsync(
            (client, token) => client.Images.WatchImages(new ImagesV1.WatchImagesRequest(), cancellationToken: token),
            ImageMapping.ToChange,
            error => new ListChange<ImageDto>.Failed(error),
            cancellationToken);
    }

    public IAsyncEnumerable<PullEvent> PullAsync(
        ImageReference reference, string platform, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(platform);

        var request = new ImagesV1.PullImageRequest { Reference = reference.Canonical, Platform = platform };

        return _daemon.StreamAsync(
            (client, token) => client.Images.PullImage(request, cancellationToken: token),
            ImageMapping.ToPullEvent,
            error => new PullEvent.Failed(error),
            cancellationToken);
    }

    public Task<Result> RemoveAsync(ImageId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var request = new ImagesV1.RemoveImageRequest { Reference = id.Value };

        return _daemon.RunAsync(
            (client, token) => client.Images
                .RemoveImageAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public async Task<Result<PruneResult>> PruneAsync(bool all, CancellationToken cancellationToken)
    {
        var request = new ImagesV1.PruneImagesRequest { All = all };
        var result = await _daemon
            .CallAsync(
                (client, token) => client.Images
                    .PruneImagesAsync(request, cancellationToken: token)
                    .ResponseAsync,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result.Failure<PruneResult>(result.Error);
        }

        var response = result.Value;

        return Result.Success(new PruneResult(response.Deleted.Count, ImageMapping.ToLong(response.ReclaimedBytes)));
    }
}
