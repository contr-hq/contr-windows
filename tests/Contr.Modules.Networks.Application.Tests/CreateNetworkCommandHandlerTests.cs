using System.Threading;
using System.Threading.Tasks;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.Modules.Networks.Application.Networks.Commands.CreateNetwork;
using Contr.SharedKernel;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Networks.Application.Tests;

public sealed class CreateNetworkCommandHandlerTests
{
    private readonly INetworksGateway _gateway = Substitute.For<INetworksGateway>();

    [Theory]
    [InlineData("", "Networks.Name.Empty")]
    [InlineData("bridge", "Networks.Name.Predefined")]
    public async Task Create_RefusesANameTheEngineWouldRefuse(string name, string code)
    {
        var result = await new CreateNetworkCommandHandler(_gateway)
            .Handle(new CreateNetworkCommand(name), CancellationToken.None);

        Assert.Equal(code, result.Error.Code);
        await _gateway.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task Create_PassesTheTrimmedName()
    {
        _gateway.CreateAsync("shop", Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await new CreateNetworkCommandHandler(_gateway)
            .Handle(new CreateNetworkCommand(" shop "), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
