using System;
using System.Collections.Generic;
using Contr.Modules.Networks.Domain;

namespace Contr.Modules.Networks.Application.Networks;

/// <summary>A network as the list shows it.</summary>
/// <param name="Subnets">Address ranges in CIDR notation.</param>
/// <param name="ComposeProject">The Compose project that created the network; empty for others.</param>
/// <param name="Gateway">The first subnet's gateway; empty when the engine chose none.</param>
public sealed record NetworkDto(
    NetworkId Id,
    string Name,
    string Driver,
    string Scope,
    bool Internal,
    IReadOnlyList<string> Subnets,
    DateTimeOffset CreatedAt,
    string ComposeProject,
    string Gateway = "");

/// <summary>A container connected to a network.</summary>
/// <param name="Address">Its IPv4 address in CIDR notation, such as <c>172.20.0.4/16</c>; empty without one.</param>
public sealed record NetworkEndpointDto(string Container, string Address);

/// <summary>A container and the networks it is connected to, running or not.</summary>
/// <param name="Networks">The networks' names; empty from a daemon older than API revision 20.</param>
public sealed record NetworkContainerDto(string Id, string Name, bool IsRunning, IReadOnlyList<string> Networks);

/// <summary>A network's containers, and the others that could connect to it.</summary>
/// <param name="Members">Every container connected to the network, sorted by name.</param>
/// <param name="Others">The containers not connected to it, sorted by name.</param>
public sealed record NetworkMembersDto(
    IReadOnlyList<NetworkMemberDto> Members, IReadOnlyList<NetworkContainerDto> Others);

/// <summary>A container connected to a network.</summary>
/// <param name="Address">Its IPv4 address, such as <c>172.20.0.4</c>; empty while it is stopped.</param>
public sealed record NetworkMemberDto(string ContainerId, string Container, bool IsRunning, string Address);
