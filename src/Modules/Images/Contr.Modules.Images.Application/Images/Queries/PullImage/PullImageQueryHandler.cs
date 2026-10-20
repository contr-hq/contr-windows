using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Images.Application.Abstractions;
using Contr.Modules.Images.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Images.Application.Images.Queries.PullImage;

public sealed class PullImageQueryHandler : IRequestHandler<PullImageQuery, Result<IAsyncEnumerable<PullEvent>>>
{
    private readonly IImagesGateway _images;

    public PullImageQueryHandler(IImagesGateway images)
    {
        ArgumentNullException.ThrowIfNull(images);
        _images = images;
    }

    public Task<Result<IAsyncEnumerable<PullEvent>>> Handle(
        PullImageQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reference = ImageReference.Parse(request.Reference);
        var pull = reference.IsSuccess
            ? Result.Success(_images.PullAsync(reference.Value, request.Platform, cancellationToken))
            : Result.Failure<IAsyncEnumerable<PullEvent>>(reference.Error);

        return Task.FromResult(pull);
    }
}
