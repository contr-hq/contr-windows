using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Images.Application.Abstractions;

namespace Contr.Modules.Images.Application.Images.Queries.WatchImages;

public sealed class WatchImagesQueryHandler
    : IRequestHandler<WatchImagesQuery, IAsyncEnumerable<ListChange<ImageDto>>>
{
    private readonly IImagesGateway _images;

    public WatchImagesQueryHandler(IImagesGateway images)
    {
        ArgumentNullException.ThrowIfNull(images);
        _images = images;
    }

    public Task<IAsyncEnumerable<ListChange<ImageDto>>> Handle(
        WatchImagesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_images.WatchAsync(cancellationToken));
    }
}
