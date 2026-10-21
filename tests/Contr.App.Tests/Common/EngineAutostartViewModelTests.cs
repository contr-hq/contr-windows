using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.SharedKernel;
using Contr.UI.Common.Engine;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.Common;

public sealed class EngineAutostartViewModelTests
{
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly IDaemonSettings _settings = Substitute.For<IDaemonSettings>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    [Fact]
    public void WithoutDaemonSettings_TheClientsOwnChoiceApplies()
    {
        _daemon.StartsEngineOnConnect.Returns(true);
        var viewModel = Create();

        Assert.True(viewModel.IsOn);

        viewModel.IsOn = false;

        _daemon.Received(1).StartsEngineOnConnect = false;
        _settings.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public void WithDaemonSettings_ShowsTheDaemonsValueAndLock()
    {
        _daemon.StartsEngineOnConnect.Returns(true);
        var viewModel = Create();

        viewModel.Show(Settings(startWithDaemon: false, DaemonSetting.StartEngineWithDaemon));

        Assert.False(viewModel.IsOn);
        Assert.True(viewModel.IsLocked);
        Assert.False(viewModel.CanChange);
        _daemon.DidNotReceive().StartsEngineOnConnect = Arg.Any<bool>();
    }

    [Fact]
    public async Task ARefusal_TurnsTheSwitchBackAndSaysWhy()
    {
        _settings
            .UpdateAsync(
                Arg.Is<DaemonSettingsEdit>(edit => edit != null && edit.StartEngineWithDaemon == true),
                Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DaemonSettingsChange>(
                new Error(IDaemonSettings.SettingLockedCode, "engine.start_with_daemon is locked")));
        var viewModel = Create();
        viewModel.Show(Settings(startWithDaemon: false));

        viewModel.IsOn = true;
        await Task.Yield();

        Assert.False(viewModel.IsOn);
        _notifications.Received(1).Show(
            "Your organization manages this setting.", InfoBarSeverity.Error, "The engine's startup didn't change");
    }

    [Fact]
    public async Task AChange_ShowsWhatTheDaemonKept()
    {
        _settings
            .UpdateAsync(
                Arg.Is<DaemonSettingsEdit>(edit => edit != null && edit.StartEngineWithDaemon == true),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DaemonSettingsChange(
                Settings(startWithDaemon: true), new HashSet<DaemonSetting>())));
        var viewModel = Create();
        viewModel.Show(Settings(startWithDaemon: false));

        await viewModel.ApplyAsync(true);

        Assert.True(viewModel.IsOn);
        _notifications.DidNotReceiveWithAnyArgs().Show(default!, default, default);
    }

    private static DaemonSettings Settings(bool startWithDaemon, params DaemonSetting[] locked)
    {
        return new DaemonSettings(startWithDaemon, ServeDefaultPipe: true, new HashSet<DaemonSetting>(locked));
    }

    private EngineAutostartViewModel Create()
    {
        return new EngineAutostartViewModel(_daemon, _settings, _notifications);
    }
}
