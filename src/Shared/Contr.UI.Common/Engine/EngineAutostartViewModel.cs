using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Contr.UI.Common.Engine;

/// <summary>
/// "Start the engine with Contr" as a switch: the daemon's <see cref="DaemonSetting.StartEngineWithDaemon"/>, or the
/// client's own choice where the daemon keeps no settings.
/// </summary>
public sealed partial class EngineAutostartViewModel : ObservableObject
{
    private readonly IDaemonConnection _daemon;
    private readonly IDaemonSettings _settings;
    private readonly INotificationService _notifications;
    private readonly DispatcherQueue? _dispatcher;
    private IDisposable? _subscription;
    private bool _servesSettings;
    private bool _isShowing;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    /// <summary>Whether an administrator fixes the setting.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChange))]
    public partial bool IsLocked { get; set; }

    public EngineAutostartViewModel(
        IDaemonConnection daemon, IDaemonSettings settings, INotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(notifications);
        _daemon = daemon;
        _settings = settings;
        _notifications = notifications;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Show(null);
    }

    public bool CanChange => !IsLocked;

    /// <summary>Follows the daemon's settings; call it when the page loads.</summary>
    public void Start()
    {
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

    public void Stop()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    /// <summary>Shows <paramref name="settings"/>, or the client's own choice where they are absent.</summary>
    public void Show(DaemonSettings? settings)
    {
        _servesSettings = settings is not null;
        _isShowing = true;
        IsOn = settings?.StartEngineWithDaemon ?? _daemon.StartsEngineOnConnect;
        IsLocked = settings?.IsLocked(DaemonSetting.StartEngineWithDaemon) == true;
        _isShowing = false;
    }

    /// <summary>Asks the daemon for <paramref name="value"/>; a refusal turns the switch back and says why.</summary>
    public async Task ApplyAsync(bool value)
    {
        var result = await _settings.SetAsync(DaemonSetting.StartEngineWithDaemon, value, CancellationToken.None);

        if (result.IsSuccess)
        {
            Show(result.Value.Settings);

            return;
        }

        _isShowing = true;
        IsOn = !value;
        _isShowing = false;
        var reason = result.Error.Code == IDaemonSettings.SettingLockedCode
            ? "Your organization manages this setting."
            : result.Error.Message;
        _notifications.Show(reason, InfoBarSeverity.Error, "The engine's startup didn't change");
    }

    partial void OnIsOnChanged(bool value)
    {
        if (_isShowing)
        {
            return;
        }

        if (_servesSettings)
        {
            _ = ApplyAsync(value);
        }
        else if (_daemon.StartsEngineOnConnect != value)
        {
            _daemon.StartsEngineOnConnect = value;
        }
    }
}
