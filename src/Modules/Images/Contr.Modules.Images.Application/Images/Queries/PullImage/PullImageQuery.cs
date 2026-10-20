using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Images.Application.Images.Queries.PullImage;

/// <summary>Pulls the image a user typed; an invalid reference fails before the engine is asked.</summary>
public sealed record PullImageQuery(string Reference) : IRequest<Result<IAsyncEnumerable<PullEvent>>>;
