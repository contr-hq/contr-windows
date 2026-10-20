using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Domain;

namespace Contr.Modules.Containers.Application.Containers.Queries.StreamContainerLogs;

/// <summary>A container's log, then its new output until the container stops.</summary>
public sealed record StreamContainerLogsQuery(ContainerId Id, LogRange Range)
    : IRequest<IAsyncEnumerable<ContainerLogEvent>>;
