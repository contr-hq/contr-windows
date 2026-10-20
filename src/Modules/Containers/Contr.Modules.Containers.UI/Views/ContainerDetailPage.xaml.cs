using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Helpers;
using Contr.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Contr.Modules.Containers.UI.Views;

/// <summary>One container: its state and actions, and its logs, inspection and stats.</summary>
public sealed partial class ContainerDetailPage : Page, IHasViewModel
{
    /// <summary>The log follows new output while the view is within this distance of the end.</summary>
    private const double _followSlack = 48;

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

    public static Visibility WhenFact(FactKind kind, string shown)
    {
        return kind.ToString() == shown ? Visibility.Visible : Visibility.Collapsed;
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

    public static Visibility WhenPort(bool hasPort, bool isRunning)
    {
        return hasPort && isRunning ? Visibility.Visible : Visibility.Collapsed;
    }

    public static string OpenText(PortLink? port)
    {
        return port is null ? string.Empty : $"Open {port.Address}";
    }

    public static string OrDash(string text)
    {
        return string.IsNullOrEmpty(text) ? "—" : text;
    }

    public static double Latest(IReadOnlyList<double> values)
    {
        return values is { Count: > 0 } ? Math.Min(values[^1], 100) : 0;
    }

    public static Visibility WhenNoFacts(IReadOnlyList<InspectFact> facts)
    {
        return facts is { Count: > 0 } ? Visibility.Collapsed : Visibility.Visible;
    }

    public static Visibility WhenNone(IReadOnlyList<PortLink> ports)
    {
        return ports is { Count: > 0 } ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Whether a tab's message shows: its tab is open and it has something to say.</summary>
    public static bool Shows(ContainerDetailTab tab, string shown, bool hasMessage)
    {
        return hasMessage && tab.ToString() == shown;
    }

    public static Visibility WhenShown(ContainerDetailTab tab, string shown, bool hasMessage)
    {
        return Shows(tab, shown, hasMessage) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Shows the log's hint while no line arrived and nothing failed.</summary>
    public static Visibility WhenQuiet(int lines, bool hasError)
    {
        return lines == 0 && !hasError ? Visibility.Visible : Visibility.Collapsed;
    }

    public static PillKind TerminalPill(bool isConnected)
    {
        return isConnected ? PillKind.Good : PillKind.Hollow;
    }

    public static string ShowText(bool isShown)
    {
        return isShown ? "Hide" : "Show";
    }

    public static string StatsWaitTitle(bool isRunning, bool hasError)
    {
        if (!isRunning)
        {
            return "Not running";
        }

        return hasError ? "No usage to show" : "Waiting for the first sample";
    }

    public static string StatsWaitText(bool isRunning)
    {
        return isRunning
            ? "CPU, memory, network and disk appear here as the engine reports them."
            : "Usage appears while the container runs.";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Tabs.SelectedItem = ViewModel.Tab switch {
            ContainerDetailTab.Inspect => InspectTab,
            ContainerDetailTab.Exec => ExecTab,
            ContainerDetailTab.Stats => StatsTab,
            _ => LogsTab,
        };
        ViewModel.Logs.Lines.CollectionChanged += OnLogLinesChanged;
        ViewModel.Logs.MatchSelected += OnMatchSelected;
        ViewModel.PropertyChanged += OnViewModelChanged;
        TerminalHost.Ready += OnTerminalReady;
        TerminalHost.Copied += OnTerminalCopied;
        ViewModel.Activate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Logs.Lines.CollectionChanged -= OnLogLinesChanged;
        ViewModel.Logs.MatchSelected -= OnMatchSelected;
        ViewModel.PropertyChanged -= OnViewModelChanged;
        TerminalHost.Ready -= OnTerminalReady;
        TerminalHost.Copied -= OnTerminalCopied;
        TerminalHost.Detach();
        ViewModel.Deactivate();
    }

    private void OnTabSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        ViewModel.Tab = sender.SelectedItem switch {
            var item when item == InspectTab => ContainerDetailTab.Inspect,
            var item when item == ExecTab => ContainerDetailTab.Exec,
            var item when item == StatsTab => ContainerDetailTab.Stats,
            _ => ContainerDetailTab.Logs,
        };
    }

    /// <summary>The terminal starts when the exec tab first shows a running container.</summary>
    private async void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ContainerDetailViewModel.Tab) or nameof(ContainerDetailViewModel.IsRunning)
            && ViewModel.Tab == ContainerDetailTab.Exec)
        {
            await TerminalHost.AttachAsync(ViewModel.Terminal);
            await ViewModel.ConnectTerminalAsync();
            TerminalHost.FocusTerminal();
        }
    }

    private async void OnTerminalReady(object? sender, EventArgs e)
    {
        if (ViewModel.Tab == ContainerDetailTab.Exec)
        {
            await ViewModel.ConnectTerminalAsync();
            TerminalHost.FocusTerminal();
        }
    }

    private void OnTerminalCopied(object? sender, string text)
    {
        ViewModel.CopyText(text);
    }

    private void OnCopyId(object sender, RoutedEventArgs e)
    {
        ViewModel.CopyId();
    }

    private async void OnOpenPort(object sender, RoutedEventArgs e)
    {
        if (ViewModel.FirstPort?.Uri is { } uri)
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    private void OnClearTerminal(object sender, RoutedEventArgs e)
    {
        ViewModel.Terminal.Clear();
    }

    private void OnCopyTerminal(object sender, RoutedEventArgs e)
    {
        TerminalHost.RequestCopy();
    }

    private async void OnConnectTerminal(object sender, RoutedEventArgs e)
    {
        await TerminalHost.AttachAsync(ViewModel.Terminal);
        await ViewModel.ConnectTerminalAsync();
        TerminalHost.FocusTerminal();
    }

    private async void OnDisconnectTerminal(object sender, RoutedEventArgs e)
    {
        await ViewModel.Terminal.DisconnectAsync();
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

        _logScroller ??= VisualTree.FindDescendant<ScrollViewer>(LogList);

        if (_logScroller is null || _logScroller.VerticalOffset >= _logScroller.ScrollableHeight - _followSlack)
        {
            LogList.ScrollIntoView(ViewModel.Logs.Lines[^1]);
        }
    }

}
