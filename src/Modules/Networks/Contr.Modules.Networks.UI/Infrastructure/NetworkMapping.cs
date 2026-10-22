using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.Domain;
using ContainersV1 = Contr.Sdk.Containers.V1;
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

    /// <summary>A container by the name the engine lists it under, without the leading slash.</summary>
    public static NetworkContainerDto ToContainer(ContainersV1.Container container)
    {
        var name = container.Names.FirstOrDefault()?.TrimStart('/') ?? container.Id;

        return new NetworkContainerDto(
            container.Id, name, container.State == ContainersV1.ContainerState.Running, [.. container.Networks]);
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
            project,
            network.Subnets.Count > 0 ? network.Subnets[0].Gateway : string.Empty);
    }

    /// <summary>
    /// Reads the <c>Containers</c> map of the engine's network inspection document (Docker Engine API), which lists
    /// the running containers only.
    /// </summary>
    public static IReadOnlyList<NetworkEndpointDto> ToEndpoints(string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);

            if (!document.RootElement.TryGetProperty("Containers", out var containers)
                || containers.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return [.. containers
                .EnumerateObject()
                .Select(container => new NetworkEndpointDto(
                    Text(container.Value, "Name"),
                    Text(container.Value, "IPv4Address")))
                .OrderBy(endpoint => endpoint.Container, StringComparer.OrdinalIgnoreCase)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}
