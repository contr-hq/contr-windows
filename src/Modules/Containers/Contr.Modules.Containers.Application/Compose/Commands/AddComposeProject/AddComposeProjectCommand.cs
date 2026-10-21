using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Compose.Commands.AddComposeProject;

/// <summary>Remembers a project by its Compose files, so it stays listed while it has no containers.</summary>
/// <param name="ConfigFiles">Absolute paths; later files override earlier ones.</param>
public sealed record AddComposeProjectCommand(IReadOnlyList<string> ConfigFiles) : IRequest<Result<ComposeProjectDto>>;
