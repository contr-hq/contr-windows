using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Application.Images.Commands.PruneImages;
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

    public ImagesViewModelTests()
    {
        _daemon.Status.Returns(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty));
    }

    [Fact]
    public async Task Pull_ShowsWhyAReferenceIsRefused()
    {
        var refused = new Error("Images.Reference.Invalid", "'A' is not valid.");
        GivenPull(Result.Failure<IAsyncEnumerable<PullEvent>>(refused));
        var viewModel = CreateViewModel();

        await viewModel.PullAsync("A");

        Assert.False(viewModel.IsPulling);
        _notifications.Received(1).Show("'A' is not valid.", InfoBarSeverity.Error, "Cannot pull");
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

        await viewModel.PullAsync("nginx:1.29");

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

        await CreateViewModel().PullAsync("nginx:nope");

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
        return new ImagesViewModel(_sender, _dialogs, _notifications, _clipboard, _daemon);
    }
}
