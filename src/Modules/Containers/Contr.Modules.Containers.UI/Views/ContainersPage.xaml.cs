using System;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Contr.Modules.Containers.UI.Views;

/// <summary>The containers list with per-row lifecycle actions and a log panel.</summary>
public sealed partial class ContainersPage : Page
{
    public ContainersPage(ContainersViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ContainersViewModel ViewModel { get; }

    public static Brush StateBrush(ContainerState state)
    {
        var key = state switch {
            ContainerState.Running => "SystemFillColorSuccessBrush",
            ContainerState.Dead => "SystemFillColorCriticalBrush",
            ContainerState.Exited or ContainerState.Unknown => "SystemFillColorNeutralBrush",
            _ => "SystemFillColorCautionBrush",
        };

        return (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];
    }

    public static string Subtitle(string project, string shortId)
    {
        return project.Length > 0 ? $"{project} · {shortId}" : shortId;
    }

    public static InfoBarSeverity SeverityFor(bool engineDown)
    {
        return engineDown ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
    }

    public static bool IsWaiting(bool isLoaded, bool hasError)
    {
        return !isLoaded && !hasError;
    }

    private static ContainerRow? RowOf(object sender)
    {
        return (sender as FrameworkElement)?.Tag as ContainerRow;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.StartWatching();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.StopWatching();
        ViewModel.CloseLogs();
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row)
        {
            await ViewModel.StartAsync(row);
        }
    }

    private async void OnStop(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row)
        {
            await ViewModel.StopAsync(row);
        }
    }

    private async void OnRestart(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row)
        {
            await ViewModel.RestartAsync(row);
        }
    }

    private async void OnRemove(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row)
        {
            await ViewModel.RemoveAsync(row);
        }
    }

    private void OnLogs(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row)
        {
            ViewModel.OpenLogs(row);
        }
    }

    private void OnCloseLogs(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseLogs();
    }

    /// <summary>Keeps the newest output in view as it arrives.</summary>
    private void OnLogsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        LogsScroller.ChangeView(null, LogsScroller.ScrollableHeight, null, disableAnimation: true);
    }
}
