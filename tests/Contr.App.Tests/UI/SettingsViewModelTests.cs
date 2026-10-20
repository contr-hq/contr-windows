using Contr.App.Services.Platform;
using Contr.App.UI;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml;
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
    public void ShowTools_DescribesInstalledToolsAndAShadowingDocker()
    {
        var viewModel = CreateViewModel();
        var tools = new CliToolsStatus(
            [new CliTool("docker", "28.3.2", "28.3.2"), new CliTool("compose", "2.39.1", "2.39.1")],
            @"C:\Users\me\AppData\Local\Contr\bin",
            IsOnPath: true,
            ShadowedBy: @"C:\Tools\docker.exe");

        viewModel.ShowTools(tools, string.Empty);

        Assert.Equal("Installed for your account", viewModel.ToolsTitle);
        Assert.Equal(@"C:\Users\me\AppData\Local\Contr\bin · on your PATH", viewModel.ToolsDetail);
        Assert.StartsWith(
            @"Another docker comes first on your PATH: C:\Tools\docker.exe.",
            viewModel.ToolsNote,
            System.StringComparison.Ordinal);
        Assert.Equal("Repair", viewModel.InstallToolsText);
        Assert.Equal(2, viewModel.Tools.Count);
    }

    private SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(_theme, _accent, _daemon, _signIn, _cliTools, _navigation);
    }
}
