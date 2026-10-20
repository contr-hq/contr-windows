using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.StopContainer;

/// <summary>Stops a running container, killing it when its stop timeout expires.</summary>
/// <param name="State">The state the list last showed, which decides whether the engine is asked at all.</param>
public sealed record StopContainerCommand(ContainerId Id, ContainerState State) : IRequest<Result>;
