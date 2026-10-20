using System;
using System.Collections.Generic;
using Contr.Modules.Networks.Domain;

namespace Contr.Modules.Networks.Application.Networks;

/// <summary>A network as the list shows it.</summary>
/// <param name="Subnets">Address ranges in CIDR notation.</param>
/// <param name="ComposeProject">The Compose project that created the network; empty for others.</param>
public sealed record NetworkDto(
    NetworkId Id,
    string Name,
    string Driver,
    string Scope,
    bool Internal,
    IReadOnlyList<string> Subnets,
    DateTimeOffset CreatedAt,
    string ComposeProject);
