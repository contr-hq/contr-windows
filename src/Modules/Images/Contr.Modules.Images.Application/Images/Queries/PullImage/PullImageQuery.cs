using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Images.Application.Images.Queries.PullImage;

/// <summary>Pulls the image a user typed; an invalid reference fails before the engine is asked.</summary>
/// <param name="Platform">Such as <c>linux/arm64</c>; empty pulls for the engine's own platform.</param>
public sealed record PullImageQuery(string Reference, string Platform = "")
    : IRequest<Result<IAsyncEnumerable<PullEvent>>>;
