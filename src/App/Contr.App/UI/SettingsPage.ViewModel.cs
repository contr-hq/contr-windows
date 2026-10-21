using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Contr.App.Services.Platform;
using Contr.SharedKernel;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Engine;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>
/// Backs Settings: startup and closing, the command-line tools, appearance, the daemon and its settings; the tabs
/// whose settings the daemon cannot change yet show their layout only.
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
    private readonly IWslDistributions _wsl;
    private readonly IDaemonSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly IClipboardService _clipboard;
    private readonly DispatcherQueue? _dispatcher;
    private IDisposable? _subscription;
    private bool _isShowing;

    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    public partial int AccentIndex { get; set; }

    /// <summary>0 shows General; the others follow the tabs' order.</summary>
    [ObservableProperty]
    public partial int TabIndex { get; set; }

    [ObservableProperty]
    public partial bool StartAtSignIn { get; set; }

    /// <summary>Whether the daemon keeps settings: it is connected and at API revision 11 or later.</summary>
    [ObservableProperty]
    public partial bool HasDaemonSettings { get; set; }

    /// <summary>Whether an administrator fixes any of the daemon's settings.</summary>
    [ObservableProperty]
    public partial bool HasLockedSettings { get; set; }

    [ObservableProperty]
    public partial bool ServesDefaultPipe { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeDefaultPipe))]
    public partial bool IsDefaultPipeLocked { get; set; }

    [ObservableProperty]
    public partial bool IsResetting { get; set; }

    /// <summary>Such as <c>Installed for your account</c>; empty until the daemon answered.</summary>
    [ObservableProperty]
    public partial string ToolsTitle { get; set; }

    /// <summary>Such as <c>C:\Users\me\AppData\Local\Contr\bin · on your PATH</c>.</summary>
    [ObservableProperty]
    public partial string ToolsDetail { get; set; }

    /// <summary>Such as <c>4 tools · 1 needs attention</c>; empty until the daemon answered.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToolsSummary))]
    public partial PillItem? ToolsSummary { get; set; }

    /// <summary>Why typing <c>docker</c> may not run Contr's, such as another one earlier on PATH; empty when
    /// it does.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPathWarning))]
    public partial string PathWarning { get; set; }

    [ObservableProperty]
    public partial bool AreToolsInstalled { get; set; }

    [ObservableProperty]
    public partial bool CanInstallTools { get; set; }

    [ObservableProperty]
    public partial string InstallToolsText { get; set; }

    /// <summary>Such as <c>2 of 3 connected</c>; empty until the daemon answered.</summary>
    [ObservableProperty]
    public partial string WslSummary { get; set; }

    /// <summary>Why the distributions could not be listed or changed; empty when nothing failed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWslError))]
    public partial string WslError { get; set; }

    public SettingsViewModel(
        IThemeService themeService,
        IAccentService accentService,
        IDaemonConnection daemon,
        ISignInStartup signIn,
        ICliTools cliTools,
        INavigationService navigation,
        IWslDistributions wsl,
        IDaemonSettings settings,
        IDialogService dialogs,
        INotificationService notifications,
        IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(accentService);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(signIn);
        ArgumentNullException.ThrowIfNull(cliTools);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(wsl);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(clipboard);
        _themeService = themeService;
        _accentService = accentService;
        _daemon = daemon;
        _signIn = signIn;
        _cliTools = cliTools;
        _navigation = navigation;
        _wsl = wsl;
        _settings = settings;
        _dialogs = dialogs;
        _notifications = notifications;
        _clipboard = clipboard;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Autostart = new EngineAutostartViewModel(daemon, settings, notifications);
        Proxies = new ProxySettingsViewModel(settings, daemon, notifications);
        Engine = new EngineSettingsViewModel(settings, daemon, notifications);
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
        ToolsTitle = string.Empty;
        ToolsDetail = string.Empty;
        PathWarning = string.Empty;
        InstallToolsText = "Install";
        WslSummary = string.Empty;
        WslError = string.Empty;
        ContextName = daemon.Status.Details?.DockerContext ?? string.Empty;
        OffersWslIntegration = daemon.Identity?.Capabilities.Contains(DaemonCapability.WslIntegration) == true;
        OffersResourceLimits = daemon.Identity?.Capabilities.Contains(DaemonCapability.ResourceLimits) == true;
        AppVersion = ClientVersion();
        DaemonVersion = daemon.Identity is { } known
            ? $"{known.Version} · API revision {known.ApiRevision.ToString(CultureInfo.InvariantCulture)}"
            : "—";
        ShowComponents(null);
    }

    public bool HasAccentChoice { get; }

    /// <summary>Whether the engine starts with Contr.</summary>
    public EngineAutostartViewModel Autostart { get; }

    public bool CanChangeDefaultPipe => !IsDefaultPipeLocked;

    /// <summary>The Proxies and certificates tab.</summary>
    public ProxySettingsViewModel Proxies { get; }

    /// <summary>The Engine tab.</summary>
    public EngineSettingsViewModel Engine { get; }

    public string DaemonDescription { get; }

    public string DaemonEndpoint { get; }

    /// <summary>The Docker context the daemon made for the engine; empty while unknown.</summary>
    public string ContextName { get; }

    public bool HasContext => ContextName.Length > 0;

    public bool OffersWslIntegration { get; }

    public bool LacksWslIntegration => !OffersWslIntegration;

    public bool OffersResourceLimits { get; }

    public bool LacksResourceLimits => !OffersResourceLimits;

    /// <summary>This app's version, such as <c>0.1.0</c>.</summary>
    public string AppVersion { get; }

    /// <summary>Such as <c>0.1.0 · API revision 6</c>; a dash while no daemon answered.</summary>
    public string DaemonVersion { get; }

    public bool HasPathWarning => PathWarning.Length > 0;

    public bool HasToolsSummary => ToolsSummary is not null;

    public ObservableCollection<CliToolRow> Tools { get; } = [];

    /// <summary>The installed versions the app and the daemon report.</summary>
    public ObservableCollection<InstalledComponent> Components { get; } = [];

    /// <summary>Such as <c>4 components</c>.</summary>
    public string ComponentsSummary => $"{Components.Count.ToString(CultureInfo.CurrentCulture)} components";

    /// <summary>The user's WSL distributions, the default first.</summary>
    public ObservableCollection<WslDistributionItem> Distributions { get; } = [];

    public bool HasWslError => WslError.Length > 0;

    public static string Describe(DaemonIdentity? daemon)
    {
        return daemon is null
            ? "Not connected."
            : $"Version {daemon.Version}, API revision {daemon.ApiRevision}, {daemon.Backend} backend";
    }

    /// <summary>Follows the daemon's settings; call it when the page loads.</summary>
    public void Activate()
    {
        Autostart.Start();
        Proxies.Activate();
        Engine.Activate();
        _subscription ??= _settings.Changes.Subscribe(settings => {
            // Changes emits on a background thread.
            if (_dispatcher is null)
            {
                Show(settings);
            }
            else
            {
                _dispatcher.TryEnqueue(() => Show(settings));
            }
        });
    }

    public void Deactivate()
    {
        Autostart.Stop();
        Proxies.Deactivate();
        Engine.Deactivate();
        _subscription?.Dispose();
        _subscription = null;
    }

    /// <summary>Shows the daemon's settings; absent ones hide the rows only the daemon backs.</summary>
    public void Show(DaemonSettings? settings)
    {
        _isShowing = true;
        HasDaemonSettings = settings is not null;
        HasLockedSettings = settings?.Locked.Count > 0;
        ServesDefaultPipe = settings?.ServeDefaultPipe == true;
        IsDefaultPipeLocked = settings?.IsLocked(DaemonSetting.ServeDefaultPipe) == true;
        _isShowing = false;
        Proxies.Show(settings);
        Engine.Show(settings);
    }

    /// <summary>Asks the daemon for <paramref name="value"/>; a refusal turns the switch back and says why.</summary>
    public async Task ApplyDefaultPipeAsync(bool value)
    {
        var result = await _settings.UpdateAsync(
            new DaemonSettingsEdit { ServeDefaultPipe = value }, CancellationToken.None);

        if (result.IsFailure)
        {
            _isShowing = true;
            ServesDefaultPipe = !value;
            _isShowing = false;
            _notifications.Show(Reason(result.Error), InfoBarSeverity.Error, "The standard Docker pipe didn't change");

            return;
        }

        Show(result.Value.Settings);
        NotifyRestart(result.Value);
    }

    /// <summary>Asks first, then returns the daemon's settings the user changed to their defaults.</summary>
    public async Task ResetSettingsAsync()
    {
        const string message = "Contr returns the engine settings you changed to their defaults. Settings your "
            + "organization manages stay as they are.";

        if (!await _dialogs.ShowConfirmAsync("Reset the engine settings", message, "Reset", "Cancel"))
        {
            return;
        }

        IsResetting = true;
        try
        {
            var result = await _settings.ResetAsync(CancellationToken.None);

            if (result.IsFailure)
            {
                _notifications.Show(Reason(result.Error), InfoBarSeverity.Error, "The engine settings didn't change");

                return;
            }

            Show(result.Value.Settings);
            Autostart.Show(result.Value.Settings);

            if (!NotifyRestart(result.Value))
            {
                _notifications.Show("The engine settings are back to their defaults.", InfoBarSeverity.Success);
            }
        }
        finally
        {
            IsResetting = false;
        }
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
            ToolsSummary = null;
            PathWarning = string.Empty;
            CanInstallTools = false;

            return;
        }

        foreach (var tool in tools.Tools.OrderBy(CliToolRow.Rank))
        {
            Tools.Add(CliToolRow.From(tool, tools));
        }

        var bundles = tools.Tools.Any(tool => tool.BundledVersion.Length > 0);
        var attention = Tools.Count(row => row.NeedsAttention);
        AreToolsInstalled = tools.BinFolder.Length > 0;
        ToolsTitle = AreToolsInstalled ? "Installed for your account" : "Not installed";
        ToolsDetail = AreToolsInstalled
            ? $"{Shortened(tools.BinFolder)} · {(tools.IsOnPath ? "on your PATH" : "not on your PATH")}"
            : bundles ? "Contr can add docker and its plugins to your PATH." : "This daemon bundles no tools.";
        var count = Tools.Count == 1 ? "1 tool" : $"{Tools.Count} tools";
        ToolsSummary = attention switch {
            0 => new PillItem($"{count} · ready", PillKind.Good),
            1 => new PillItem($"{count} · 1 needs attention", PillKind.Caution),
            _ => new PillItem($"{count} · {attention} need attention", PillKind.Caution),
        };
        PathWarning = tools.ShadowedBy.Length > 0
            ? $"Typing docker runs {tools.ShadowedBy} first. Contr leaves it in place."
            : string.Empty;
        CanInstallTools = bundles;
        InstallToolsText = AreToolsInstalled ? "Repair" : "Install";
        ShowComponents(tools);
    }

    public void CopyVersions()
    {
        _clipboard.SetText(InstalledComponent.Details(Components));
    }

    private void ShowComponents(CliToolsStatus? tools)
    {
        Components.Clear();

        foreach (var row in InstalledComponent.From(AppVersion, _daemon.Identity, _daemon.Status.Details, tools))
        {
            Components.Add(row);
        }

        OnPropertyChanged(nameof(ComponentsSummary));
    }

    /// <summary>A path under the user's local application data as Windows Explorer accepts it, such as
    /// <c>%LOCALAPPDATA%\Contr\bin</c>.</summary>
    public static string Shortened(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return local.Length > 0 && path.StartsWith(local, StringComparison.OrdinalIgnoreCase)
            ? $"%LOCALAPPDATA%{path[local.Length..]}"
            : path;
    }

    /// <summary>Lists the user's WSL distributions where the daemon offers it; call it when the page loads.</summary>
    public async Task LoadWslAsync()
    {
        if (!OffersWslIntegration)
        {
            return;
        }

        var listed = await _wsl.ListAsync(CancellationToken.None);

        if (listed.IsFailure)
        {
            WslError = listed.Error.Message;

            return;
        }

        WslError = string.Empty;

        foreach (var item in Distributions)
        {
            item.Switched -= OnDistributionSwitched;
        }

        Distributions.Clear();

        foreach (var distribution in WslDistributionItem.Ordered(listed.Value))
        {
            var item = new WslDistributionItem(distribution);
            item.Switched += OnDistributionSwitched;
            Distributions.Add(item);
        }

        SummarizeWsl();
    }

    /// <summary>Asks the daemon for what <paramref name="item"/>'s switch now says; a refusal turns it back.</summary>
    public async Task ApplyAsync(WslDistributionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var wanted = item.IsOn;
        var result = await _wsl.SetIntegrationAsync(item.Name, wanted, CancellationToken.None);

        if (result.IsFailure)
        {
            item.SetQuietly(!wanted);
            WslError = $"{item.Name} was not turned {(wanted ? "on" : "off")}: {result.Error.Message}";
        }
        else
        {
            WslError = string.Empty;
            item.Show(result.Value);
        }

        SummarizeWsl();
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

    partial void OnServesDefaultPipeChanged(bool value)
    {
        if (!_isShowing)
        {
            _ = ApplyDefaultPipeAsync(value);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Deactivate();
        }
    }

    private static string Reason(Error error)
    {
        return error.Code == IDaemonSettings.SettingLockedCode
            ? "Your organization manages this setting."
            : error.Message;
    }

    /// <summary>Says when changed settings wait for the engine's next start.</summary>
    /// <returns>Whether anything waits.</returns>
    private bool NotifyRestart(DaemonSettingsChange change)
    {
        if (change.RestartRequired.Count == 0)
        {
            return false;
        }

        var names = change.RestartRequired
            .Order()
            .Select(setting => setting switch {
                DaemonSetting.ServeDefaultPipe => "the standard Docker pipe setting",
                _ => "starting the engine with Contr",
            });
        _notifications.Show(
            $"Restart the engine to apply {string.Join(" and ", names)} now.",
            InfoBarSeverity.Informational,
            "Applies at the next engine start");

        return true;
    }

    private void OnDistributionSwitched(object? sender, EventArgs e)
    {
        if (sender is WslDistributionItem item)
        {
            _ = ApplyAsync(item);
        }
    }

    private void SummarizeWsl()
    {
        var connected = Distributions.Count(item => item.Distribution.State == WslIntegrationState.Active);
        WslSummary = Distributions.Count == 0
            ? "No distros besides Contr's own"
            : $"{connected} of {Distributions.Count} connected";
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
