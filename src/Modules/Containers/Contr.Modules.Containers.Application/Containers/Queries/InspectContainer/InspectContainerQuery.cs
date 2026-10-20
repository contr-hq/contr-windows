using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers.Queries.InspectContainer;

public sealed record InspectContainerQuery(ContainerId Id) : IRequest<Result<ContainerDetailsDto>>;
