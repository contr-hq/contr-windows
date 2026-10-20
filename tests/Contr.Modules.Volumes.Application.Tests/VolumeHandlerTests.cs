using System.Threading;
using System.Threading.Tasks;
using Contr.Modules.Volumes.Application.Abstractions;
using Contr.Modules.Volumes.Application.Volumes.Commands.CreateVolume;
using Contr.Modules.Volumes.Application.Volumes.Commands.RemoveVolume;
using Contr.Modules.Volumes.Domain;
using Contr.SharedKernel;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Volumes.Application.Tests;

public sealed class VolumeHandlerTests
{
    private readonly IVolumesGateway _gateway = Substitute.For<IVolumesGateway>();

    [Fact]
    public async Task Create_RefusesAnInvalidNameWithoutAskingTheEngine()
    {
        var result = await new CreateVolumeCommandHandler(_gateway)
            .Handle(new CreateVolumeCommand("-bad"), CancellationToken.None);

        Assert.Equal("Volumes.Name.Invalid", result.Error.Code);
        await _gateway.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task Create_HandsTheTrimmedNameToTheGateway()
    {
        _gateway.CreateAsync(new VolumeName("pgdata"), Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await new CreateVolumeCommandHandler(_gateway)
            .Handle(new CreateVolumeCommand(" pgdata "), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Remove_PassesTheEngineFailureThrough()
    {
        var inUse = new Error("Daemon.Conflict", "volume is in use");
        _gateway.RemoveAsync(default!, default).ReturnsForAnyArgs(Result.Failure(inUse));

        var result = await new RemoveVolumeCommandHandler(_gateway)
            .Handle(new RemoveVolumeCommand(new VolumeName("pgdata")), CancellationToken.None);

        Assert.Equal(inUse, result.Error);
    }
}
