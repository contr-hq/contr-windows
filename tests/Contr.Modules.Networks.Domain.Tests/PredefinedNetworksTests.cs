using Contr.Modules.Networks.Domain;
using Xunit;

namespace Contr.Modules.Networks.Domain.Tests;

public sealed class PredefinedNetworksTests
{
    [Theory]
    [InlineData("bridge", true)]
    [InlineData("host", true)]
    [InlineData("none", true)]
    [InlineData("shop_default", false)]
    [InlineData("Bridge", false)]
    public void IsPredefined_NamesTheEngineOwnNetworks(string name, bool expected)
    {
        Assert.Equal(expected, PredefinedNetworks.IsPredefined(name));
    }

    [Fact]
    public void NetworkId_ShortIsTheFirstTwelveCharacters()
    {
        Assert.Equal("e70000000000", new NetworkId("e700000000000000000000000000").Short);
    }
}
