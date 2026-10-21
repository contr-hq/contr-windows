using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Commands.CreateContainer;

/// <summary>Creates a container from a local image and starts it.</summary>
public sealed record CreateContainerCommand(ContainerSpec Spec) : IRequest<Result<CreatedContainer>>;
