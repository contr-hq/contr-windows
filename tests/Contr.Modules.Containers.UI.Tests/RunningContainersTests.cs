using System;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.Infrastructure;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class RunningContainersTests
{
    [Fact]
    public void Running_ListsOnlyRunningContainersWithTheirFirstPort()
    {
        var web = Dto("web") with {
            Ports = [new PortDto(80, 8080, "0.0.0.0", "tcp")],
        };
        var running = RunningContainers.Running([web, Dto("old", ContainerState.Exited)]);

        var container = Assert.Single(running);
        Assert.Equal("localhost:8080", container.Port);
        Assert.Equal(new Uri("http://localhost:8080"), container.Link);
    }

    private static ContainerDto Dto(string name, ContainerState state = ContainerState.Running)
    {
        return new ContainerDto(
            new ContainerId(name + "-id"),
            name,
            "nginx:1.29",
            state,
            "Up 2 hours",
            DateTimeOffset.UnixEpoch,
            [],
            string.Empty,
            string.Empty);
    }
}
