using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Commands.RemoveContainer;
using Contr.Modules.Containers.Application.Containers.Commands.StartContainer;
using Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ContainersViewModelTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();

    public ContainersViewModelTests()
    {
        var running = new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty);
        _daemon.Status.Returns(running);
        _daemon.StatusChanges.Returns(Observable.Return(running));
    }

    [Fact]
    public async Task Start_SendsTheRowStateAndShowsAFailure()
    {
        var row = ContainerRowTests.Row("web", state: ContainerState.Exited, status: "Exited (0) 1 minute ago");
        _sender
            .Send(Arg.Is<StartContainerCommand>(command => command != null), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("Daemon.Conflict", "port is taken")));

        await CreateViewModel().StartAsync(row);

        await _sender.Received(1).Send(
            Arg.Is<StartContainerCommand>(
                command => command != null && command.Id == row.ContainerId && command.State == row.State),
            Arg.Any<CancellationToken>());
        _notifications.Received(1).Show("port is taken", InfoBarSeverity.Error, "Could not start web");
    }

    [Fact]
    public async Task Remove_DoesNothingWhenTheUserDeclines()
    {
        _dialogs.ShowConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        await CreateViewModel().RemoveAsync(ContainerRowTests.Row("web"));

        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<RemoveContainerCommand>(), default);
    }

    [Fact]
    public void Groups_ListProjectsByNameThenTheStandaloneContainers()
    {
        Stream(Snapshot(
            Dto("shop-web", "shop"),
            Dto("shop-db", "shop"),
            Dto("adminer"),
            Dto("docs-web", "docs")));
        var viewModel = CreateViewModel();

        viewModel.Activate();

        Assert.Equal(["docs", "shop", ContainerGroup.StandaloneKey], viewModel.Groups.Select(group => group.Key));
        Assert.Equal(["shop-db", "shop-web"], viewModel.Groups[1].Rows.Select(row => row.Name));
        Assert.Equal("2 containers · 2 running", viewModel.Groups[1].Summary);
        Assert.Equal(["4 running", "2 compose projects"], viewModel.SummaryPills.Select(pill => pill.Text));
    }

    [Fact]
    public void Filter_HidesTheCardsItEmpties()
    {
        Stream(Snapshot(Dto("shop-web", "shop"), Dto("adminer")));
        var viewModel = CreateViewModel();
        viewModel.Activate();

        viewModel.FilterText = "admin";

        Assert.Equal([ContainerGroup.StandaloneKey], viewModel.Groups.Select(group => group.Key));
        Assert.True(viewModel.HasMatches);
    }

    [Fact]
    public void ShowStopped_Off_HidesStoppedContainers()
    {
        var old = Dto("old", state: ContainerState.Exited, status: "Exited (0) 3 days ago");
        Stream(Snapshot(Dto("shop-web", "shop"), old));
        var viewModel = CreateViewModel();
        viewModel.Activate();

        viewModel.ShowStopped = false;

        Assert.Equal(["shop"], viewModel.Groups.Select(group => group.Key));
        Assert.True(viewModel.CanPrune);
    }

    [Fact]
    public void FlatList_ShowsEveryContainerInOneCard()
    {
        Stream(Snapshot(Dto("shop-web", "shop"), Dto("adminer")));
        var viewModel = CreateViewModel();
        viewModel.Activate();

        viewModel.ViewIndex = 1;

        var group = Assert.Single(viewModel.Groups);
        Assert.False(group.HasHeader);
        Assert.Equal(["adminer", "shop-web"], group.Rows.Select(row => row.Name));
    }

    [Fact]
    public void ExitNotice_OpensWhenARunningContainerFails()
    {
        var running = Dto("worker", "shop");
        var exited = running with { State = ContainerState.Exited, Status = "Exited (1) Less than a second ago" };
        Stream(Snapshot(running), new ListChange<ContainerDto>.Upserted(exited));
        var viewModel = CreateViewModel();

        viewModel.Activate();

        Assert.True(viewModel.IsExitNoticeOpen);
        Assert.StartsWith("worker in shop exited with code 1 at ", viewModel.ExitNotice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PruneStopped_DeletesEveryStoppedContainerOnceConfirmed()
    {
        Stream(Snapshot(
            Dto("web"),
            Dto("old", state: ContainerState.Exited, status: "Exited (0) 3 days ago"),
            Dto("new", state: ContainerState.Created, status: "Created")));
        _dialogs.ShowConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _sender
            .Send(Arg.Any<RemoveContainerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var viewModel = CreateViewModel();
        viewModel.Activate();

        await viewModel.PruneStoppedAsync();

        await _sender.Received(2).Send(Arg.Any<RemoveContainerCommand>(), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(
            Arg.Is<RemoveContainerCommand>(command => command != null && command.Id.Value == "web-id"),
            Arg.Any<CancellationToken>());
    }

    private static ListChange<ContainerDto> Snapshot(params ContainerDto[] containers)
    {
        return new ListChange<ContainerDto>.Snapshot(containers);
    }

    private static ContainerDto Dto(
        string name,
        string project = "",
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
            string.Empty);
    }

    private void Stream(params ListChange<ContainerDto>[] changes)
    {
        IAsyncEnumerable<ListChange<ContainerDto>> stream = TestStreams.Of(changes);
        _sender
            .Send(Arg.Any<WatchContainersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stream));
    }

    private ContainersViewModel CreateViewModel()
    {
        return new ContainersViewModel(_sender, _dialogs, _notifications, _navigation, _clipboard, _daemon);
    }
}
