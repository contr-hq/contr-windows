using System;
using System.IO;
using System.Runtime.InteropServices;
using Contr.App.UI;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using Windows.UI.Notifications;

namespace Contr.App.Services.Platform;

/// <summary>
/// Tells the user in a Windows notification when an operation ends while the main window is hidden, such as a pull
/// finishing after the window closed to the tray; a click on it opens the window.
/// </summary>
/// <remarks>
/// Uses Windows' own toasts under an app ID registered for the user, because the Windows App SDK's
/// <c>AppNotificationManager</c> fails to register where its Singleton package is missing, as for a self-contained
/// install. Start it on the UI thread.
/// </remarks>
public sealed partial class ActivityToasts : IDisposable
{
    /// <summary>The app ID Windows shows the notifications under.</summary>
    private const string _appId = "Contr.App";

    private const string _appIdKey = @"Software\Classes\AppUserModelId\" + _appId;

    private readonly IActivity _activity;
    private readonly ILogger<ActivityToasts> _logger;
    private DispatcherQueue? _dispatcher;
    private ToastNotifier? _notifier;
    private IDisposable? _subscription;

    public ActivityToasts(IActivity activity, ILogger<ActivityToasts> logger)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(logger);
        _activity = activity;
        _logger = logger;
    }

    public void Start()
    {
        if (_subscription is not null)
        {
            return;
        }

        _dispatcher = DispatcherQueue.GetForCurrentThread();
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(_appIdKey))
            {
                key.SetValue("DisplayName", "Contr");
                key.SetValue("IconUri", Path.Combine(AppContext.BaseDirectory, "Assets", "ContrLogo.png"));
            }

            _notifier = ToastNotificationManager.CreateToastNotifier(_appId);
        }
        catch (Exception exception) when (exception is COMException or IOException or UnauthorizedAccessException)
        {
            LogUnavailable(exception.Message);
        }

        _subscription = _activity.Finished.Subscribe(operation => _dispatcher?.TryEnqueue(() => Notify(operation)));
    }

    /// <summary>Removes the app ID <see cref="Start"/> registered; for the uninstall hook.</summary>
    public static void Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_appIdKey, throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException)
        {
            // The key names only the app; it stays in the user's profile.
        }
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private void Notify(DaemonOperation operation)
    {
        if (_notifier is null || !ActivityText.IsNews(operation) || App.Current.IsMainWindowVisible)
        {
            return;
        }

        var content = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
        var lines = content.GetElementsByTagName("text");
        lines[0].AppendChild(content.CreateTextNode(ActivityText.Outcome(operation)));
        lines[1].AppendChild(content.CreateTextNode(ActivityText.Detail(operation, DateTimeOffset.Now)));
        var toast = new ToastNotification(content);
        toast.Activated += (_, _) => _dispatcher?.TryEnqueue(() => App.Current.ShowMainWindow());
        try
        {
            _notifier.Show(toast);
        }
        catch (COMException exception)
        {
            LogUnavailable(exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Windows notifications are unavailable: {Reason}")]
    private partial void LogUnavailable(string reason);
}
