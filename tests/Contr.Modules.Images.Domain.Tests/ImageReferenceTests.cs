using Contr.Modules.Images.Domain;
using Xunit;

namespace Contr.Modules.Images.Domain.Tests;

public sealed class ImageReferenceTests
{
    private const string _digest = "sha256:d100000000000000000000000000000000000000000000000000000000000000";

    [Theory]
    [InlineData("nginx", "nginx", null)]
    [InlineData("nginx:1.29", "nginx", "1.29")]
    [InlineData("library/nginx:latest", "library/nginx", "latest")]
    [InlineData("ghcr.io/contr-hq/contr:v0.1.0", "ghcr.io/contr-hq/contr", "v0.1.0")]
    [InlineData("localhost:5000/team/app:Build_7", "localhost:5000/team/app", "Build_7")]
    [InlineData("my_org__x/app-name", "my_org__x/app-name", null)]
    public void Parse_SplitsNameAndTag(string text, string name, string? tag)
    {
        var reference = ImageReference.Parse(text);

        Assert.True(reference.IsSuccess);
        Assert.Equal(name, reference.Value.Name);
        Assert.Equal(tag, reference.Value.Tag);
    }

    [Fact]
    public void Parse_ReadsADigest()
    {
        var reference = ImageReference.Parse($"nginx@{_digest}");

        Assert.True(reference.IsSuccess);
        Assert.Equal(_digest, reference.Value.Digest);
        Assert.Equal($"nginx@{_digest}", reference.Value.Canonical);
    }

    [Theory]
    [InlineData("Nginx")]
    [InlineData("nginx:")]
    [InlineData("nginx:-bad")]
    [InlineData("a//b")]
    [InlineData("nginx latest")]
    [InlineData("nginx@sha256:abc")]
    [InlineData("-nginx")]
    public void Parse_RefusesAnInvalidReference(string text)
    {
        var reference = ImageReference.Parse(text);

        Assert.True(reference.IsFailure);
        Assert.Equal("Images.Reference.Invalid", reference.Error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_RequiresAReference(string? text)
    {
        Assert.Equal(ImagesErrors.ReferenceRequired, ImageReference.Parse(text).Error);
    }

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        Assert.Equal("nginx:1.29", ImageReference.Parse("  nginx:1.29 ").Value.Canonical);
    }

    [Fact]
    public void Display_ShowsTheTagTheEngineResolves()
    {
        Assert.Equal("nginx:latest", ImageReference.Parse("nginx").Value.Display);
        Assert.Equal("nginx:1.29", ImageReference.Parse("nginx:1.29").Value.Display);
    }

    [Fact]
    public void Parse_RefusesANameOverTheLengthLimit()
    {
        var name = new string('a', 256);

        Assert.True(ImageReference.Parse(name).IsFailure);
    }
}
