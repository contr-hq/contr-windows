using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.RenameContainer;

/// <summary>Gives a container a new name; the engine refuses a name another container has.</summary>
/// <param name="Name">The name as typed; the handler validates it.</param>
public sealed record RenameContainerCommand(ContainerId Id, string Name) : IRequest<Result>;
