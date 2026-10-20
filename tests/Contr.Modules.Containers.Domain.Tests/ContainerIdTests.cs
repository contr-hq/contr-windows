using System;
using Contr.Modules.Containers.Domain;
using Xunit;

namespace Contr.Modules.Containers.Domain.Tests;

public sealed class ContainerIdTests
{
    [Fact]
    public void Short_IsTheFirstTwelveCharacters()
    {
        var id = new ContainerId("c0ffee0100000000000000000000000000000000000000000000000000000000");

        Assert.Equal("c0ffee010000", id.Short);
    }

    [Fact]
    public void Short_KeepsAnIdShorterThanTwelve()
    {
        Assert.Equal("abc", new ContainerId("abc").Short);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_RefusesAnEmptyId(string value)
    {
        Assert.Throws<ArgumentException>(() => new ContainerId(value));
    }
}
