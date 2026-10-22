using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.Services.Platform;
using Contr.App.UI.Onboarding;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class OnboardingUpdateCheckTests
{
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly IDiagnostics _diagnostics = Substitute.For<IDiagnostics>();
    private readonly ICliTools _cliTools = Substitute.For<ICliTools>();
    private readonly IDaemonSettings _daemonSettings = Substitute.For<IDaemonSettings>();

    public OnboardingUpdateCheckTests()
    {
        _daemon.Status.Returns(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty));
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 21, "WSL 2"));
        _cliTools
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CliToolsStatus>(new Error("Daemon.FeatureUnavailable", "no tools")));
        _diagnostics
            .RunChecksAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<DiagnosticCheck>>([
                new(DiagnosticCheck.PrerequisitesId, "Engine prerequisites", DiagnosticStatus.Pass, string.Empty, []),
            ]));
    }

    [Fact]
    public async Task ToolsStep_SaysWhatTheUpdateCheckSends_AndTurnsItOff()
    {
        Settings(check: true);
        _daemonSettings
            .UpdateAsync(default!, default)
            .ReturnsForAnyArgs(Result.Success(new DaemonSettingsChange(Saved(false), new HashSet<DaemonSetting>())));
        var viewModel = Create();

        await viewModel.NextAsync();
        await viewModel.NextAsync();

        Assert.True(viewModel.OffersUpdateCheck);
        Assert.True(viewModel.ChecksForUpdates);
        Assert.Equal(
            "Contr checks GitHub once a day for new releases; turn this off in Settings → Updates.",
            viewModel.UpdateCheckText);

        viewModel.ChecksForUpdates = false;
        await viewModel.NextAsync();

        await _daemonSettings.Received(1).UpdateAsync(
            Arg.Is<DaemonSettingsEdit>(edit => edit != null && edit.UpdatesCheck == false),
            Arg.Any<CancellationToken>());
        Assert.Contains(viewModel.Summary, row => row.Name == "Update check");
    }

    [Fact]
    public async Task ALockedUpdateCheck_StaysAsTheOrganizationSetIt()
    {
        Settings(check: false, locked: true);
        var viewModel = Create();

        await viewModel.NextAsync();
        await viewModel.NextAsync();

        Assert.True(viewModel.IsUpdateCheckLocked);
        Assert.False(viewModel.CanChooseUpdateCheck);
        Assert.False(viewModel.ChecksForUpdates);
        await viewModel.NextAsync();
        await _daemonSettings.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task AnOlderDaemon_HasNoUpdateCheckToShow()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 18, "WSL 2"));
        Settings(check: true);
        var viewModel = Create();

        await viewModel.NextAsync();
        await viewModel.NextAsync();

        Assert.False(viewModel.OffersUpdateCheck);
    }

    private void Settings(bool check, bool locked = false)
    {
        _daemonSettings.Changes.Returns(Observable.Return<DaemonSettings?>(Saved(check, locked)));
    }

    private static DaemonSettings Saved(bool check, bool locked = false)
    {
        HashSet<DaemonSetting> locks = locked ? [DaemonSetting.UpdatesCheck] : [];

        return new DaemonSettings(false, true, locks) { Updates = new UpdatesSettings(check, false) };
    }

    private OnboardingViewModel Create()
    {
        return new OnboardingViewModel(
            _daemon,
            _diagnostics,
            _cliTools,
            Substitute.For<IWslDistributions>(),
            Substitute.For<ISignInStartup>(),
            Substitute.For<ILocalSettingsService>(),
            Substitute.For<IClipboardService>(),
            _daemonSettings);
    }
}
