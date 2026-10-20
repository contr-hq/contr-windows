using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.PruneContainers;

/// <summary>Deletes every stopped container.</summary>
public sealed record PruneContainersCommand : IRequest<Result<ContainersPruned>>;
