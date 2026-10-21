using System;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Contr.Modules.Containers.UI.Views;

/// <summary>The containers list, grouped by Compose project, with per-row and per-project actions.</summary>
public sealed partial class ContainersPage : Page, IHasViewModel
{
    /// <summary>The page key of the Images module's page; modules know each other only by page key.</summary>
    private const string _imagesPageKey = "Images";

    private readonly INavigationService _navigation;
    private readonly IClipboardService _clipboard;

    public ContainersPage(
        ContainersViewModel viewModel, INavigationService navigation, IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(clipboard);
        ViewModel = viewModel;
        _navigation = navigation;
        _clipboard = clipboard;

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>The command the empty list suggests trying from a terminal.</summary>
    public static string TryCommand => "docker run -d -p 8080:80 nginx";

    public ContainersViewModel ViewModel { get; }

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    public static string ChevronGlyph(bool expanded)
    {
        return expanded ? "\uE70D" : "\uE76C";
    }

    public static string ExpandName(bool expanded)
    {
        return expanded ? "Collapse" : "Expand";
    }

    public static string Shown(string text, bool engineOpen)
    {
        return engineOpen ? text : string.Empty;
    }

    public static Visibility ShowsBoth(bool first, bool second)
    {
        return first && second ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility ErrorVisibility(bool hasError, bool engineOpen)
    {
        return ShowsError(hasError, engineOpen) ? Visibility.Visible : Visibility.Collapsed;
    }

    public static bool ShowsError(bool hasError, bool engineOpen)
    {
        return hasError && engineOpen;
    }

    public static Visibility ShowsNoMatches(bool engineOpen, bool hasRows, bool hasMatches)
    {
        return engineOpen && hasRows && !hasMatches ? Visibility.Visible : Visibility.Collapsed;
    }

    public static bool IsWaiting(bool engineOpen, bool isLoaded, bool hasError)
    {
        return engineOpen && !isLoaded && !hasError;
    }

    private static T? TagOf<T>(object sender)
        where T : class
    {
        return (sender as FrameworkElement)?.Tag as T;
    }

    private static bool IsInsideButton(DependencyObject source, object row)
    {
        var node = source;
        while (node is not null && !ReferenceEquals(node, row))
        {
            if (node is ButtonBase)
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Activate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Deactivate();
    }

    /// <summary>Opens the container unless the tap landed on one of the row's buttons or links.</summary>
    private void OnRowTapped(object sender, TappedRoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInsideButton(source, sender))
        {
            return;
        }

        if (TagOf<ContainerRow>(sender) is { } row)
        {
            ViewModel.OpenDetails(row, ContainerDetailTab.Logs);
        }
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerRow>(sender) is { } row)
        {
            await ViewModel.StartAsync(row);
        }
    }

    private async void OnStop(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerRow>(sender) is { } row)
        {
            await ViewModel.StopAsync(row);
        }
    }

    private async void OnRestart(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerRow>(sender) is { } row)
        {
            await ViewModel.RestartAsync(row);
        }
    }

    private async void OnRemove(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerRow>(sender) is { } row)
        {
            await ViewModel.RemoveAsync(row);
        }
    }

    private void OnLogs(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerRow>(sender) is { } row)
        {
            ViewModel.OpenDetails(row, ContainerDetailTab.Logs);
        }
    }

    private void OnTerminal(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerRow>(sender) is { } row)
        {
            ViewModel.OpenDetails(row, ContainerDetailTab.Exec);
        }
    }

    private void OnDetails(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerRow>(sender) is { } row)
        {
            ViewModel.OpenDetails(row, ContainerDetailTab.Inspect);
        }
    }

    private void OnCopyId(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerRow>(sender) is { } row)
        {
            ViewModel.CopyId(row);
        }
    }

    private void OnToggleGroup(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerGroup>(sender) is { } group)
        {
            group.IsExpanded = !group.IsExpanded;
        }
    }

    private async void OnStartAll(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerGroup>(sender) is { } group)
        {
            await ViewModel.StartAllAsync(group);
        }
    }

    private async void OnStopAll(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerGroup>(sender) is { } group)
        {
            await ViewModel.StopAllAsync(group);
        }
    }

    private async void OnRestartAll(object sender, RoutedEventArgs e)
    {
        if (TagOf<ContainerGroup>(sender) is { } group)
        {
            await ViewModel.RestartAllAsync(group);
        }
    }

    private async void OnPruneStopped(object sender, RoutedEventArgs e)
    {
        await ViewModel.PruneStoppedAsync();
    }

    private void OnViewExitedLogs(object sender, RoutedEventArgs e)
    {
        ViewModel.ViewExitedLogs();
    }

    private async void OnRestartExited(object sender, RoutedEventArgs e)
    {
        await ViewModel.RestartExitedAsync();
    }

    private void OnPullImage(object sender, RoutedEventArgs e)
    {
        _navigation.NavigateTo(_imagesPageKey);
    }

    private void OnCopyTryCommand(object sender, RoutedEventArgs e)
    {
        _clipboard.SetText(TryCommand);
    }
}
