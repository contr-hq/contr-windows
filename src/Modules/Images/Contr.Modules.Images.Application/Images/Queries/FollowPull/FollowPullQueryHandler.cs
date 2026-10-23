using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Images.Application.Abstractions;

namespace Contr.Modules.Images.Application.Images.Queries.FollowPull;

public sealed class FollowPullQueryHandler : IRequestHandler<FollowPullQuery, IAsyncEnumerable<PullEvent>>
{
    private readonly IImagesGateway _images;

    public FollowPullQueryHandler(IImagesGateway images)
    {
        ArgumentNullException.ThrowIfNull(images);
        _images = images;
    }

    public Task<IAsyncEnumerable<PullEvent>> Handle(FollowPullQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(_images.FollowPullAsync(request.OperationId, cancellationToken));
    }
}
