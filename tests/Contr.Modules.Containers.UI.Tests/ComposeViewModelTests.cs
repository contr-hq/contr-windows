using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ComposeViewModelTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();

    public ComposeViewModelTests()
    {
        var running = new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty);
        _daemon.Status.Returns(running);
        _daemon.StatusChanges.Returns(Observable.Return(running));
    }

    [Fact]
    public void Activate_ListsProjectsWithTheirRunningServices()
    {
        IAsyncEnumerable<ListChange<ContainerDto>> stream = TestStreams.Of<ListChange<ContainerDto>>(
            new ListChange<ContainerDto>.Snapshot([
                Dto("shop-api-1", "shop", "api"),
                Dto("shop-worker-1", "shop", "worker", ContainerState.Exited, "Exited (1) 1 minute ago"),
                Dto("docs-web-1", "docs", "web"),
                Dto("adminer", string.Empty, string.Empty),
            ]));
        _sender
            .Send(Arg.Any<WatchContainersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stream));
        var viewModel = new ComposeViewModel(
            _sender, Substitute.For<INotificationService>(), _daemon, Substitute.For<IDaemonSettings>());

        viewModel.Activate();

        Assert.Equal(["docs", "shop"], viewModel.Projects.Select(project => project.Name));
        var shop = viewModel.Projects.Single(project => project.Name == "shop");
        Assert.Equal("1 of 2 running", shop.StatusText);
        Assert.Equal(PillKind.Caution, shop.PillKind);
        Assert.Equal("docs", viewModel.Selected?.Name);
        Assert.Equal(["All", "web"], viewModel.Filters);
    }

    private static ContainerDto Dto(
        string name,
        string project,
        string service,
        ContainerState state = ContainerState.Running,
        string status = "Up 2 hours")
    {
        return new ContainerDto(
            new ContainerId(name + "-id"),
            name,
            "nginx:1.29",
            state,
            status,
            DateTimeOffset.UnixEpoch,
            [],
            project,
            string.Empty,
            service);
    }
}
