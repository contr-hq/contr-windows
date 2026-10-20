using System;
using System.Collections.Generic;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.UI.Common.Controls;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ContainerRowTests
{
    [Fact]
    public void PortLinks_ShowEachPublishedBindingOnce()
    {
        var links = PortLink.From([
            new PortDto(3000, 8080, "0.0.0.0", "tcp"),
            new PortDto(3000, 8080, "::", "tcp"),
            new PortDto(5432, 0, string.Empty, "tcp"),
        ]);

        var link = Assert.Single(links);
        Assert.Equal("localhost:8080", link.Address);
        Assert.Equal("→ 3000", link.Target);
        Assert.Equal(new Uri("http://localhost:8080"), link.Uri);
    }

    [Fact]
    public void PortLinks_KeepABoundAddressAndOmitTheSamePort()
    {
        var link = Assert.Single(PortLink.From([new PortDto(6443, 6443, "127.0.0.1", "tcp")]));

        Assert.Equal("127.0.0.1:6443", link.Address);
        Assert.False(link.HasTarget);
    }

    [Fact]
    public void PortLinks_LinkOnlyTcp()
    {
        var link = Assert.Single(PortLink.From([new PortDto(53, 5353, "0.0.0.0", "udp")]));

        Assert.Equal("localhost:5353/udp", link.Address);
        Assert.Null(link.Uri);
    }

    [Theory]
    [InlineData(ContainerState.Running, "Up 2 hours", StatusKind.Good, "Running", "2 h")]
    [InlineData(ContainerState.Running, "Up 2 hours (unhealthy)", StatusKind.Caution, "Unhealthy", "2 h")]
    [InlineData(ContainerState.Exited, "Exited (1) 14 minutes ago", StatusKind.Critical, "Exited (1)", "14 min")]
    [InlineData(ContainerState.Exited, "Exited (0) 3 days ago", StatusKind.Neutral, "Exited (0)", "3 d")]
    [InlineData(ContainerState.Paused, "Up 5 hours (Paused)", StatusKind.Paused, "Paused", "5 h")]
    [InlineData(ContainerState.Created, "Created", StatusKind.Neutral, "Created", "")]
    public void Status_ReadsTheEngineText(
        ContainerState state, string status, StatusKind kind, string text, string elapsed)
    {
        var row = Row("web", state: state, status: status);

        Assert.Equal(kind, row.StatusKind);
        Assert.Equal(text, row.StatusText);
        Assert.Equal(elapsed, row.Elapsed);
    }

    [Theory]
    [InlineData(ContainerState.Running, "Up 2 hours", true, false, false)]
    [InlineData(ContainerState.Exited, "Exited (1) 14 minutes ago", false, true, false)]
    [InlineData(ContainerState.Exited, "Exited (0) 3 days ago", false, false, true)]
    [InlineData(ContainerState.Paused, "Up 2 hours (Paused)", false, true, false)]
    public void Actions_FollowTheDesignPerState(
        ContainerState state, string status, bool terminal, bool logs, bool delete)
    {
        var row = Row("web", state: state, status: status);

        Assert.Equal(terminal, row.OffersTerminal);
        Assert.Equal(logs, row.OffersLogs);
        Assert.Equal(delete, row.OffersDelete);
    }

    [Fact]
    public void From_TakesTheFirstComposeFile()
    {
        var row = Row("web", project: "shop", files: @"C:\src\shop\compose.yaml,C:\src\shop\compose.override.yaml");

        Assert.Equal(@"C:\src\shop\compose.yaml", row.ComposeFile);
    }

    [Theory]
    [InlineData("WEB", true)]
    [InlineData("nginx", true)]
    [InlineData("8080", true)]
    [InlineData("3000", true)]
    [InlineData("postgres", false)]
    public void Matches_SearchesNameImageAndPorts(string filter, bool expected)
    {
        var row = Row("web", ports: [new PortDto(3000, 8080, "0.0.0.0", "tcp")]);

        Assert.Equal(expected, row.Matches(filter));
    }

    internal static ContainerRow Row(
        string name,
        string project = "",
        ContainerState state = ContainerState.Running,
        string status = "Up 2 hours",
        string files = "",
        IReadOnlyList<PortDto>? ports = null)
    {
        var dto = new ContainerDto(
            new ContainerId(name + "-id"),
            name,
            "nginx:1.29",
            state,
            status,
            DateTimeOffset.UnixEpoch,
            ports ?? [],
            project,
            files);

        return ContainerRow.From(dto, new ContainerUsage());
    }
}
