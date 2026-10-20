using System;
using System.Collections.Specialized;
using System.Linq;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Contr.Modules.Containers.UI.Views;

/// <summary>One container: its state and actions, and its logs, inspection and stats.</summary>
public sealed partial class ContainerDetailPage : Page, IHasViewModel
{
    /// <summary>The log follows new output while the view is within this distance of the end.</summary>
    private const double _followSlack = 48;

    private static readonly FontFamily _codeFont = new("Cascadia Mono,Consolas");

    private ScrollViewer? _logScroller;

    public ContainerDetailPage(ContainerDetailViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ContainerDetailViewModel ViewModel { get; }

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    public static Visibility When(ContainerDetailTab tab, string shown)
    {
        return tab.ToString() == shown ? Visibility.Visible : Visibility.Collapsed;
    }

    public static FontFamily FactFont(bool isCode)
    {
        return isCode ? _codeFont : FontFamily.XamlAutoFontFamily;
    }

    /// <summary>Client notes are dimmed against the container's own output.</summary>
    public static double LineOpacity(bool isNote)
    {
        return isNote ? 0.6 : 1;
    }

    public static Visibility WhenKind(JsonTokenKind kind, string shown)
    {
        return kind.ToString() == shown ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The top of a rate chart: the largest rate with headroom, and never zero.</summary>
    public static double Headroom(double maximum)
    {
        return maximum > 0 ? maximum * 1.25 : 1;
    }

    public static string StatsWaitText(bool isRunning, string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return error;
        }

        return isRunning ? "Waiting for the first sample." : "Stats appear while the container runs.";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Tabs.SelectedItem = ViewModel.Tab switch {
            ContainerDetailTab.Inspect => InspectTab,
            ContainerDetailTab.Stats => StatsTab,
            _ => LogsTab,
        };
        ViewModel.Logs.Lines.CollectionChanged += OnLogLinesChanged;
        ViewModel.Logs.MatchSelected += OnMatchSelected;
        ViewModel.Activate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Logs.Lines.CollectionChanged -= OnLogLinesChanged;
        ViewModel.Logs.MatchSelected -= OnMatchSelected;
        ViewModel.Deactivate();
    }

    private void OnTabSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        ViewModel.Tab = sender.SelectedItem switch {
            var item when item == InspectTab => ContainerDetailTab.Inspect,
            var item when item == StatsTab => ContainerDetailTab.Stats,
            _ => ContainerDetailTab.Logs,
        };
    }

    private void OnTrailClicked(object? sender, int index)
    {
        ViewModel.OpenList();
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        await ViewModel.StartAsync();
    }

    private async void OnStop(object sender, RoutedEventArgs e)
    {
        await ViewModel.StopAsync();
    }

    private async void OnRestart(object sender, RoutedEventArgs e)
    {
        await ViewModel.RestartAsync();
    }

    private async void OnRemove(object sender, RoutedEventArgs e)
    {
        await ViewModel.RemoveAsync();
    }

    /// <summary>Enter moves to the next match, Shift+Enter to the previous one.</summary>
    private void OnLogSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        var shift = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (shift)
        {
            ViewModel.Logs.PreviousMatch();
        }
        else
        {
            ViewModel.Logs.NextMatch();
        }

        e.Handled = true;
    }

    private void OnMatchSelected(object? sender, LogLine line)
    {
        LogList.ScrollIntoView(line, ScrollIntoViewAlignment.Leading);
    }

    private void OnCopyLogs(object sender, RoutedEventArgs e)
    {
        ViewModel.Logs.CopyAll();
    }

    private void OnCollapseJson(object sender, RoutedEventArgs e)
    {
        ViewModel.Inspect.CollapseAll();
    }

    private void OnCopyJson(object sender, RoutedEventArgs e)
    {
        ViewModel.Inspect.CopyJson();
    }

    /// <summary>Keeps the newest output in view unless the reader scrolled up.</summary>
    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || ViewModel.Logs.Lines.Count == 0)
        {
            return;
        }

        _logScroller ??= FindScroller(LogList);

        if (_logScroller is null || _logScroller.VerticalOffset >= _logScroller.ScrollableHeight - _followSlack)
        {
            LogList.ScrollIntoView(ViewModel.Logs.Lines[^1]);
        }
    }

    private static ScrollViewer? FindScroller(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is ScrollViewer scroller)
            {
                return scroller;
            }

            if (FindScroller(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
