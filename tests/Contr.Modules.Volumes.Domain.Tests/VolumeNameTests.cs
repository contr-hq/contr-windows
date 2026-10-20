using Contr.Modules.Volumes.Domain;
using Xunit;

namespace Contr.Modules.Volumes.Domain.Tests;

public sealed class VolumeNameTests
{
    [Theory]
    [InlineData("pgdata")]
    [InlineData("shop_pgdata")]
    [InlineData("cache.v2")]
    [InlineData("A-1")]
    public void Parse_AcceptsTheEngineRule(string text)
    {
        var name = VolumeName.Parse(text);

        Assert.True(name.IsSuccess);
        Assert.Equal(text, name.Value.Value);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("-data")]
    [InlineData("my volume")]
    [InlineData("data/2")]
    public void Parse_RefusesAnInvalidName(string text)
    {
        var name = VolumeName.Parse(text);

        Assert.True(name.IsFailure);
        Assert.Equal("Volumes.Name.Invalid", name.Error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Parse_RequiresAName(string? text)
    {
        Assert.Equal(VolumesErrors.NameRequired, VolumeName.Parse(text).Error);
    }

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        Assert.Equal("pgdata", VolumeName.Parse(" pgdata ").Value.Value);
    }
}
