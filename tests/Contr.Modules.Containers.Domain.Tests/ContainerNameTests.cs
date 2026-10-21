using Contr.Modules.Containers.Domain;
using Xunit;

namespace Contr.Modules.Containers.Domain.Tests;

public sealed class ContainerNameTests
{
    [Theory]
    [InlineData("web", "web")]
    [InlineData(" shop-api_1.2 ", "shop-api_1.2")]
    public void Parse_TrimsAValidName(string text, string expected)
    {
        Assert.Equal(expected, ContainerName.Parse(text).Value.Value);
    }

    [Fact]
    public void Parse_RequiresAName()
    {
        Assert.Equal(ContainersErrors.NameRequired, ContainerName.Parse("  ").Error);
    }

    [Theory]
    [InlineData("-web")]
    [InlineData("my web")]
    [InlineData("w")]
    public void Parse_RefusesWhatTheEngineWould(string text)
    {
        Assert.Equal("Containers.Name.Invalid", ContainerName.Parse(text).Error.Code);
    }
}
