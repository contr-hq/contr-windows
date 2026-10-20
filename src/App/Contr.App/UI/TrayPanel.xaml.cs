using System;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Containers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>The tray flyout's content; the tray service supplies what each command does.</summary>
public sealed partial class TrayPanel : UserControl
{
    public TrayPanel(TrayViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();
    }

    /// <summary>Raised with the page key the user asked to see; empty opens the window as it was.</summary>
    public event EventHandler<string>? OpenRequested;

    public event EventHandler? QuitRequested;

    public TrayViewModel ViewModel { get; }

    public static string ProjectText(string project)
    {
        return project.Length > 0 ? $"· {project}" : string.Empty;
    }

    public static PillKind Pill(StatusKind kind)
    {
        return PillView.For(kind);
    }

    public static Visibility WhenLink(Uri? link)
    {
        return link is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnStopContainer(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is RunningContainer container)
        {
            await ViewModel.StopContainerAsync(container);
        }
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        OpenRequested?.Invoke(this, string.Empty);
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        OpenRequested?.Invoke(this, HostNavigation.SettingsPageKey);
    }

    private void OnShowContainers(object sender, RoutedEventArgs e)
    {
        OpenRequested?.Invoke(this, HostNavigation.ContainersPageKey);
    }

    private void OnTroubleshoot(object sender, RoutedEventArgs e)
    {
        OpenRequested?.Invoke(this, HostNavigation.TroubleshootPageKey);
    }

    private async void OnStartEngine(object sender, RoutedEventArgs e)
    {
        await ViewModel.StartEngineAsync();
    }

    private async void OnRestartEngine(object sender, RoutedEventArgs e)
    {
        await ViewModel.RestartEngineAsync();
    }

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        QuitRequested?.Invoke(this, EventArgs.Empty);
    }
}
