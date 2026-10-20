using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Modules.Images.Application.Abstractions;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Application.Images.Commands.RemoveImage;
using Contr.Modules.Images.Application.Images.Queries.PullImage;
using Contr.Modules.Images.Domain;
using Contr.SharedKernel;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Images.Application.Tests;

public sealed class ImageHandlerTests
{
    private readonly IImagesGateway _gateway = Substitute.For<IImagesGateway>();

    [Fact]
    public async Task Pull_RefusesAnInvalidReferenceWithoutAskingTheEngine()
    {
        var result = await new PullImageQueryHandler(_gateway)
            .Handle(new PullImageQuery("Not Valid"), CancellationToken.None);

        Assert.Equal("Images.Reference.Invalid", result.Error.Code);
        _gateway.DidNotReceiveWithAnyArgs().PullAsync(default!, default);
    }

    [Fact]
    public async Task Pull_HandsTheParsedReferenceToTheGateway()
    {
        var events = AsyncEnumerable.Empty<PullEvent>();
        _gateway
            .PullAsync(
                Arg.Is<ImageReference>(reference => reference != null && reference.Canonical == "nginx:1.29"),
                Arg.Any<CancellationToken>())
            .Returns(events);

        var result = await new PullImageQueryHandler(_gateway)
            .Handle(new PullImageQuery(" nginx:1.29 "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(events, result.Value);
    }

    [Fact]
    public async Task Remove_PassesTheIdToTheGateway()
    {
        var id = new ImageId("sha256:5e1a");
        _gateway.RemoveAsync(id, Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await new RemoveImageCommandHandler(_gateway)
            .Handle(new RemoveImageCommand(id), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
