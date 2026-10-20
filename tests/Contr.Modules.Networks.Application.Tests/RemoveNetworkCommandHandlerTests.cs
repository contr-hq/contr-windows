using System.Threading;
using System.Threading.Tasks;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.Modules.Networks.Application.Networks.Commands.RemoveNetwork;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Networks.Application.Tests;

public sealed class RemoveNetworkCommandHandlerTests
{
    private readonly INetworksGateway _gateway = Substitute.For<INetworksGateway>();

    [Fact]
    public async Task Remove_RefusesAPredefinedNetworkWithoutAskingTheEngine()
    {
        var result = await new RemoveNetworkCommandHandler(_gateway)
            .Handle(new RemoveNetworkCommand(new NetworkId("e700"), "bridge"), CancellationToken.None);

        Assert.Equal(NetworksErrors.Predefined, result.Error);
        await _gateway.DidNotReceiveWithAnyArgs().RemoveAsync(default!, default);
    }

    [Fact]
    public async Task Remove_AsksTheEngineForAUserDefinedNetwork()
    {
        var id = new NetworkId("e800");
        _gateway.RemoveAsync(id, Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await new RemoveNetworkCommandHandler(_gateway)
            .Handle(new RemoveNetworkCommand(id, "shop_default"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
