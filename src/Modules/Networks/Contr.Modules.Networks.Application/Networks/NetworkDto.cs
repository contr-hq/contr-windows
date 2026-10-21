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
