using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.Services.Platform;
using Contr.App.UI;
using Contr.SharedKernel;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class SettingsViewModelTests
{
    private readonly IThemeService _theme = Substitute.For<IThemeService>();
    private readonly IAccentService _accent = Substitute.For<IAccentService>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly ISignInStartup _signIn = Substitute.For<ISignInStartup>();
    private readonly ICliTools _cliTools = Substitute.For<ICliTools>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IWslDistributions _wsl = Substitute.For<IWslDistributions>();
    private readonly IDaemonSettings _settings = Substitute.For<IDaemonSettings>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    public SettingsViewModelTests()
    {
        _daemon.Status.Returns(DaemonStatus.Connecting);
    }

    [Fact]
    public void Constructor_MapsCurrentThemeToIndex()
    {
        _theme.Theme.Returns(ElementTheme.Dark);
        Assert.Equal(2, CreateViewModel().ThemeIndex);

        _theme.Theme.Returns(ElementTheme.Light);
        Assert.Equal(1, CreateViewModel().ThemeIndex);

        _theme.Theme.Returns(ElementTheme.Default);
        Assert.Equal(0, CreateViewModel().ThemeIndex);
    }

    [Fact]
    public void DaemonSettings_ShowTheRowsTheyBackAndTheirLocks()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.HasDaemonSettings);

        viewModel.Show(SettingsOf(servesDefaultPipe: true, DaemonSetting.ServeDefaultPipe));

        Assert.True(viewModel.HasDaemonSettings);
        Assert.True(viewModel.HasLockedSettings);
        Assert.True(viewModel.ServesDefaultPipe);
        Assert.False(viewModel.CanChangeDefaultPipe);
        _settings.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task DefaultPipe_SaysWhenItWaitsForTheEngine()
    {
        _settings
            .UpdateAsync(
                Arg.Is<DaemonSettingsEdit>(edit => edit != null && edit.ServeDefaultPipe == false),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DaemonSettingsChange(
                SettingsOf(servesDefaultPipe: false),
                new HashSet<DaemonSetting> { DaemonSetting.ServeDefaultPipe })));
        var viewModel = CreateViewModel();
        viewModel.Show(SettingsOf(servesDefaultPipe: true));

        await viewModel.ApplyDefaultPipeAsync(false);

        Assert.False(viewModel.ServesDefaultPipe);
        _notifications.Received(1).Show(
            "Restart the engine to apply the standard Docker pipe setting now.",
            InfoBarSeverity.Informational,
            "Applies at the next engine start");
    }

    [Fact]
    public async Task Reset_AsksFirstThenReturnsTheDefaults()
    {
        _dialogs
            .ShowConfirmAsync(default!, default!, default!, default!)
            .ReturnsForAnyArgs(true);
        _settings
            .ResetAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DaemonSettingsChange(
                SettingsOf(servesDefaultPipe: true), new HashSet<DaemonSetting>())));
        var viewModel = CreateViewModel();
        viewModel.Show(SettingsOf(servesDefaultPipe: false));

        await viewModel.ResetSettingsAsync();

        Assert.True(viewModel.ServesDefaultPipe);
        Assert.False(viewModel.IsResetting);
        _notifications.Received(1).Show(
            "The engine settings are back to their defaults.", InfoBarSeverity.Success, Arg.Any<string?>());
    }

    [Fact]
    public void ThemeIndexChange_AppliesMappedTheme()
    {
        var viewModel = CreateViewModel();

        viewModel.ThemeIndex = 2;
        viewModel.ThemeIndex = 1;
        viewModel.ThemeIndex = 0;

        _theme.Received(1).SetTheme(ElementTheme.Dark);
        _theme.Received(1).SetTheme(ElementTheme.Light);
        _theme.Received(1).SetTheme(ElementTheme.Default);
    }

    [Fact]
    public void AccentIndexChange_AppliesMappedSource()
    {
        var viewModel = CreateViewModel();

        viewModel.AccentIndex = 1;
        viewModel.AccentIndex = 0;

        _accent.Received(1).SetAccent(AccentSource.App);
        _accent.Received(1).SetAccent(AccentSource.Windows);
    }

    [Fact]
    public void Daemon_DescribesTheHandshake()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/contr-test", "0.1.0", 2, "Fake"));

        var viewModel = CreateViewModel();

        Assert.Equal("Version 0.1.0, API revision 2, Fake backend", viewModel.DaemonDescription);
        Assert.Equal("npipe:////./pipe/contr-test", viewModel.DaemonEndpoint);
    }

    [Fact]
    public void Daemon_SaysWhenNotConnected()
    {
        _daemon.Identity.Returns((DaemonIdentity?)null);

        Assert.Equal("Not connected.", CreateViewModel().DaemonDescription);
    }

    [Fact]
    public void StartAtSignIn_WritesOnlyAChange()
    {
        _signIn.IsEnabled.Returns(true);
        var viewModel = CreateViewModel();

        viewModel.StartAtSignIn = false;

        _signIn.Received(1).IsEnabled = false;
        _signIn.DidNotReceive().IsEnabled = true;
    }

    [Fact]
    public void Kubectl_ReadsAsTheKubernetesClient()
    {
        var tools = new CliToolsStatus([], string.Empty, IsOnPath: true, ShadowedBy: string.Empty);

        var row = CliToolRow.From(new CliTool("kubectl", "1.37.1", "1.37.1"), tools);

        Assert.Equal(("kubectl", "Kubernetes command-line client", "Ready"), (row.Name, row.Description, row.Status));
    }

    [Fact]
    public void ShowTools_ListsEachToolAndWarnsOfAShadowingDocker()
    {
        var viewModel = CreateViewModel();
        var tools = new CliToolsStatus(
            [
                new CliTool("buildx", "0.26.1", "0.25.0"),
                new CliTool("compose", "2.39.1", "2.39.1"),
                new CliTool("credential-wincred", "0.9.10", string.Empty),
                new CliTool("docker", "28.3.2", "28.3.2"),
            ],
            @"C:\Users\me\AppData\Local\Contr\bin",
            IsOnPath: true,
            ShadowedBy: @"C:\Tools\docker\docker.exe");

        viewModel.ShowTools(tools, string.Empty);

        Assert.Equal("Installed for your account", viewModel.ToolsTitle);
        Assert.Equal(
            ["docker", "docker compose", "docker buildx", "docker-credential-wincred"],
            viewModel.Tools.Select(row => row.Name));
        Assert.Equal(
            ["Second on PATH", "Ready", "0.26.1 bundled", "Not installed"],
            viewModel.Tools.Select(row => row.Status));
        Assert.Equal(new PillItem("4 tools · 3 need attention", PillKind.Caution), viewModel.ToolsSummary);
        Assert.Equal(
            @"Typing docker runs C:\Tools\docker\docker.exe first. Contr leaves it in place.", viewModel.PathWarning);
        Assert.Equal("Repair", viewModel.InstallToolsText);
        Assert.Contains(viewModel.Components, row => row is { Name: "Compose", Version: "2.39.1" });
    }

    [Fact]
    public void Components_ShowWhatTheDaemonReports()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 12, "WSL 2"));
        _daemon.Status.Returns(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty) {
            Details = new EngineDetails("29.0.1", "contr"),
        });

        var viewModel = CreateViewModel();

        Assert.Contains(viewModel.Components, row => row is { Name: "Engine", Version: "29.0.1" });
        Assert.Contains(
            viewModel.Components, row => row is { Name: "API revision", Version: "12", Status: "Compatible" });
        Assert.DoesNotContain(viewModel.Components, row => row.Name is "Kubernetes" or "WSL");
    }

    [Fact]
    public async Task LoadWsl_ListsTheDefaultFirstAndCountsTheConnected()
    {
        OfferWsl(
            Distribution("Debian", WslIntegrationState.Disabled),
            Distribution("Ubuntu-24.04", WslIntegrationState.Active) with { IsDefault = true },
            Distribution("openSUSE", WslIntegrationState.Disabled) with { Version = 1 });
        var viewModel = CreateViewModel();

        await viewModel.LoadWslAsync();

        Assert.Equal(["Ubuntu-24.04", "Debian", "openSUSE"], viewModel.Distributions.Select(item => item.Name));
        Assert.Equal("1 of 3 connected", viewModel.WslSummary);
        Assert.Equal([true, false, false], viewModel.Distributions.Select(item => item.IsOn));
        Assert.False(viewModel.Distributions[2].CanChange);
    }

    [Fact]
    public async Task LoadWsl_AsksNothingWithoutTheCapability()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/contr-test", "0.1.0", 4, "Fake"));

        await CreateViewModel().LoadWslAsync();

        await _wsl.DidNotReceiveWithAnyArgs().ListAsync(default);
    }

    [Fact]
    public async Task Switch_ShowsTheDaemonsAnswer()
    {
        OfferWsl(Distribution("Debian", WslIntegrationState.Disabled));
        _wsl
            .SetIntegrationAsync("Debian", true, Arg.Any<CancellationToken>())
            .Returns(Result.Success(Distribution("Debian", WslIntegrationState.Pending)));
        var viewModel = CreateViewModel();
        await viewModel.LoadWslAsync();

        viewModel.Distributions[0].IsOn = true;

        Assert.Equal(WslIntegrationState.Pending, viewModel.Distributions[0].Distribution.State);
        Assert.True(viewModel.Distributions[0].IsOn);
        Assert.False(viewModel.HasWslError);
    }

    [Fact]
    public async Task Switch_TurnsBackWhenTheDaemonRefuses()
    {
        OfferWsl(Distribution("Debian", WslIntegrationState.Disabled));
        _wsl
            .SetIntegrationAsync("Debian", true, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<WslDistribution>(new Error("Daemon.NotFound", "Debian is not registered")));
        var viewModel = CreateViewModel();
        await viewModel.LoadWslAsync();

        viewModel.Distributions[0].IsOn = true;

        Assert.False(viewModel.Distributions[0].IsOn);
        Assert.Equal("Debian was not turned on: Debian is not registered", viewModel.WslError);
    }

    private static WslDistribution Distribution(string name, WslIntegrationState state)
    {
        return new WslDistribution(name, 2, IsDefault: false, state, string.Empty);
    }

    private void OfferWsl(params WslDistribution[] distributions)
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/contr-test", "0.1.0", 4, "WSL 2") {
            Capabilities = new HashSet<DaemonCapability> { DaemonCapability.WslIntegration },
        });
        _wsl
            .ListAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<WslDistribution>>(distributions));
    }

    private static DaemonSettings SettingsOf(bool servesDefaultPipe, params DaemonSetting[] locked)
    {
        return new DaemonSettings(
            StartEngineWithDaemon: false, servesDefaultPipe, new HashSet<DaemonSetting>(locked));
    }

    private SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(
            _theme,
            _accent,
            _daemon,
            _signIn,
            _cliTools,
            _navigation,
            _wsl,
            _settings,
            _dialogs,
            _notifications,
            Substitute.For<IClipboardService>());
    }
}
