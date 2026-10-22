using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Queries.GetNetworkMembers;

/// <summary>Every container connected to a network, running or stopped, and the containers that are not.</summary>
/// <param name="Name">The network's name, which containers list their networks by.</param>
public sealed record GetNetworkMembersQuery(NetworkId Id, string Name) : IRequest<Result<NetworkMembersDto>>;
