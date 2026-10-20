using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Queries.OpenTerminal;

/// <summary>Starts <paramref name="Shell"/> in a running container with a terminal of the given size.</summary>
/// <param name="Shell">Such as <c>/bin/sh</c>.</param>
/// <param name="User">Such as <c>root</c>; empty runs as the container's own user.</param>
public sealed record OpenTerminalQuery(ContainerId Id, string Shell, string User, int Columns, int Rows)
    : IRequest<Result<ITerminalSession>>;
