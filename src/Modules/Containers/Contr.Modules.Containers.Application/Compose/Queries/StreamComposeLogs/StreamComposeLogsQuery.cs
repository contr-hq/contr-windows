using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;

namespace Contr.Modules.Containers.Application.Compose.Queries.StreamComposeLogs;

/// <summary>The end of every log of a project's containers.</summary>
/// <param name="TailLines">The lines to send from the end of each container's log.</param>
/// <param name="Follow">Then new output of the containers running when the stream opened.</param>
public sealed record StreamComposeLogsQuery(string Project, int TailLines, bool Follow)
    : IRequest<IAsyncEnumerable<ComposeLogEvent>>;
