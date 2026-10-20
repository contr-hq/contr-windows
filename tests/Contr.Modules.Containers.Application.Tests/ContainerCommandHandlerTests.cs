using System.Threading;
using System.Threading.Tasks;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Commands.CreateContainer;
using Contr.Modules.Containers.Application.Containers.Commands.KillContainer;
using Contr.Modules.Containers.Application.Containers.Commands.PauseContainer;
using Contr.Modules.Containers.Application.Containers.Commands.RemoveContainer;
using Contr.Modules.Containers.Application.Containers.Commands.RenameContainer;
using Contr.Modules.Containers.Application.Containers.Commands.RestartContainer;
using Contr.Modules.Containers.Application.Containers.Commands.StartContainer;
using Contr.Modules.Containers.Application.Containers.Commands.StopContainer;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Containers.Application.Tests;

public sealed class ContainerCommandHandlerTests
{
    private static readonly ContainerId _id = new("c0ffee01");

    private readonly IContainersGateway _gateway = Substitute.For<IContainersGateway>();

    public ContainerCommandHandlerTests()
    {
        _gateway.StartAsync(default!, default).ReturnsForAnyArgs(Result.Success());
        _gateway.StopAsync(default!, default).ReturnsForAnyArgs(Result.Success());
        _gateway.RestartAsync(default!, default).ReturnsForAnyArgs(Result.Success());
        _gateway.RemoveAsync(default!, default, default).ReturnsForAnyArgs(Result.Success());
    }

    [Fact]
    public async Task Start_AsksTheEngineForAnExitedContainer()
    {
        var result = await new StartContainerCommandHandler(_gateway)
            .Handle(new StartContainerCommand(_id, ContainerState.Exited), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _gateway.Received(1).StartAsync(_id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_RefusesARunningContainerWithoutAskingTheEngine()
    {
        var result = await new StartContainerCommandHandler(_gateway)
            .Handle(new StartContainerCommand(_id, ContainerState.Running), CancellationToken.None);

        Assert.Equal(ContainersErrors.CannotStart, result.Error);
        await _gateway.DidNotReceiveWithAnyArgs().StartAsync(default!, default);
    }

    [Fact]
    public async Task Stop_RefusesAnExitedContainer()
    {
        var result = await new StopContainerCommandHandler(_gateway)
            .Handle(new StopContainerCommand(_id, ContainerState.Exited), CancellationToken.None);

        Assert.Equal(ContainersErrors.CannotStop, result.Error);
        await _gateway.DidNotReceiveWithAnyArgs().StopAsync(default!, default);
    }

    [Fact]
    public async Task Restart_AsksTheEngineForARunningContainer()
    {
        await new RestartContainerCommandHandler(_gateway)
            .Handle(new RestartContainerCommand(_id, ContainerState.Running), CancellationToken.None);

        await _gateway.Received(1).RestartAsync(_id, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ContainerState.Running, true)]
    [InlineData(ContainerState.Exited, false)]
    public async Task Remove_ForcesOnlyARunningContainer(ContainerState state, bool force)
    {
        await new RemoveContainerCommandHandler(_gateway)
            .Handle(new RemoveContainerCommand(_id, state), CancellationToken.None);

        await _gateway.Received(1).RemoveAsync(_id, force, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_PassesTheEngineFailureThrough()
    {
        var conflict = new Error("Daemon.Conflict", "removal in progress");
        _gateway.RemoveAsync(default!, default, default).ReturnsForAnyArgs(Result.Failure(conflict));

        var result = await new RemoveContainerCommandHandler(_gateway)
            .Handle(new RemoveContainerCommand(_id, ContainerState.Exited), CancellationToken.None);

        Assert.Equal(conflict, result.Error);
    }

    [Fact]
    public async Task Pause_RefusesAPausedContainer()
    {
        var result = await new PauseContainerCommandHandler(_gateway)
            .Handle(new PauseContainerCommand(_id, ContainerState.Paused), CancellationToken.None);

        Assert.Equal(ContainersErrors.CannotPause, result.Error);
        await _gateway.DidNotReceiveWithAnyArgs().PauseAsync(default!, default);
    }

    [Fact]
    public async Task Kill_AsksTheEngineForARunningContainer()
    {
        _gateway.KillAsync(default!, default).ReturnsForAnyArgs(Result.Success());

        await new KillContainerCommandHandler(_gateway)
            .Handle(new KillContainerCommand(_id, ContainerState.Running), CancellationToken.None);

        await _gateway.Received(1).KillAsync(_id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rename_RefusesAnInvalidNameWithoutAskingTheEngine()
    {
        var result = await new RenameContainerCommandHandler(_gateway)
            .Handle(new RenameContainerCommand(_id, "my web"), CancellationToken.None);

        Assert.Equal("Containers.Name.Invalid", result.Error.Code);
        await _gateway.DidNotReceiveWithAnyArgs().RenameAsync(default!, default!, default);
    }

    [Fact]
    public async Task Create_RefusesAMountOutsideTheContainersFilesystem()
    {
        var spec = ContainerSpec.For("nginx") with {
            Mounts = [new MountRequest(MountSource.Bind, @"C:\site", "html", ReadOnly: true)],
        };

        var result = await new CreateContainerCommandHandler(_gateway)
            .Handle(new CreateContainerCommand(spec), CancellationToken.None);

        Assert.Equal("Containers.Path.Invalid", result.Error.Code);
        await _gateway.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task Create_PassesATrimmedImageToTheEngine()
    {
        _gateway
            .CreateAsync(default!, default)
            .ReturnsForAnyArgs(Result.Success(new CreatedContainer(_id, [])));

        var result = await new CreateContainerCommandHandler(_gateway)
            .Handle(new CreateContainerCommand(ContainerSpec.For(" nginx:1.29 ")), CancellationToken.None);

        Assert.Equal(_id, result.Value.Id);
        await _gateway.Received(1).CreateAsync(
            Arg.Is<ContainerSpec>(spec => spec != null && spec.Image == "nginx:1.29"), Arg.Any<CancellationToken>());
    }
}
