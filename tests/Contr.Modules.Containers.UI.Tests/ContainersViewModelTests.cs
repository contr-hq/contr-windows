using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Commands.RemoveContainer;
using Contr.Modules.Containers.Application.Containers.Commands.StartContainer;
using Contr.Modules.Containers.Application.Containers.Queries.StreamContainerLogs;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.SharedKernel;
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

    [Fact]
    public async Task Start_SendsTheRowStateAndShowsAFailure()
    {
        var row = Row(ContainerState.Exited);
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

        await CreateViewModel().RemoveAsync(Row(ContainerState.Running));

        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<RemoveContainerCommand>(), default);
    }

    [Fact]
    public async Task Remove_SendsTheCommandOnceConfirmed()
    {
        _dialogs.ShowConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _sender
            .Send(Arg.Is<RemoveContainerCommand>(command => command != null), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await CreateViewModel().RemoveAsync(Row(ContainerState.Running));

        await _sender.Received(1).Send(
            Arg.Is<RemoveContainerCommand>(command => command != null && command.State == ContainerState.Running),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void OpenLogs_ShowsTheOutputAndTheFailure()
    {
        IAsyncEnumerable<ContainerLogEvent> events = TestStreams.Of<ContainerLogEvent>(
            new ContainerLogEvent.Output("starting\n", false),
            new ContainerLogEvent.Output("ready\n", false),
            new ContainerLogEvent.Failed(new Error("Daemon.Unavailable", "connection closed")));
        _sender
            .Send(Arg.Is<StreamContainerLogsQuery>(query => query != null), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(events));
        var viewModel = CreateViewModel();

        viewModel.OpenLogs(Row(ContainerState.Running));

        Assert.True(viewModel.IsLogsOpen);
        Assert.Equal("web", viewModel.LogsTitle);
        Assert.Equal("starting\nready\n", viewModel.LogsText);
        Assert.Equal("connection closed", viewModel.LogsError);

        viewModel.CloseLogs();

        Assert.False(viewModel.IsLogsOpen);
    }

    private static ContainerRow Row(ContainerState state)
    {
        return ContainerRow.From(new ContainerDto(
            new ContainerId("c0ffee01"), "web", "nginx:1.29", state, "Up", DateTimeOffset.UnixEpoch, [], string.Empty));
    }

    private ContainersViewModel CreateViewModel()
    {
        return new ContainersViewModel(_sender, _dialogs, _notifications);
    }
}
