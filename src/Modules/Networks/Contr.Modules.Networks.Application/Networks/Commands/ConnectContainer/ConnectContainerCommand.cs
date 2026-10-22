using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Commands.ConnectContainer;

/// <summary>Connects a container to a network.</summary>
/// <param name="Container">The container's ID or name.</param>
public sealed record ConnectContainerCommand(NetworkId Network, string Container) : IRequest<Result>;
