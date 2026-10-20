using Contr.Modules.Images.Domain;
using Xunit;

namespace Contr.Modules.Images.Domain.Tests;

public sealed class ImageIdTests
{
    [Fact]
    public void Short_DropsTheAlgorithm()
    {
        var id = new ImageId("sha256:5e1a000000000000000000000000000000000000000000000000000000000000");

        Assert.Equal("5e1a00000000", id.Short);
    }

    [Fact]
    public void Short_ReadsAnIdWithoutAlgorithm()
    {
        Assert.Equal("5e1a00000000", new ImageId("5e1a0000000000000000").Short);
    }
}
