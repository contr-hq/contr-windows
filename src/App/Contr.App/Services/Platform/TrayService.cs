using System;
using Contr.App.UI;
using Contr.UI.Common.Helpers;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinUIEx;

namespace Contr.App.Services.Platform;

/// <summary>
/// The notification-area icon: a click opens the flyout with the engine and the running containers, a double click
/// opens the window.
/// </summary>
/// <remarks>Create and dispose it on the UI thread; the icon's message window belongs to that thread.</remarks>
public sealed class TrayService : IDisposable
{
    private const uint _iconId = 1;

    private readonly IDaemonConnection _daemon;
    private readonly INavigationService _navigation;
    private readonly Func<TrayViewModel> _createViewModel;
    private TrayIcon? _icon;
    private Flyout? _flyout;
    private IDisposable? _statusSubscription;

    public TrayService(IDaemonConnection daemon, INavigationService navigation, Func<TrayViewModel> createViewModel)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(createViewModel);
        _daemon = daemon;
        _navigation = navigation;
        _createViewModel = createViewModel;
    }

    public void Show()
    {
        if (_icon is not null)
        {
            return;
        }

        _icon = new TrayIcon(_iconId, WindowHelper.AppIconPath, Tooltip(_daemon.Status));
        _icon.Selected += (_, args) => args.Flyout = Flyout();
        _icon.ContextMenu += (_, args) => args.Flyout = Flyout();
        _icon.LeftDoubleClick += (_, _) => App.Current.ShowMainWindow();
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

    /// <summary>The flyout, built once; its panel follows the engine and the containers only while it is open.</summary>
    private Flyout Flyout()
    {
        if (_flyout is not null)
        {
            return _flyout;
        }

        var panel = new TrayPanel(_createViewModel());
        panel.OpenRequested += OnOpenRequested;
        panel.QuitRequested += async (_, _) => await App.Current.QuitAsync();

        _flyout = new Flyout { Content = panel, Placement = FlyoutPlacementMode.Top };
        _flyout.Opened += (_, _) => {
            ApplyWindowTheme(panel);
            panel.ViewModel.Start();
        };
        _flyout.Closed += (_, _) => panel.ViewModel.Stop();

        return _flyout;
    }

    /// <summary>The flyout lives in its own window, which does not follow the theme the main window requests.</summary>
    private static void ApplyWindowTheme(TrayPanel panel)
    {
        var theme = (App.MainWindow.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default;

        if (panel.XamlRoot?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }

        panel.RequestedTheme = theme;
    }

    private void OnOpenRequested(object? sender, string pageKey)
    {
        _icon?.CloseFlyout();
        App.Current.ShowMainWindow();

        if (pageKey.Length > 0)
        {
            _navigation.NavigateTo(pageKey);
        }
    }

    private void UpdateTooltip(DaemonStatus status)
    {
        if (_icon is not null)
        {
            _icon.Tooltip = Tooltip(status);
        }
    }
}
