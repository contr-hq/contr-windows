using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Queries.GetNetworkMembers;

public sealed class GetNetworkMembersQueryHandler : IRequestHandler<GetNetworkMembersQuery, Result<NetworkMembersDto>>
{
    private readonly INetworksGateway _networks;

    public GetNetworkMembersQueryHandler(INetworksGateway networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        _networks = networks;
    }

    public async Task<Result<NetworkMembersDto>> Handle(
        GetNetworkMembersQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var endpoints = await _networks.GetEndpointsAsync(request.Id, cancellationToken);

        if (endpoints.IsFailure)
        {
            return Result.Failure<NetworkMembersDto>(endpoints.Error);
        }

        var containers = await _networks.ListContainersAsync(cancellationToken);

        return Result.Success(Members(
            request.Name, endpoints.Value, containers.IsSuccess ? containers.Value : []));
    }

    /// <summary>The containers that name the network, with the addresses the network gives the running ones.</summary>
    /// <remarks>The engine lists only running containers among a network's endpoints; containers list their
    /// networks whether they run or not.</remarks>
    public static NetworkMembersDto Members(
        string network, IReadOnlyList<NetworkEndpointDto> endpoints, IReadOnlyList<NetworkContainerDto> containers)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(containers);

        var addresses = endpoints
            .GroupBy(endpoint => endpoint.Container, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key, group => group.First().Address.Split('/')[0], StringComparer.Ordinal);
        bool Joined(NetworkContainerDto container) =>
            container.Networks.Contains(network, StringComparer.Ordinal) || addresses.ContainsKey(container.Name);

        var members = containers
            .Where(Joined)
            .Select(container => new NetworkMemberDto(
                container.Id,
                container.Name,
                container.IsRunning,
                container.IsRunning ? addresses.GetValueOrDefault(container.Name, string.Empty) : string.Empty))
            .ToList();
        members.AddRange(endpoints
            .Where(endpoint => !members.Any(member => member.Container == endpoint.Container))
            .Select(endpoint => new NetworkMemberDto(
                endpoint.Container, endpoint.Container, true, addresses[endpoint.Container])));

        return new NetworkMembersDto(
            [.. members.OrderBy(member => member.Container, StringComparer.OrdinalIgnoreCase)],
            [.. containers
                .Where(container => !Joined(container))
                .OrderBy(container => container.Name, StringComparer.OrdinalIgnoreCase)]);
    }
}
