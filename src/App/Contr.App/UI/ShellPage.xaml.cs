using System;
using System.Collections.Generic;
using System.Linq;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Contr.App.UI;

/// <summary>The main window's content: the navigation pane, the page frame and the status bar.</summary>
public sealed partial class ShellPage : Page
{
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly IReadOnlyList<IModule> _modules;
    private readonly IDaemonConnection _daemon;
    private readonly ISubscription _subscription;
    private readonly Dictionary<string, NavigationViewItem> _navItemsByKey = new(StringComparer.Ordinal);
    private bool _isSyncingSelection;

    public ShellPage(
        ShellViewModel viewModel,
        INavigationService navigationService,
        INotificationService notificationService,
        IEnumerable<IModule> modules,
        IDaemonConnection daemon,
        ISubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(subscription);

        ViewModel = viewModel;
        _navigationService = navigationService;
        _notificationService = notificationService;
        _modules = [.. modules];
        _daemon = daemon;
        _subscription = subscription;

        InitializeComponent();
    }

    public ShellViewModel ViewModel { get; }

    /// <summary>The custom title bar, handed to the window so it becomes the draggable caption.</summary>
    public TitleBar TitleBar => AppTitleBar;

    public static Brush ToneBrush(StatusTone tone)
    {
        var key = tone switch {
            StatusTone.Good => "SystemFillColorSuccessBrush",
            StatusTone.Busy => "SystemFillColorCautionBrush",
            StatusTone.Problem => "SystemFillColorCriticalBrush",
            _ => "SystemFillColorNeutralBrush",
        };

        return (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];
    }

    public static object? ToolTipFor(string detail)
    {
        return string.IsNullOrEmpty(detail) ? null : detail;
    }

    private void OnShellPageLoaded(object sender, RoutedEventArgs e)
    {
        _navigationService.Initialize(NavFrame);
        _navigationService.Register([.. HostNavigation.ShellPages]);
        _notificationService.Initialize(AppNotificationBar);

        foreach (var item in _modules.SelectMany(module => module.GetNavigationItems()))
        {
            AddNavItem(item);
        }

        _subscription.Add(_navigationService.OnNavigated.Subscribe(OnNavigated));

        // StatusChanges emits on a background thread.
        _subscription.Add(_daemon.StatusChanges.Subscribe(
            status => DispatcherQueue.TryEnqueue(() => ViewModel.ApplyStatus(status))));

        NavView.SelectedItem = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault();
    }

    private void AddNavItem(NavigationItem item)
    {
        var pageKey = _navigationService.GetPageKey(item.PageType);
        var navItem = new NavigationViewItem {
            Tag = pageKey,
            Content = item.Title,
            Icon = new FontIcon { Glyph = item.IconGlyph },
        };
        AutomationProperties.SetAutomationId(navItem, pageKey);
        _navItemsByKey[pageKey] = navItem;

        if (item.Placement == NavigationItemPlacement.Footer)
        {
            NavView.FooterMenuItems.Add(navItem);
        }
        else
        {
            NavView.MenuItems.Add(navItem);
        }
    }

    private void OnNavViewSelectionChanged(
        NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        if (args.IsSettingsSelected)
        {
            _navigationService.NavigateTo(HostNavigation.SettingsPageKey);

            return;
        }

        if (args.SelectedItem is NavigationViewItem { Tag: string pageKey })
        {
            _navigationService.NavigateTo(pageKey);
        }
    }

    /// <summary>Follows back and programmatic navigation in the pane without navigating again.</summary>
    private void OnNavigated(NavigationEvent e)
    {
        var container = e.PageKey == HostNavigation.SettingsPageKey
            ? NavView.SettingsItem as NavigationViewItem
            : _navItemsByKey.GetValueOrDefault(e.PageKey);

        if (container is null)
        {
            return;
        }

        _isSyncingSelection = true;
        try
        {
            ViewModel.Selected = container;
            NavView.SelectedItem = container;
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void OnTitleBarBackRequested(TitleBar sender, object args)
    {
        _navigationService.GoBack();
    }

    private void OnTitleBarPaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    /// <summary>Undoes what loading did, so a second Loaded does not double the pane.</summary>
    private void OnShellPageUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_subscription.Unsubscribed)
        {
            _subscription.Unsubscribe();
        }

        foreach (var navItem in _navItemsByKey.Values)
        {
            NavView.MenuItems.Remove(navItem);
            NavView.FooterMenuItems.Remove(navItem);
        }

        _navItemsByKey.Clear();
    }
}
