using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Images.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Images.Application.Images.Commands.PruneImages;

public sealed class PruneImagesCommandHandler : IRequestHandler<PruneImagesCommand, Result<PruneResult>>
{
    private readonly IImagesGateway _images;

    public PruneImagesCommandHandler(IImagesGateway images)
    {
        ArgumentNullException.ThrowIfNull(images);
        _images = images;
    }

    public Task<Result<PruneResult>> Handle(PruneImagesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _images.PruneAsync(request.All, cancellationToken);
    }
}
