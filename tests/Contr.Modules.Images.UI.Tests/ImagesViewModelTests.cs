using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Application.Images.Commands.PruneImages;
using Contr.Modules.Images.Application.Images.Queries.FollowPull;
using Contr.Modules.Images.Application.Images.Queries.PullImage;
using Contr.Modules.Images.UI.ViewModels;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Images.UI.Tests;

public sealed class ImagesViewModelTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly IActivity _activity = Substitute.For<IActivity>();
    private readonly BehaviorSubject<IReadOnlyList<DaemonOperation>> _operations = new([]);

    public ImagesViewModelTests()
    {
        var running = new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty);
        _daemon.Status.Returns(running);
        _daemon.StatusChanges.Returns(Observable.Return(running));
        _activity.Changes.Returns(_operations);
        _activity.Current.Returns(_ => _operations.Value);
    }

    [Fact]
    public async Task Activate_FollowsAPullAnotherPageStarted()
    {
        _operations.OnNext([Pull("op-7", "postgres:17")]);
        _sender
            .Send(
                Arg.Is<FollowPullQuery>(query => query != null && query.OperationId == "op-7"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestStreams.Of<PullEvent>(
                new PullEvent.Layer("a", "Downloading", 50, 100),
                new PullEvent.Pulled("postgres:17", "sha256:aa"))));
        var viewModel = CreateViewModel();

        viewModel.Activate();
        await viewModel.Pulling;

        Assert.Equal("postgres:17", viewModel.PullReference);
        _notifications.Received(1).Show("Pulled postgres:17.", InfoBarSeverity.Success);
        Assert.False(viewModel.IsPulling);
    }

    [Fact]
    public async Task Activate_LeavesAPullItFollowedToItsEnd()
    {
        _operations.OnNext([Pull("op-7", "postgres:17")]);
        _sender
            .Send(Arg.Any<FollowPullQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestStreams.Of<PullEvent>(new PullEvent.Pulled("postgres:17", "sha256:aa"))));
        var viewModel = CreateViewModel();
        viewModel.Activate();
        await viewModel.Pulling;

        // The daemon still lists the pull as running for a moment after it ended.
        _operations.OnNext([Pull("op-7", "postgres:17")]);

        await _sender.Received(1).Send(Arg.Any<FollowPullQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelPull_CancelsTheDaemonsOperation()
    {
        var pulling = new TaskCompletionSource();
        GivenPull(Result.Success(Endless(new PullEvent.Started("op-9"), pulling.Task)));
        _activity.CancelAsync("op-9", Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Success()));
        var viewModel = CreateViewModel();
        await viewModel.StartPullAsync("nginx");

        viewModel.CancelPull();

        await _activity.Received(1).CancelAsync("op-9", Arg.Any<CancellationToken>());
        Assert.True(viewModel.IsPulling);
        pulling.SetResult();
        await viewModel.Pulling;
    }

    [Fact]
    public async Task Dispose_StopsFollowingWithoutCancellingThePull()
    {
        var pulling = new TaskCompletionSource();
        GivenPull(Result.Success(Endless(new PullEvent.Started("op-9"), pulling.Task)));
        var viewModel = CreateViewModel();
        await viewModel.StartPullAsync("nginx");

        viewModel.Dispose();
        await viewModel.Pulling;

        await _activity.DidNotReceive().CancelAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<InfoBarSeverity>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Pull_ShowsWhyAReferenceIsRefused()
    {
        var refused = new Error("Images.Reference.Invalid", "'A' is not valid.");
        GivenPull(Result.Failure<IAsyncEnumerable<PullEvent>>(refused));
        var viewModel = CreateViewModel();

        var error = await viewModel.StartPullAsync("A");

        Assert.Equal("'A' is not valid.", error);
        Assert.False(viewModel.IsPulling);
    }

    [Fact]
    public async Task Pull_ReportsLayerProgressThenTheResult()
    {
        var viewModel = CreateViewModel();
        var progress = new List<string>();
        viewModel.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(ImagesViewModel.PullStatus))
            {
                progress.Add($"{viewModel.PullStatus} {viewModel.PullProgress:0}");
            }
        };
        GivenPull(Result.Success(TestStreams.Of<PullEvent>(
            new PullEvent.Layer("a", "Downloading", 25, 100),
            new PullEvent.Layer("b", "Already exists", 0, 0),
            new PullEvent.Layer("a", "Pull complete", 100, 100),
            new PullEvent.Pulled("nginx:1.29", "sha256:5e1a"))));

        Assert.Null(await viewModel.StartPullAsync("nginx:1.29"));
        await viewModel.Pulling;

        Assert.Equal(
            [
                "Starting 0",
                "25 B of 100 B · 0 of 1 layers 25",
                "25 B of 100 B · 1 of 2 layers 25",
                "100 B of 100 B · 2 of 2 layers 100",
            ],
            progress);
        Assert.Equal(
            ["Pull complete · 100 B", "Already exists"],
            viewModel.PullLayers.Select(layer => layer.StatusText));
        Assert.False(viewModel.IsPulling);
        _notifications.Received(1).Show("Pulled nginx:1.29.", InfoBarSeverity.Success);
    }

    [Fact]
    public async Task Pull_ShowsAFailedPull()
    {
        GivenPull(Result.Success(TestStreams.Of<PullEvent>(
            new PullEvent.Failed(new Error("Daemon.Failed", "manifest unknown")))));

        var viewModel = CreateViewModel();
        await viewModel.StartPullAsync("nginx:nope");
        await viewModel.Pulling;

        _notifications.Received(1).Show("manifest unknown", InfoBarSeverity.Error, "Pull failed");
    }

    [Fact]
    public async Task RemoveUnused_PrunesEveryUnusedImageOnceConfirmed()
    {
        _dialogs.ShowConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _sender
            .Send(Arg.Any<PruneImagesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PruneResult(2, 1_100_000_000)));

        await CreateViewModel().RemoveUnusedAsync();

        await _sender.Received(1).Send(
            Arg.Is<PruneImagesCommand>(command => command != null && command.All),
            Arg.Any<CancellationToken>());
        _notifications.Received(1).Show("Deleted 2 images and freed 1.1 GB.", InfoBarSeverity.Success);
    }

    private void GivenPull(Result<IAsyncEnumerable<PullEvent>> result)
    {
        _sender
            .Send(Arg.Is<PullImageQuery>(query => query != null), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
    }

    private ImagesViewModel CreateViewModel()
    {
        return new ImagesViewModel(
            _sender, _dialogs, _notifications, _clipboard, _daemon, Substitute.For<IDaemonSettings>(), _activity);
    }

    private static DaemonOperation Pull(string id, string reference)
    {
        return new DaemonOperation(
            id, new OperationWork.PullImage(reference, string.Empty), OperationState.Running, DateTimeOffset.UnixEpoch);
    }

    /// <summary>A pull that starts, then goes on until <paramref name="until"/> completes or the reader stops.
    /// </summary>
    private static async IAsyncEnumerable<PullEvent> Endless(
        PullEvent first, Task until, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return first;
        await until.WaitAsync(cancellationToken);
    }
}
