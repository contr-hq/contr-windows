using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.RestartContainer;

/// <summary>Stops a container if it runs, then starts it.</summary>
/// <param name="State">The state the list last showed, which decides whether the engine is asked at all.</param>
public sealed record RestartContainerCommand(ContainerId Id, ContainerState State) : IRequest<Result>;
