using System;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml;

namespace Contr.App.UI;

/// <summary>
/// Presents the theme choice as a 0/1/2 index and the accent choice as a 0/1 index, applies each
/// through its service, and describes the connected daemon.
/// </summary>
public sealed partial class SettingsViewModel : ViewModel
{
    private readonly IThemeService _themeService;
    private readonly IAccentService _accentService;

    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    public partial int AccentIndex { get; set; }

    public bool HasAccentChoice { get; }

    public string DaemonDescription { get; }

    public string DaemonEndpoint { get; }

    public SettingsViewModel(
        IThemeService themeService, IAccentService accentService, IDaemonConnection daemon)
    {
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(accentService);
        ArgumentNullException.ThrowIfNull(daemon);
        _themeService = themeService;
        _accentService = accentService;
        ThemeIndex = themeService.Theme switch {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0,
        };
        AccentIndex = accentService.Accent == AccentSource.App ? 1 : 0;
        HasAccentChoice = accentService.HasAppAccent;
        DaemonDescription = Describe(daemon.Identity);
        DaemonEndpoint = daemon.Identity?.Endpoint ?? string.Empty;
    }

    public static string Describe(DaemonIdentity? daemon)
    {
        return daemon is null
            ? "Not connected."
            : $"Version {daemon.Version}, API revision {daemon.ApiRevision}, {daemon.Backend} backend";
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

    [RelayCommand]
    private void OpenLogFolder()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Contr",
            "logs");
        Directory.CreateDirectory(folder);

        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }
}
