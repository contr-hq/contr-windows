using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.UI;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class UpdatesSettingsViewModelTests
{
    private readonly IDaemonSettings _settings = Substitute.For<IDaemonSettings>();
    private readonly IDaemonUpdates _updates = Substitute.For<IDaemonUpdates>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    public UpdatesSettingsViewModelTests()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0-alpha.2", 21, "WSL 2"));
        _updates
            .GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(Status(null)));
    }

    [Fact]
    public async Task Activate_ShowsTheReleaseTheDaemonFound()
    {
        var published = DateTimeOffset.Now.AddDays(-3);
        _updates
            .GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(Status(new AvailableRelease(
                "0.2.0", new Uri("https://github.com/contr-hq/contr/releases/tag/v0.2.0"), published, "notes"))));
        var viewModel = Create();

        await viewModel.ActivateAsync();
        viewModel.Show(Saved());

        Assert.True(viewModel.HasAvailable);
        Assert.Equal("Contr 0.2.0 is available", viewModel.AvailableTitle);
        Assert.Equal(
            ("Published 3d ago · you have ", "0.1.0-alpha.2"), (viewModel.AvailableDetail, viewModel.CurrentVersion));
        Assert.Equal("What's new in 0.2.0", viewModel.WhatsNewText);
        Assert.True(viewModel.CanCheckNow);
        Assert.Equal("Checked 2h ago", viewModel.CheckedText);
    }

    [Fact]
    public async Task CheckNow_ShowsWhenItCheckedOrWhyItFailed()
    {
        _updates
            .CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(Status(null) with { Error = "github.com timed out" }));
        var viewModel = Create();
        await viewModel.ActivateAsync();
        viewModel.Show(Saved());

        await viewModel.CheckNowAsync();

        Assert.False(viewModel.HasAvailable);
        Assert.Equal("The last check failed: github.com timed out", viewModel.CheckedText);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task WithTheCheckOff_ThereIsNothingToCheck()
    {
        var viewModel = Create();
        await viewModel.ActivateAsync();

        viewModel.Show(Saved(check: false));

        Assert.False(viewModel.CanCheckNow);
        Assert.Equal("Turn on the daily check to check now.", viewModel.CheckedText);
        await viewModel.CheckNowAsync();
        await _updates.DidNotReceive().CheckAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Settings_ChangeTheCheckAndTheChannel()
    {
        _settings
            .UpdateAsync(default!, default)
            .ReturnsForAnyArgs(Result.Success(new DaemonSettingsChange(Saved(), new HashSet<DaemonSetting>())));
        var viewModel = Create();
        await viewModel.ActivateAsync();
        viewModel.Show(Saved());

        await viewModel.ChooseChannelAsync(1);
        await viewModel.ChooseCheckAsync(false);

        await _settings.Received(1).UpdateAsync(
            Arg.Is<DaemonSettingsEdit>(edit => edit != null && edit.UpdatesPrereleases == true),
            Arg.Any<CancellationToken>());
        await _settings.Received(1).UpdateAsync(
            Arg.Is<DaemonSettingsEdit>(edit => edit != null && edit.UpdatesCheck == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ALockedCheck_CannotChange()
    {
        var viewModel = Create();
        await viewModel.ActivateAsync();

        viewModel.Show(Saved(check: false, locked: [DaemonSetting.UpdatesCheck]));

        Assert.True(viewModel.IsCheckLocked);
        Assert.False(viewModel.CanChooseCheck);
        Assert.True(viewModel.CanChooseChannel);
    }

    [Fact]
    public async Task AnOlderDaemon_OffersNoUpdates()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 18, "WSL 2"));
        var viewModel = Create();

        await viewModel.ActivateAsync();

        Assert.True(viewModel.LacksOffer);
        await _updates.DidNotReceive().GetStatusAsync(Arg.Any<CancellationToken>());
    }

    private UpdatesSettingsViewModel Create()
    {
        return new UpdatesSettingsViewModel(_settings, _updates, _daemon, _notifications);
    }

    private static UpdateStatus Status(AvailableRelease? available)
    {
        return new UpdateStatus("0.1.0-alpha.2", available, DateTimeOffset.Now.AddHours(-2), string.Empty);
    }

    private static DaemonSettings Saved(bool check = true, DaemonSetting[]? locked = null)
    {
        return new DaemonSettings(false, true, new HashSet<DaemonSetting>(locked ?? [])) {
            Updates = new UpdatesSettings(check, false),
        };
    }
}
