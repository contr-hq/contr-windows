using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Daemon.UI;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;
using ContainersV1 = Contr.Sdk.Containers.V1;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;

namespace Contr.Modules.Containers.UI.Infrastructure;

/// <summary>The <see cref="IContainersGateway"/> over the daemon's ContainersService.</summary>
/// <remarks>
/// The SDK namespace goes by an alias: inside <c>Contr.Modules.Containers</c> the identifier
/// <c>Containers</c> binds to the module's namespace before any using directive.
/// </remarks>
public sealed class ContainersGateway : IContainersGateway
{
    private readonly IDaemonClient _daemon;

    public ContainersGateway(IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public IAsyncEnumerable<ListChange<ContainerDto>> WatchAsync(CancellationToken cancellationToken)
    {
        return _daemon.StreamAsync(
            (client, token) => client.Containers.WatchContainers(
                new ContainersV1.WatchContainersRequest(), cancellationToken: token),
            ContainerMapping.ToChange,
            error => new ListChange<ContainerDto>.Failed(error),
            cancellationToken);
    }

    public Task<Result> StartAsync(ContainerId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var request = new ContainersV1.StartContainerRequest { Id = id.Value };

        return _daemon.RunAsync(
            (client, token) => client.Containers
                .StartContainerAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public Task<Result> StopAsync(ContainerId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var request = new ContainersV1.StopContainerRequest { Id = id.Value };

        return _daemon.RunAsync(
            (client, token) => client.Containers
                .StopContainerAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public Task<Result> RestartAsync(ContainerId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var request = new ContainersV1.RestartContainerRequest { Id = id.Value };

        return _daemon.RunAsync(
            (client, token) => client.Containers
                .RestartContainerAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public Task<Result> RemoveAsync(ContainerId id, bool force, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var request = new ContainersV1.RemoveContainerRequest { Id = id.Value, Force = force };

        return _daemon.RunAsync(
            (client, token) => client.Containers
                .RemoveContainerAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public IAsyncEnumerable<ContainerLogEvent> StreamLogsAsync(
        ContainerId id, LogRange range, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(range);

        var request = new ContainersV1.StreamLogsRequest {
            Id = id.Value,
            Follow = true,
            Timestamps = range.Timestamps,
        };

        if (range.TailLines is { } tailLines)
        {
            request.TailLines = (uint)Math.Max(0, tailLines);
        }

        if (range.Since is { } since)
        {
            request.Since = Timestamp.FromDateTimeOffset(since);
        }

        var decoder = new LogDecoder();

        return _daemon.StreamAsync(
            (client, token) => client.Containers.StreamLogs(request, cancellationToken: token),
            decoder.Decode,
            error => new ContainerLogEvent.Failed(error),
            cancellationToken);
    }

    public Task<Result<ITerminalSession>> OpenTerminalAsync(
        ContainerId id, string shell, string user, int columns, int rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(shell);
        ArgumentNullException.ThrowIfNull(user);

        var start = new ContainersV1.ExecStart {
            ContainerId = id.Value,
            Tty = true,
            AttachStdin = true,
            User = user,
            TerminalSize = Size(columns, rows),
        };
        start.Command.Add(shell);

        return TerminalSession.OpenAsync(_daemon, start, cancellationToken);
    }

    internal static ContainersV1.TerminalSize Size(int columns, int rows)
    {
        return new ContainersV1.TerminalSize { Columns = (uint)Math.Max(1, columns), Rows = (uint)Math.Max(1, rows) };
    }

    public IAsyncEnumerable<ContainerStatsEvent> StreamStatsAsync(ContainerId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var request = new ContainersV1.StreamStatsRequest { Id = id.Value };

        return _daemon.StreamAsync(
            (client, token) => client.Containers.StreamStats(request, cancellationToken: token),
            ContainerMapping.ToStatsEvent,
            error => new ContainerStatsEvent.Failed(error),
            cancellationToken);
    }

    public async Task<Result<ContainerDetailsDto>> InspectAsync(ContainerId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var request = new ContainersV1.InspectContainerRequest { Id = id.Value };
        var result = await _daemon
            .CallAsync(
                (client, token) => client.Containers
                    .InspectContainerAsync(request, cancellationToken: token)
                    .ResponseAsync,
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Result.Success(ContainerMapping.ToDetails(id, result.Value))
            : Result.Failure<ContainerDetailsDto>(result.Error);
    }
}
