using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;

namespace Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;

/// <summary>Every container, then every change, until the request's cancellation token fires.</summary>
public sealed record WatchContainersQuery : IRequest<IAsyncEnumerable<ListChange<ContainerDto>>>;
