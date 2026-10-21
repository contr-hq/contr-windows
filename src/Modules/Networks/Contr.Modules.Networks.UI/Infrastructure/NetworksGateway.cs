using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Daemon.UI;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;
using NetworksV1 = Contr.Sdk.Networks.V1;

namespace Contr.Modules.Networks.UI.Infrastructure;

/// <summary>The <see cref="INetworksGateway"/> over the daemon's NetworksService.</summary>
public sealed class NetworksGateway : INetworksGateway
{
    private readonly IDaemonClient _daemon;

    public NetworksGateway(IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public IAsyncEnumerable<ListChange<NetworkDto>> WatchAsync(CancellationToken cancellationToken)
    {
        return _daemon.StreamAsync(
            (client, token) => client.Networks.WatchNetworks(
                new NetworksV1.WatchNetworksRequest(), cancellationToken: token),
            NetworkMapping.ToChange,
            error => new ListChange<NetworkDto>.Failed(error),
            cancellationToken);
    }

    public Task<Result> RemoveAsync(NetworkId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var request = new NetworksV1.RemoveNetworkRequest { Id = id.Value };

        return _daemon.RunAsync(
            (client, token) => client.Networks
                .RemoveNetworkAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public Task<Result> CreateAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        var request = new NetworksV1.CreateNetworkRequest { Name = name };

        return _daemon.RunAsync(
            (client, token) => client.Networks
                .CreateNetworkAsync(request, cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<NetworkEndpointDto>>> GetEndpointsAsync(
        NetworkId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var request = new NetworksV1.InspectNetworkRequest { Id = id.Value };
        var result = await _daemon
            .CallAsync(
                (client, token) => client.Networks
                    .InspectNetworkAsync(request, cancellationToken: token)
                    .ResponseAsync,
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Result.Success(NetworkMapping.ToEndpoints(result.Value.RawJson))
            : Result.Failure<IReadOnlyList<NetworkEndpointDto>>(result.Error);
    }

    public async Task<Result<int>> PruneAsync(CancellationToken cancellationToken)
    {
        var result = await _daemon
            .CallAsync(
                (client, token) => client.Networks
                    .PruneNetworksAsync(new NetworksV1.PruneNetworksRequest(), cancellationToken: token)
                    .ResponseAsync,
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Result.Success(result.Value.Deleted.Count) : Result.Failure<int>(result.Error);
    }
}
