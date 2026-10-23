using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Daemon.UI;
using Contr.Modules.Images.Application.Abstractions;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Domain;
using Contr.Sdk.Operations.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using ImagesV1 = Contr.Sdk.Images.V1;
using OperationState = Contr.UI.Contracts.Services.Daemon.OperationState;

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

        return _daemon.Offers()
            ? PullOperationAsync(request, cancellationToken)
            : _daemon.StreamAsync(
                (client, token) => client.Images.PullImage(request, cancellationToken: token),
                ImageMapping.ToPullEvent,
                error => new PullEvent.Failed(error),
                cancellationToken);
    }

    public async IAsyncEnumerable<PullEvent> FollowPullAsync(
        string operationId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(operationId);

        var pulled = false;
        var events = _daemon.FollowOperationAsync(
            operationId,
            ImageMapping.ToFollowed,
            error => new ImageMapping.Followed(new PullEvent.Failed(error), null),
            cancellationToken);

        await foreach (var followed in events.ConfigureAwait(false))
        {
            if (followed.Event is { } pullEvent)
            {
                pulled |= pullEvent is PullEvent.Pulled;

                yield return pullEvent;
            }

            if (followed.Finished is { } finished && Ending(finished, pulled) is { } last)
            {
                yield return last;
            }
        }
    }

    /// <summary>The last event of a pull that ended; absent when the daemon sent it already.</summary>
    /// <remarks>The daemon replays layers and status lines, not the last event of a pull that ended.</remarks>
    public static PullEvent? Ending(DaemonOperation finished, bool pulled)
    {
        ArgumentNullException.ThrowIfNull(finished);

        return finished.State switch {
            OperationState.Succeeded when pulled => null,
            OperationState.Succeeded => new PullEvent.Pulled(
                (finished.Work as OperationWork.PullImage)?.Reference ?? string.Empty, string.Empty),
            OperationState.Cancelled => new PullEvent.Cancelled(),
            _ => new PullEvent.Failed(DaemonOperations.Ended(finished)),
        };
    }

    private async IAsyncEnumerable<PullEvent> PullOperationAsync(
        ImagesV1.PullImageRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var started = await _daemon
            .StartOperationAsync(new OperationRequest { PullImage = request }, cancellationToken)
            .ConfigureAwait(false);

        if (started.IsFailure)
        {
            yield return new PullEvent.Failed(started.Error);
            yield break;
        }

        yield return new PullEvent.Started(started.Value.Id);

        await foreach (var pullEvent in FollowPullAsync(started.Value.Id, cancellationToken).ConfigureAwait(false))
        {
            yield return pullEvent;
        }
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
