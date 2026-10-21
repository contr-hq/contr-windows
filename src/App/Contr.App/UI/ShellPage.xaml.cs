using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Helpers;
using Contr.UI.Common.Icons;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Contr.UI.Contracts.Services.Search;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>The main window's content: the navigation pane, the page frame and the status bar.</summary>
public sealed partial class ShellPage : Page
{
    private static readonly TimeSpan _searchPause = TimeSpan.FromMilliseconds(200);

    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly IReadOnlyList<IModule> _modules;
    private readonly IDaemonConnection _daemon;
    private readonly IServiceProvider _services;
    private readonly ISubscription _subscription;
    private readonly GlobalSearch _search;
    private readonly Dictionary<string, NavigationViewItem> _navItemsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<NavigationViewItem, NavigationItem> _itemsByContainer = [];
    private bool _isSyncingSelection;
    private string? _currentPageKey;
    private NavigationViewItemSeparator? _toolsSeparator;
    private CancellationTokenSource? _searching;

    public ShellPage(
        ShellViewModel viewModel,
        INavigationService navigationService,
        INotificationService notificationService,
        IEnumerable<IModule> modules,
        IDaemonConnection daemon,
        IServiceProvider services,
        ISubscription subscription,
        GlobalSearch search)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(search);

        ViewModel = viewModel;
        _navigationService = navigationService;
        _notificationService = notificationService;
        _modules = [.. modules];
        _daemon = daemon;
        _services = services;
        _subscription = subscription;
        _search = search;

        InitializeComponent();
    }

    public ShellViewModel ViewModel { get; }

    /// <summary>The custom title bar, handed to the window so it becomes the draggable caption.</summary>
    public TitleBar TitleBar => AppTitleBar;

    public static object? ToolTipFor(string detail)
    {
        return string.IsNullOrEmpty(detail) ? null : detail;
    }

    private void OnShellPageLoaded(object sender, RoutedEventArgs e)
    {
        _navigationService.Initialize(NavFrame);
        _navigationService.Register([.. HostNavigation.ShellPages]);

        var items = _modules
            .SelectMany(module => module.GetNavigationItems())
            .Concat(HostNavigation.Items)
            .OrderBy(item => item.Placement)
            .ThenBy(item => item.Order)
            .ToList();

        foreach (var item in items)
        {
            AddNavItem(item);
        }

        _subscription.Add(_navigationService.OnNavigated.Subscribe(OnNavigated));

        // StatusChanges and UsageChanges emit on a background thread.
        _subscription.Add(_daemon.StatusChanges.Subscribe(
            status => DispatcherQueue.TryEnqueue(() => ViewModel.ApplyStatus(status))));
        _subscription.Add(_daemon.UsageChanges.Subscribe(
            usage => DispatcherQueue.TryEnqueue(() => ViewModel.ApplyUsage(usage))));

        NavView.SelectedItem = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault();
    }

    private void AddNavItem(NavigationItem item)
    {
        var pageKey = _navigationService.GetPageKey(item.PageType);
        var badge = new TextBlock {
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Resources["StatusBarTextStyle"],
        };
        var navItem = new NavigationViewItem {
            Tag = pageKey,
            Content = NavItemContent(item.Title, badge),
            Icon = Icon(item.IconGlyph),
        };
        _itemsByContainer[navItem] = item;
        AutomationProperties.SetAutomationId(navItem, pageKey);
        AutomationProperties.SetName(navItem, item.Title);
        _navItemsByKey[pageKey] = navItem;

        if (item.Badge is { } source)
        {
            // Badge sources emit on background threads.
            _subscription.Add(source(_services).Subscribe(
                text => DispatcherQueue.TryEnqueue(() => badge.Text = text)));
        }

        if (item.Placement == NavigationItemPlacement.Footer)
        {
            NavView.FooterMenuItems.Add(navItem);

            return;
        }

        if (item.Placement == NavigationItemPlacement.Tools && _toolsSeparator is null)
        {
            _toolsSeparator = new NavigationViewItemSeparator();
            NavView.MenuItems.Add(_toolsSeparator);
        }

        NavView.MenuItems.Add(navItem);
    }

    private static FontIcon Icon(string glyph)
    {
        var style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["IconStyle"];

        return new FontIcon { Glyph = glyph, Style = style, FontSize = 20 };
    }

    /// <summary>Gives the selected item its selected glyph, such as a filled one, and every other its own.</summary>
    private void ShowSelectedIcons(object? selected)
    {
        foreach (var (container, item) in _itemsByContainer)
        {
            if (container.Icon is FontIcon icon)
            {
                icon.Glyph = ReferenceEquals(container, selected) && item.SelectedIconGlyph is { } filled
                    ? filled
                    : item.IconGlyph;
            }
        }
    }

    private void OnTitleBarPaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private static Grid NavItemContent(string title, TextBlock badge)
    {
        var content = new Grid { ColumnSpacing = 8 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(new TextBlock { Text = title, TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(badge, 1);
        content.Children.Add(badge);

        return content;
    }

    private void OnNavViewSelectionChanged(
        NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        ShowSelectedIcons(args.SelectedItemContainer);

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

    /// <summary>Returns to a section's page when its selected item is clicked again, as from a detail page.</summary>
    private void OnNavViewItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var pageKey = args.IsSettingsInvoked
            ? HostNavigation.SettingsPageKey
            : (args.InvokedItemContainer as NavigationViewItem)?.Tag as string;

        if (pageKey is not null
            && ReferenceEquals(args.InvokedItemContainer, NavView.SelectedItem)
            && pageKey != _currentPageKey)
        {
            _navigationService.NavigateTo(pageKey);
        }
    }

    /// <summary>Notifications show under the breadcrumb of the page that is open.</summary>
    private void ShowNoticesOnceLoaded(object content)
    {
        if (content is not FrameworkElement page)
        {
            return;
        }

        if (page.IsLoaded)
        {
            ShowNoticesOn(page);
        }
        else
        {
            page.Loaded += OnPageLoaded;
        }
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        var page = (FrameworkElement)sender;
        page.Loaded -= OnPageLoaded;
        ShowNoticesOn(page);
    }

    private void ShowNoticesOn(FrameworkElement page)
    {
        if (VisualTree.FindDescendant<PageHeader>(page) is { } header)
        {
            _notificationService.Initialize(header.Notice);
        }
    }

    /// <summary>Follows back and programmatic navigation in the pane without navigating again.</summary>
    private void OnNavigated(NavigationEvent e)
    {
        _currentPageKey = e.PageKey;
        ShowNoticesOnceLoaded(e.Content);

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

    private async void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        if (_searching is { } previous)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        var searching = new CancellationTokenSource();
        _searching = searching;

        try
        {
            // Waits for a pause in typing, so each keystroke does not ask the engine again.
            await Task.Delay(_searchPause, searching.Token);
            var hits = await _search.SearchAsync(sender.Text, searching.Token);
            sender.ItemsSource = hits.Count > 0 || sender.Text.Trim().Length == 0
                ? hits
                : new[] { NoMatches(sender.Text) };
        }
        catch (OperationCanceledException)
        {
            // Typing again replaced this search.
        }
    }

    private void OnSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var hit = args.ChosenSuggestion as SearchHit
            ?? (sender.ItemsSource as IReadOnlyList<SearchHit>)?.FirstOrDefault();

        if (hit is null || hit.PageKey.Length == 0)
        {
            return;
        }

        _search.Open(hit);
        sender.Text = string.Empty;
        sender.ItemsSource = null;
    }

    private static SearchHit NoMatches(string text)
    {
        return new SearchHit(
            $"Nothing matches \u201C{text.Trim()}\u201D",
            "Containers, images, volumes and networks",
            ContrIcons.Search,
            string.Empty,
            []);
    }

    private void OnTitleBarBackRequested(TitleBar sender, object args)
    {
        _navigationService.GoBack();
    }

    /// <summary>Undoes what loading did, so a second Loaded does not double the pane.</summary>
    private void OnShellPageUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_subscription.Unsubscribed)
        {
            _subscription.Unsubscribe();
        }

        _searching?.Cancel();
        _searching?.Dispose();
        _searching = null;

        foreach (var navItem in _navItemsByKey.Values)
        {
            NavView.MenuItems.Remove(navItem);
            NavView.FooterMenuItems.Remove(navItem);
        }

        NavView.MenuItems.Remove(_toolsSeparator);
        _toolsSeparator = null;
        _navItemsByKey.Clear();
    }
}
