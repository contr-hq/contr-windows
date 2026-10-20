using System;
using System.Linq;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.Domain;
using NetworksV1 = Contr.Sdk.Networks.V1;
using WatchCase = Contr.Sdk.Networks.V1.WatchNetworksResponse.EventOneofCase;

namespace Contr.Modules.Networks.UI.Infrastructure;

/// <summary>Maps the daemon's network messages to the application's DTOs.</summary>
internal static class NetworkMapping
{
    /// <summary>The label Docker Compose puts on every network it creates.</summary>
    private const string _composeProjectLabel = "com.docker.compose.project";

    /// <summary>Returns <see langword="null"/> for an event kind this client does not know.</summary>
    public static ListChange<NetworkDto>? ToChange(NetworksV1.WatchNetworksResponse response)
    {
        return response.EventCase switch {
            WatchCase.Snapshot => new ListChange<NetworkDto>.Snapshot([.. response.Snapshot.Networks.Select(ToDto)]),
            WatchCase.Upserted => new ListChange<NetworkDto>.Upserted(ToDto(response.Upserted)),
            WatchCase.RemovedId => new ListChange<NetworkDto>.Removed(response.RemovedId),
            _ => null,
        };
    }

    public static NetworkDto ToDto(NetworksV1.Network network)
    {
        var project = network.Labels.TryGetValue(_composeProjectLabel, out var label) ? label : string.Empty;
        var scope = network.Scope switch {
            NetworksV1.NetworkScope.Global => "global",
            NetworksV1.NetworkScope.Swarm => "swarm",
            _ => "local",
        };

        return new NetworkDto(
            new NetworkId(network.Id),
            network.Name,
            network.Driver,
            scope,
            network.Internal,
            [.. network.Subnets.Select(subnet => subnet.Cidr)],
            network.CreatedAt?.ToDateTimeOffset() ?? DateTimeOffset.UnixEpoch,
            project);
    }
}
