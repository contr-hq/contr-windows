using System;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ContainerRowTests
{
    [Fact]
    public void FormatPorts_ShowsEachBindingOnce()
    {
        var ports = new[] {
            new PortDto(80, 8080, "0.0.0.0", "tcp"),
            new PortDto(80, 8080, "::", "tcp"),
            new PortDto(5432, 0, string.Empty, "tcp"),
        };

        Assert.Equal("8080→80/tcp, 5432/tcp", ContainerRow.FormatPorts(ports));
    }

    [Fact]
    public void Compare_PutsComposeProjectsFirst()
    {
        var loose = Row("adminer", project: string.Empty);
        var web = Row("shop-web-1", project: "shop");
        var db = Row("shop-db-1", project: "shop");

        Assert.True(ContainerRow.Compare(web, loose) < 0);
        Assert.True(ContainerRow.Compare(db, web) < 0);
    }

    [Theory]
    [InlineData(ContainerState.Running, false, true)]
    [InlineData(ContainerState.Exited, true, false)]
    public void From_FollowsTheLifecycleRules(ContainerState state, bool canStart, bool canStop)
    {
        var row = Row("web", state: state);

        Assert.Equal(canStart, row.CanStart);
        Assert.Equal(canStop, row.CanStop);
    }

    private static ContainerRow Row(string name, string project = "", ContainerState state = ContainerState.Running)
    {
        return ContainerRow.From(new ContainerDto(
            new ContainerId(name + "-id"), name, "nginx:1.29", state, "Up", DateTimeOffset.UnixEpoch, [], project));
    }
}
