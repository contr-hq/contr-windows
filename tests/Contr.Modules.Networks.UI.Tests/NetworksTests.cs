using System;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.Application.Networks.Commands.RemoveNetwork;
using Contr.Modules.Networks.Domain;
using Contr.Modules.Networks.UI.Infrastructure;
using Contr.Modules.Networks.UI.ViewModels;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using NSubstitute;
using Xunit;
using NetworksV1 = Contr.Sdk.Networks.V1;

namespace Contr.Modules.Networks.UI.Tests;

public sealed class NetworksTests
{
    [Fact]
    public void Row_DisablesRemovalOfTheEngineNetworks()
    {
        Assert.False(Row("bridge").CanRemove);
        Assert.True(Row("shop_default").CanRemove);
    }

    [Fact]
    public void Compare_PutsTheEngineNetworksLast()
    {
        Assert.True(NetworkRow.Compare(Row("app"), Row("none")) < 0);
        Assert.True(NetworkRow.Compare(Row("app"), Row("shop_default")) < 0);
    }

    [Fact]
    public void Mapping_ReadsTheConnectedContainers()
    {
        const string inspection = """
            {
              "Name": "shop_default",
              "Containers": {
                "c1": { "Name": "shop-web-1", "IPv4Address": "172.18.0.3/16" },
                "c2": { "Name": "shop-db-1", "IPv4Address": "172.18.0.2/16" }
              }
            }
            """;

        var endpoints = NetworkMapping.ToEndpoints(inspection);

        Assert.Equal(
            [
                new NetworkEndpointDto("shop-db-1", "172.18.0.2/16"),
                new NetworkEndpointDto("shop-web-1", "172.18.0.3/16"),
            ],
            endpoints);
    }

    [Fact]
    public void Mapping_ReadsNoContainersFromAnUnexpectedDocument()
    {
        Assert.Empty(NetworkMapping.ToEndpoints("not json"));
        Assert.Empty(NetworkMapping.ToEndpoints("{}"));
    }

    [Fact]
    public void Mapping_ReadsScopeSubnetsAndProject()
    {
        var network = new NetworksV1.Network {
            Id = "e800",
            Name = "shop_default",
            Driver = "bridge",
            Scope = NetworksV1.NetworkScope.Local,
        };
        network.Subnets.Add(new NetworksV1.Subnet { Cidr = "172.18.0.0/16", Gateway = "172.18.0.1" });
        network.Labels.Add("com.docker.compose.project", "shop");

        var dto = NetworkMapping.ToDto(network);

        Assert.Equal("local", dto.Scope);
        Assert.Equal(["172.18.0.0/16"], dto.Subnets);
        Assert.Equal("shop", dto.ComposeProject);
    }

    [Fact]
    public async Task Remove_DoesNothingWhenTheUserDeclines()
    {
        var sender = Substitute.For<ISender>();
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ShowConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        var daemon = Substitute.For<IDaemonConnection>();
        daemon.Status.Returns(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty));

        await new NetworksViewModel(sender, dialogs, Substitute.For<INotificationService>(), daemon)
            .RemoveAsync(Row("shop_default"));

        await sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<RemoveNetworkCommand>(), default);
    }

    private static NetworkRow Row(string name)
    {
        return NetworkRow.From(new NetworkDto(
            new NetworkId(name + "-id"), name, "bridge", "local", false, [], DateTimeOffset.UnixEpoch, string.Empty));
    }
}
