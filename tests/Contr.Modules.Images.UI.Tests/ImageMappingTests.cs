using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.UI.Infrastructure;
using Xunit;
using ImagesV1 = Contr.Sdk.Images.V1;

namespace Contr.Modules.Images.UI.Tests;

public sealed class ImageMappingTests
{
    [Fact]
    public void ToPullEvent_MapsLayerProgress()
    {
        var response = new ImagesV1.PullImageResponse {
            Layer = new ImagesV1.LayerProgress {
                LayerId = "a1",
                Status = "Downloading",
                CurrentBytes = 50,
                TotalBytes = 100,
            },
        };

        Assert.Equal(new PullEvent.Layer("a1", "Downloading", 50, 100), ImageMapping.ToPullEvent(response));
    }

    [Fact]
    public void ToPullEvent_MapsTheEnd()
    {
        var response = new ImagesV1.PullImageResponse {
            Pulled = new ImagesV1.PulledImage { Reference = "nginx:1.29", ImageId = "sha256:5e1a" },
        };

        Assert.Equal(new PullEvent.Pulled("nginx:1.29", "sha256:5e1a"), ImageMapping.ToPullEvent(response));
        Assert.Null(ImageMapping.ToPullEvent(new ImagesV1.PullImageResponse()));
    }

    [Fact]
    public void ToDto_KeepsEveryTag()
    {
        var image = new ImagesV1.Image { Id = "sha256:5e1a", SizeBytes = 192_000_000, Containers = -1 };
        image.RepoTags.Add("nginx:1.29");
        image.RepoTags.Add("nginx:latest");

        var dto = ImageMapping.ToDto(image);

        Assert.Equal(["nginx:1.29", "nginx:latest"], dto.Tags);
        Assert.Equal(-1, dto.Containers);
    }
}
