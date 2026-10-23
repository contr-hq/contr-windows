using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;

namespace Contr.Modules.Images.Application.Images.Queries.FollowPull;

/// <summary>Follows a pull already running in the daemon, such as one started before the page closed.</summary>
public sealed record FollowPullQuery(string OperationId) : IRequest<IAsyncEnumerable<PullEvent>>;
