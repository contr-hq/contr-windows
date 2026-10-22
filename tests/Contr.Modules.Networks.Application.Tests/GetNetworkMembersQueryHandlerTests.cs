using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.Application.Networks.Queries.GetNetworkMembers;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Networks.Application.Tests;

public sealed class GetNetworkMembersQueryHandlerTests
{
    private readonly INetworksGateway _gateway = Substitute.For<INetworksGateway>();

    [Fact]
    public void Members_IncludeStoppedContainersAndTheAddressesOfRunningOnes()
    {
        NetworkEndpointDto[] endpoints = [new("api", "172.20.0.4/16"), new("db", "172.20.0.2/16")];
        NetworkContainerDto[] containers = [
            new("w1", "worker", false, ["shop_default"]),
            new("a1", "api", true, ["shop_default"]),
            new("d1", "db", true, ["shop_default", "other"]),
            new("m1", "migrate", false, []),
        ];

        var members = GetNetworkMembersQueryHandler.Members("shop_default", endpoints, containers);

        Assert.Equal(
            [("api", true, "172.20.0.4"), ("db", true, "172.20.0.2"), ("worker", false, "")],
            members.Members.Select(member => (member.Container, member.IsRunning, member.Address)));
        Assert.Equal(["migrate"], members.Others.Select(other => other.Name));
    }

    [Fact]
    public void Members_FromAnOlderDaemon_AreTheRunningEndpoints()
    {
        var members = GetNetworkMembersQueryHandler.Members(
            "shop_default", [new NetworkEndpointDto("api", "172.20.0.4/16")], [new("a1", "api", true, [])]);

        Assert.Equal(("a1", "api", true, "172.20.0.4"), Single(members));
    }

    [Fact]
    public async Task Handle_KeepsTheEndpointsWhenContainersCannotBeListed()
    {
        var id = new NetworkId("n1");
        _gateway
            .GetEndpointsAsync(id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<NetworkEndpointDto>>(
                [new NetworkEndpointDto("api", "172.20.0.4/16")]));
        _gateway
            .ListContainersAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<NetworkContainerDto>>(
                new Error("Daemon.Failed", "boom")));

        var result = await new GetNetworkMembersQueryHandler(_gateway)
            .Handle(new GetNetworkMembersQuery(id, "shop_default"), CancellationToken.None);

        Assert.Equal(("api", "api", true, "172.20.0.4"), Single(result.Value));
    }

    private static (string, string, bool, string) Single(NetworkMembersDto members)
    {
        var member = Assert.Single(members.Members);

        return (member.ContainerId, member.Container, member.IsRunning, member.Address);
    }
}
