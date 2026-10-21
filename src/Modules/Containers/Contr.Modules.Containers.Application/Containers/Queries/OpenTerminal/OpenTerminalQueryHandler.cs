using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Queries.OpenTerminal;

public sealed class OpenTerminalQueryHandler : IRequestHandler<OpenTerminalQuery, Result<ITerminalSession>>
{
    private readonly IContainersGateway _containers;

    public OpenTerminalQueryHandler(IContainersGateway containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        _containers = containers;
    }

    public Task<Result<ITerminalSession>> Handle(OpenTerminalQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _containers.OpenTerminalAsync(
            request.Id, request.Shell, request.User, request.Columns, request.Rows, cancellationToken);
    }
}
