using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Commands.RemoveNetwork;

/// <summary>Deletes a user-defined network.</summary>
/// <param name="Name">The network's name, which decides whether it is one the engine owns.</param>
public sealed record RemoveNetworkCommand(NetworkId Id, string Name) : IRequest<Result>;
