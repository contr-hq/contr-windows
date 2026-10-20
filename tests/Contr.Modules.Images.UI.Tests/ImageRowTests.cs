using System;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Domain;
using Contr.Modules.Images.UI.ViewModels;
using Xunit;

namespace Contr.Modules.Images.UI.Tests;

public sealed class ImageRowTests
{
    [Theory]
    [InlineData("nginx:1.29", "nginx", "1.29")]
    [InlineData("localhost:5000/team/app:v2", "localhost:5000/team/app", "v2")]
    [InlineData("localhost:5000/team/app", "localhost:5000/team/app", "<none>")]
    public void SplitTag_SplitsAtTheColonAfterTheLastSlash(string reference, string repository, string tag)
    {
        Assert.Equal((repository, tag), ImageRow.SplitTag(reference));
    }

    [Fact]
    public void From_ShowsADanglingImageByItsId()
    {
        var row = ImageRow.From(Image([], containers: 0));

        Assert.Equal("<none>", row.Repository);
        Assert.Equal("5e1a00000000", row.DisplayName);
        Assert.False(row.InUse);
    }

    [Fact]
    public void From_CountsTheOtherTags()
    {
        var row = ImageRow.From(Image(["nginx:1.29", "nginx:latest", "nginx:stable"], containers: 2));

        Assert.Equal("nginx:1.29", row.DisplayName);
        Assert.Equal("+2 tags", row.OtherTags);
        Assert.True(row.InUse);
        Assert.Equal("192 MB", row.Size);
    }

    private static ImageDto Image(string[] tags, long containers)
    {
        return new ImageDto(
            new ImageId("sha256:5e1a000000000000"), tags, DateTimeOffset.UtcNow.AddDays(-3), 192_000_000, containers);
    }
}
