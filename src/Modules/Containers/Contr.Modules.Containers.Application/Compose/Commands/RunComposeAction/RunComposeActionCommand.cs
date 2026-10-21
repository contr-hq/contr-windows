using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;

namespace Contr.Modules.Containers.Application.Compose.Commands.RunComposeAction;

/// <summary>Runs Compose on a project; the stream carries its output, then its exit code.</summary>
/// <param name="Services">The services to act on; empty acts on every one, and Down always does.</param>
/// <param name="RemoveVolumes">With <see cref="ComposeAction.Down"/>, also removes the project's named volumes.
/// </param>
public sealed record RunComposeActionCommand(
    string Project, ComposeAction Action, IReadOnlyList<string> Services, bool RemoveVolumes = false)
    : IRequest<IAsyncEnumerable<ComposeActionEvent>>;
