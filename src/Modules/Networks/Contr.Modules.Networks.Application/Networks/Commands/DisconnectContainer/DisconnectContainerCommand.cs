using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Commands.DisconnectContainer;

/// <summary>Disconnects a container from a network.</summary>
/// <param name="Container">The container's ID or name.</param>
public sealed record DisconnectContainerCommand(NetworkId Network, string Container) : IRequest<Result>;
