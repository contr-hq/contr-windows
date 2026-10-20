using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;

namespace Contr.Modules.Images.Application.Images.Queries.WatchImages;

/// <summary>Every image, then every change, until the request's cancellation token fires.</summary>
public sealed record WatchImagesQuery : IRequest<IAsyncEnumerable<ListChange<ImageDto>>>;
