using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Daemon.UI;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Application.Compose;
using Contr.SharedKernel;
using ComposeV1 = Contr.Sdk.Compose.V1;

namespace Contr.Modules.Containers.UI.Infrastructure;

/// <summary>The <see cref="IComposeGateway"/> over the daemon's ComposeService.</summary>
public sealed class ComposeGateway : IComposeGateway
{
    private readonly IDaemonClient _daemon;

    public ComposeGateway(IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public IAsyncEnumerable<ListChange<ComposeProjectDto>> WatchProjectsAsync(CancellationToken cancellationToken)
    {
        return _daemon.StreamAsync(
            (client, token) => client.Compose.WatchProjects(
                new ComposeV1.WatchProjectsRequest(), cancellationToken: token),
            ComposeMapping.ToChange,
            error => new ListChange<ComposeProjectDto>.Failed(error),
            cancellationToken);
    }

    public async Task<Result<ComposeProjectDto>> AddProjectAsync(
        IReadOnlyList<string> configFiles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configFiles);

        var request = new ComposeV1.AddProjectRequest { ConfigFiles = { configFiles } };
        var result = await _daemon
            .CallAsync(
                (client, token) => client.Compose
                    .AddProjectAsync(request, cancellationToken: token)
                    .ResponseAsync,
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Result.Success(ComposeMapping.ToDto(result.Value.Project))
            : Result.Failure<ComposeProjectDto>(result.Error);
    }

    public Task<Result> RemoveProjectAsync(string project, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(project);

        var request = new ComposeV1.RemoveProjectRequest { Name = project };

        return _daemon.RunAsync(
            (client, token) => client.Compose
                .RemoveProjectAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public IAsyncEnumerable<ComposeActionEvent> RunActionAsync(
        string project,
        ComposeAction action,
        IReadOnlyList<string> services,
        bool removeVolumes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(project);
        ArgumentNullException.ThrowIfNull(services);

        var request = new ComposeV1.RunActionRequest {
            Project = project,
            Action = ComposeMapping.ToWire(action),
            Services = { services },
            RemoveVolumes = removeVolumes,
        };

        return _daemon.StreamAsync(
            (client, token) => client.Compose.RunAction(request, cancellationToken: token),
            ComposeMapping.ToEvent,
            error => new ComposeActionEvent.Failed(error),
            cancellationToken);
    }

    public IAsyncEnumerable<ComposeLogEvent> StreamLogsAsync(
        string project, int tailLines, bool follow, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(project);

        var request = new ComposeV1.StreamLogsRequest {
            Project = project,
            Follow = follow,
            TailLines = (uint)Math.Max(0, tailLines),
        };
        var decoder = new ComposeLogDecoder();

        return _daemon.StreamAsync(
            (client, token) => client.Compose.StreamLogs(request, cancellationToken: token),
            decoder.Decode,
            error => new ComposeLogEvent.Failed(error),
            cancellationToken);
    }
}
