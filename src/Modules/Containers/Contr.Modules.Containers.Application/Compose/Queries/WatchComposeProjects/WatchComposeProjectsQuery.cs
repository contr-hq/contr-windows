using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;

namespace Contr.Modules.Containers.Application.Compose.Queries.WatchComposeProjects;

/// <summary>Every Compose project, then the list again after every change.</summary>
public sealed record WatchComposeProjectsQuery : IRequest<IAsyncEnumerable<ListChange<ComposeProjectDto>>>;
