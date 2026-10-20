using Contr.Modules.Containers.Domain;
using Xunit;

namespace Contr.Modules.Containers.Domain.Tests;

public sealed class ContainerLifecycleTests
{
    [Theory]
    [InlineData(ContainerState.Created, true)]
    [InlineData(ContainerState.Exited, true)]
    [InlineData(ContainerState.Running, false)]
    [InlineData(ContainerState.Paused, false)]
    [InlineData(ContainerState.Dead, false)]
    [InlineData(ContainerState.Removing, false)]
    public void CanStart_OnlyFromCreatedOrExited(ContainerState state, bool expected)
    {
        Assert.Equal(expected, state.CanStart());
    }

    [Theory]
    [InlineData(ContainerState.Running, true)]
    [InlineData(ContainerState.Paused, true)]
    [InlineData(ContainerState.Restarting, true)]
    [InlineData(ContainerState.Exited, false)]
    [InlineData(ContainerState.Created, false)]
    public void CanStop_OnlyWhileItRuns(ContainerState state, bool expected)
    {
        Assert.Equal(expected, state.CanStop());
    }

    [Theory]
    [InlineData(ContainerState.Running, true)]
    [InlineData(ContainerState.Exited, true)]
    [InlineData(ContainerState.Removing, false)]
    [InlineData(ContainerState.Dead, false)]
    public void CanRestart_FollowsTheEngine(ContainerState state, bool expected)
    {
        Assert.Equal(expected, state.CanRestart());
    }

    [Theory]
    [InlineData(ContainerState.Running, true)]
    [InlineData(ContainerState.Paused, true)]
    [InlineData(ContainerState.Stopping, true)]
    [InlineData(ContainerState.Exited, false)]
    [InlineData(ContainerState.Dead, false)]
    public void RemovalNeedsForce_WhileItRuns(ContainerState state, bool expected)
    {
        Assert.Equal(expected, state.RemovalNeedsForce());
    }

    [Fact]
    public void CanRemove_ExceptWhileBeingRemoved()
    {
        Assert.True(ContainerState.Running.CanRemove());
        Assert.False(ContainerState.Removing.CanRemove());
    }
}
