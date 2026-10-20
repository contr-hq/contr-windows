using System;
using Contr.App.UI;
using Contr.UI.Common.Helpers;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using WinUIEx;

namespace Contr.App.Services.Platform;

/// <summary>The notification-area icon: it opens the window, shows the engine status, and quits the app.</summary>
/// <remarks>Create and dispose it on the UI thread; the icon's message window belongs to that thread.</remarks>
public sealed class TrayService : IDisposable
{
    private const uint _iconId = 1;

    private readonly IDaemonConnection _daemon;
    private TrayIcon? _icon;
    private IDisposable? _statusSubscription;

    public TrayService(IDaemonConnection daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public void Show()
    {
        if (_icon is not null)
        {
            return;
        }

        _icon = new TrayIcon(_iconId, WindowHelper.AppIconPath, Tooltip(_daemon.Status));
        _icon.Selected += (_, _) => App.Current.ShowMainWindow();
        _icon.ContextMenu += (_, args) => args.Flyout = BuildMenu();
        _icon.IsVisible = true;

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _statusSubscription = _daemon.StatusChanges.Subscribe(
            status => dispatcher.TryEnqueue(() => UpdateTooltip(status)));
    }

    public void Dispose()
    {
        _statusSubscription?.Dispose();
        _statusSubscription = null;
        _icon?.Dispose();
        _icon = null;
    }

    private static string Tooltip(DaemonStatus status)
    {
        return $"Contr: {StatusSummary.From(status).Text}";
    }

    private static MenuFlyout BuildMenu()
    {
        var open = new MenuFlyoutItem { Text = "Open Contr" };
        open.Click += (_, _) => App.Current.ShowMainWindow();

        var quit = new MenuFlyoutItem { Text = "Quit Contr" };
        quit.Click += async (_, _) => await App.Current.QuitAsync();

        var menu = new MenuFlyout();
        menu.Items.Add(open);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(quit);

        return menu;
    }

    private void UpdateTooltip(DaemonStatus status)
    {
        if (_icon is not null)
        {
            _icon.Tooltip = Tooltip(status);
        }
    }
}
