using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Domain;

namespace Contr.Modules.Containers.Application.Containers.Queries.StreamContainerStats;

/// <summary>A running container's resource usage, sampled until it stops.</summary>
public sealed record StreamContainerStatsQuery(ContainerId Id) : IRequest<IAsyncEnumerable<ContainerStatsEvent>>;
