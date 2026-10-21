using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Compose;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Abstractions;

/// <summary>The engine's Compose projects, as the module's adapter reaches them through the daemon.</summary>
/// <remarks>Streams never throw for daemon failures; they end with a failed item instead.</remarks>
public interface IComposeGateway
{
    /// <summary>Every project, sorted by name, as a snapshot after every change, until cancelled or broken.</summary>
    IAsyncEnumerable<ListChange<ComposeProjectDto>> WatchProjectsAsync(CancellationToken cancellationToken);

    /// <summary>Remembers a project by its Compose files, later files overriding earlier ones.</summary>
    /// <returns>The project; a failure carrying Compose's message when it cannot read the files.</returns>
    Task<Result<ComposeProjectDto>> AddProjectAsync(
        IReadOnlyList<string> configFiles, CancellationToken cancellationToken);

    /// <summary>Forgets a project the user added; its containers stay.</summary>
    Task<Result> RemoveProjectAsync(string project, CancellationToken cancellationToken);

    /// <summary>Runs Compose on <paramref name="project"/>, streaming its output until it exits.</summary>
    /// <param name="services">The services to act on; empty acts on every one.</param>
    /// <param name="removeVolumes">With <see cref="ComposeAction.Down"/>, also removes the named volumes.</param>
    IAsyncEnumerable<ComposeActionEvent> RunActionAsync(
        string project,
        ComposeAction action,
        IReadOnlyList<string> services,
        bool removeVolumes,
        CancellationToken cancellationToken);

    /// <summary>The last <paramref name="tailLines"/> lines of every container of the project.</summary>
    /// <param name="follow">Then new output of the containers running when the stream opened.</param>
    IAsyncEnumerable<ComposeLogEvent> StreamLogsAsync(
        string project, int tailLines, bool follow, CancellationToken cancellationToken);
}
