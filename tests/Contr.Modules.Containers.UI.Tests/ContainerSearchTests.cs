using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.Infrastructure;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.SharedKernel;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ContainerSearchTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    [Fact]
    public async Task Search_MatchesNameImageAndProject_NamesStartingWithTheTextFirst()
    {
        Stream(Dto("adminer", "adminer:5"), Dto("shop-web", "nginx:1.29", "shop"), Dto("web", "nginx:1.29"));

        var hits = await new ContainerSearch(_sender).SearchAsync("web", CancellationToken.None);

        Assert.Equal(["web", "shop-web"], hits.Select(hit => hit.Title));
        Assert.Equal("Container · nginx:1.29", hits[0].Detail);
    }

    [Fact]
    public async Task Search_OpensTheContainerPageOnItsLogs()
    {
        Stream(Dto("db", "postgres:18", "shop"));

        var hit = Assert.Single(await new ContainerSearch(_sender).SearchAsync("shop", CancellationToken.None));

        Assert.Equal("ContainerDetail", hit.PageKey);
        Assert.Equal(new object[] { new ContainerId("db-id"), ContainerDetailTab.Logs }, hit.Arguments);
    }

    [Fact]
    public async Task Search_FindsNothingWhenTheStreamFails()
    {
        Stream(new ListChange<ContainerDto>.Failed(new Error("Engine.Down", "down")));

        Assert.Empty(await new ContainerSearch(_sender).SearchAsync("web", CancellationToken.None));
    }

    private static ContainerDto Dto(string name, string image, string project = "")
    {
        return new ContainerDto(
            new ContainerId(name + "-id"),
            name,
            image,
            ContainerState.Running,
            "Up 2 hours",
            DateTimeOffset.UnixEpoch,
            [],
            project,
            string.Empty);
    }

    private void Stream(params ContainerDto[] containers)
    {
        Stream(new ListChange<ContainerDto>.Snapshot(containers));
    }

    private void Stream(ListChange<ContainerDto> change)
    {
        IAsyncEnumerable<ListChange<ContainerDto>> stream = TestStreams.Of(change);
        _sender
            .Send(Arg.Any<WatchContainersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stream));
    }
}
