using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Contr.UI.Common.Helpers;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Contr.App.Services.Platform;

/// <summary>
/// Applies and persists the accent choice by writing the <c>SystemAccentColor</c> family into
/// application resources, where the framework's accent brushes resolve their colours.
/// </summary>
/// <remarks>The keys must exist before the first window resolves a brush, so this runs first at startup.</remarks>
public sealed class AccentService : IAccentService
{
    private const string _accentSettingKey = "AppAccent";

    private readonly ILocalSettingsService _localSettings;
    private readonly IMainWindowProvider _mainWindow;
    private readonly Subject<AccentSource> _accentChanged = new();
    private readonly UISettings _systemColors = new();
    private readonly Color? _appAccent;
    private DispatcherQueue? _dispatcher;

    public AccentService(
        ILocalSettingsService localSettings,
        IMainWindowProvider mainWindow,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(localSettings);
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(configuration);

        _localSettings = localSettings;
        _mainWindow = mainWindow;
        _appAccent = AccentPalette.TryParse(configuration["Branding:Accent"], out var color)
            ? color
            : null;
    }

    public AccentSource Accent { get; private set; } = AccentSource.Windows;

    public bool HasAppAccent => _appAccent is not null;

    public IObservable<AccentSource> OnAccentChanged => _accentChanged.AsObservable();

    public void Initialize()
    {
        Accent = _localSettings.Read<AccentSource>(_accentSettingKey);

        // These keys shadow the framework's own accent, so following Windows is this service's job.
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _systemColors.ColorValuesChanged += OnSystemColorsChanged;

        Apply();
    }

    public void SetAccent(AccentSource source)
    {
        Accent = source;
        _localSettings.Save(_accentSettingKey, source);
        Apply();
        _accentChanged.OnNext(source);
    }

    private void OnSystemColorsChanged(UISettings sender, object args)
    {
        if (Accent != AccentSource.Windows)
        {
            return;
        }

        // The event arrives on a system thread, and everything Apply touches is the UI thread's.
        _dispatcher?.TryEnqueue(Apply);
    }

    private void Apply()
    {
        var shades = Accent == AccentSource.App && _appAccent is Color accent
            ? AccentPalette.Shades(accent)
            : WindowsShades();

        // Fully qualified because the root namespace has an Application layer, which shadows the
        // XAML type's namespace from inside the App project.
        var resources = Microsoft.UI.Xaml.Application.Current.Resources;
        resources["SystemAccentColor"] = shades.Base;
        resources["SystemAccentColorLight1"] = shades.Light1;
        resources["SystemAccentColorLight2"] = shades.Light2;
        resources["SystemAccentColorLight3"] = shades.Light3;
        resources["SystemAccentColorDark1"] = shades.Dark1;
        resources["SystemAccentColorDark2"] = shades.Dark2;
        resources["SystemAccentColorDark3"] = shades.Dark3;

        NudgeTheme();
    }

    /// <summary>The ramp Windows derives from the user's accent.</summary>
    private AccentShades WindowsShades()
    {
        return new AccentShades(
            _systemColors.GetColorValue(UIColorType.Accent),
            _systemColors.GetColorValue(UIColorType.AccentLight1),
            _systemColors.GetColorValue(UIColorType.AccentLight2),
            _systemColors.GetColorValue(UIColorType.AccentLight3),
            _systemColors.GetColorValue(UIColorType.AccentDark1),
            _systemColors.GetColorValue(UIColorType.AccentDark2),
            _systemColors.GetColorValue(UIColorType.AccentDark3));
    }

    /// <summary>
    /// Makes resolved theme resources resolve again by flipping the root's requested theme away and
    /// back; before the shell exists nothing is resolved yet.
    /// </summary>
    private void NudgeTheme()
    {
        if (_mainWindow.MainWindow?.Content is not FrameworkElement root)
        {
            return;
        }

        var requested = root.RequestedTheme;
        root.RequestedTheme = root.ActualTheme == ElementTheme.Dark
            ? ElementTheme.Light
            : ElementTheme.Dark;
        root.RequestedTheme = requested;
    }
}
