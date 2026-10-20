using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Contr.App.Services.Platform;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml;

namespace Contr.App.UI;

/// <summary>
/// Backs Settings: startup and closing, the command-line tools, appearance and the daemon; the tabs whose settings
/// the daemon cannot change yet show their layout only.
/// </summary>
public sealed partial class SettingsViewModel : ViewModel
{
    private const string _kubernetesPageKey = "Kubernetes";

    private readonly IThemeService _themeService;
    private readonly IAccentService _accentService;
    private readonly IDaemonConnection _daemon;
    private readonly ISignInStartup _signIn;
    private readonly ICliTools _cliTools;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    public partial int AccentIndex { get; set; }

    /// <summary>0 shows General; the others follow the tabs' order.</summary>
    [ObservableProperty]
    public partial int TabIndex { get; set; }

    [ObservableProperty]
    public partial bool StartAtSignIn { get; set; }

    [ObservableProperty]
    public partial bool StartsEngine { get; set; }

    /// <summary>Such as <c>Installed for your account</c>; empty until the daemon answered.</summary>
    [ObservableProperty]
    public partial string ToolsTitle { get; set; }

    /// <summary>Such as <c>C:\Users\me\AppData\Local\Contr\bin · on your PATH</c>.</summary>
    [ObservableProperty]
    public partial string ToolsDetail { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToolsNote))]
    public partial string ToolsNote { get; set; }

    [ObservableProperty]
    public partial bool AreToolsInstalled { get; set; }

    [ObservableProperty]
    public partial bool CanInstallTools { get; set; }

    [ObservableProperty]
    public partial string InstallToolsText { get; set; }

    public SettingsViewModel(
        IThemeService themeService,
        IAccentService accentService,
        IDaemonConnection daemon,
        ISignInStartup signIn,
        ICliTools cliTools,
        INavigationService navigation)
    {
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(accentService);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(signIn);
        ArgumentNullException.ThrowIfNull(cliTools);
        ArgumentNullException.ThrowIfNull(navigation);
        _themeService = themeService;
        _accentService = accentService;
        _daemon = daemon;
        _signIn = signIn;
        _cliTools = cliTools;
        _navigation = navigation;
        ThemeIndex = themeService.Theme switch {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0,
        };
        AccentIndex = accentService.Accent == AccentSource.App ? 1 : 0;
        HasAccentChoice = accentService.HasAppAccent;
        DaemonDescription = Describe(daemon.Identity);
        DaemonEndpoint = daemon.Identity?.Endpoint ?? string.Empty;
        StartAtSignIn = signIn.IsEnabled;
        StartsEngine = daemon.StartsEngineOnConnect;
        ToolsTitle = string.Empty;
        ToolsDetail = string.Empty;
        ToolsNote = string.Empty;
        InstallToolsText = "Install";
        ContextName = daemon.Status.Details?.DockerContext ?? string.Empty;
        OffersWslIntegration = daemon.Identity?.Capabilities.Contains(DaemonCapability.WslIntegration) == true;
        OffersResourceLimits = daemon.Identity?.Capabilities.Contains(DaemonCapability.ResourceLimits) == true;
        VersionText = daemon.Identity is { } identity
            ? $"Contr {ClientVersion()} · daemon {identity.Version} · API revision {identity.ApiRevision}"
            : $"Contr {ClientVersion()}";
    }

    public bool HasAccentChoice { get; }

    public string DaemonDescription { get; }

    public string DaemonEndpoint { get; }

    /// <summary>The Docker context the daemon made for the engine; empty while unknown.</summary>
    public string ContextName { get; }

    public bool HasContext => ContextName.Length > 0;

    public bool OffersWslIntegration { get; }

    public bool LacksWslIntegration => !OffersWslIntegration;

    public bool OffersResourceLimits { get; }

    public bool LacksResourceLimits => !OffersResourceLimits;

    /// <summary>Such as <c>Contr 0.1.0 · daemon 0.1.0 · API revision 3</c>.</summary>
    public string VersionText { get; }

    public bool HasToolsNote => ToolsNote.Length > 0;

    public ObservableCollection<CliTool> Tools { get; } = [];

    public static string Describe(DaemonIdentity? daemon)
    {
        return daemon is null
            ? "Not connected."
            : $"Version {daemon.Version}, API revision {daemon.ApiRevision}, {daemon.Backend} backend";
    }

    /// <summary>Reads the command-line tools' state; call it when the page loads.</summary>
    public async Task LoadToolsAsync()
    {
        var tools = await _cliTools.GetAsync(CancellationToken.None);
        ShowTools(tools.IsSuccess ? tools.Value : null, tools.IsFailure ? tools.Error.Message : string.Empty);
    }

    public void ShowTools(CliToolsStatus? tools, string error)
    {
        Tools.Clear();

        if (tools is null)
        {
            ToolsTitle = "Unknown";
            ToolsDetail = error;
            CanInstallTools = false;

            return;
        }

        foreach (var tool in tools.Tools)
        {
            Tools.Add(tool);
        }

        var bundles = tools.Tools.Count > 0 && tools.Tools[0].BundledVersion.Length > 0;
        AreToolsInstalled = tools.BinFolder.Length > 0;
        ToolsTitle = AreToolsInstalled ? "Installed for your account" : "Not installed";
        ToolsDetail = AreToolsInstalled
            ? $"{tools.BinFolder} · {(tools.IsOnPath ? "on your PATH" : "not on your PATH")}"
            : bundles ? "Contr can add docker and its plugins to your PATH." : "This daemon bundles no tools.";
        ToolsNote = tools.ShadowedBy.Length > 0
            ? $"Another docker comes first on your PATH: {tools.ShadowedBy}. Typing docker runs that one, and Contr "
                + "leaves it in place."
            : string.Empty;
        CanInstallTools = bundles;
        InstallToolsText = AreToolsInstalled ? "Repair" : "Install";
    }

    public void OpenKubernetes()
    {
        _navigation.NavigateTo(_kubernetesPageKey);
    }

    partial void OnThemeIndexChanged(int value)
    {
        _themeService.SetTheme(value switch {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default,
        });
    }

    partial void OnAccentIndexChanged(int value)
    {
        _accentService.SetAccent(value == 1 ? AccentSource.App : AccentSource.Windows);
    }

    partial void OnStartAtSignInChanged(bool value)
    {
        if (_signIn.IsEnabled != value)
        {
            _signIn.IsEnabled = value;
        }
    }

    partial void OnStartsEngineChanged(bool value)
    {
        if (_daemon.StartsEngineOnConnect != value)
        {
            _daemon.StartsEngineOnConnect = value;
        }
    }

    private static string ClientVersion()
    {
        var version = typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        return version?.InformationalVersion ?? "unknown";
    }

    [RelayCommand]
    private async Task InstallToolsAsync()
    {
        CanInstallTools = false;
        var installed = await _cliTools.InstallAsync(CancellationToken.None);
        ShowTools(
            installed.IsSuccess ? installed.Value : null, installed.IsFailure ? installed.Error.Message : string.Empty);
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var folder = FileLoggerProvider.LogDirectory;
        Directory.CreateDirectory(folder);

        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }
}
