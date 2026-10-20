using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Images.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Images.Application.Images.Commands.RemoveImage;

public sealed class RemoveImageCommandHandler : IRequestHandler<RemoveImageCommand, Result>
{
    private readonly IImagesGateway _images;

    public RemoveImageCommandHandler(IImagesGateway images)
    {
        ArgumentNullException.ThrowIfNull(images);
        _images = images;
    }

    public Task<Result> Handle(RemoveImageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _images.RemoveAsync(request.Id, cancellationToken);
    }
}
