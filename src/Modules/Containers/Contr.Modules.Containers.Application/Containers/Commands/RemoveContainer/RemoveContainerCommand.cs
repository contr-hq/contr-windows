using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.RemoveContainer;

/// <summary>Deletes a container, killing it first when it runs.</summary>
/// <param name="State">The state the list last showed, which decides whether the engine is asked at all.</param>
public sealed record RemoveContainerCommand(ContainerId Id, ContainerState State) : IRequest<Result>;
