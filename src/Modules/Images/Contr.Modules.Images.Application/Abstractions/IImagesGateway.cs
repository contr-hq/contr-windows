using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Images.Application.Abstractions;

/// <summary>The engine's image store, as the module's adapter reaches it through the daemon.</summary>
/// <remarks>Streams never throw for daemon failures; they end with a failed item instead.</remarks>
public interface IImagesGateway
{
    /// <summary>The list, then every change to it, until cancelled or broken.</summary>
    IAsyncEnumerable<ListChange<ImageDto>> WatchAsync(CancellationToken cancellationToken);

    /// <summary>Pulls the image; cancelling the enumeration cancels the pull.</summary>
    IAsyncEnumerable<PullEvent> PullAsync(ImageReference reference, CancellationToken cancellationToken);

    /// <summary>Removes one reference to the image and deletes it once nothing references it.</summary>
    Task<Result> RemoveAsync(ImageId id, CancellationToken cancellationToken);
}
