using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;

namespace Contr.Modules.Containers.Application.Compose.Queries.FollowComposeAction;

/// <summary>Follows a Compose action already running in the daemon, or replays one that ended.</summary>
public sealed record FollowComposeActionQuery(string OperationId) : IRequest<IAsyncEnumerable<ComposeActionEvent>>;
