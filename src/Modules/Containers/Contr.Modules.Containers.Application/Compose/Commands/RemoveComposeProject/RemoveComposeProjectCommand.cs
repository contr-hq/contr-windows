using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Compose.Commands.RemoveComposeProject;

/// <summary>Forgets a project the user added; its containers stay.</summary>
public sealed record RemoveComposeProjectCommand(string Project) : IRequest<Result>;
