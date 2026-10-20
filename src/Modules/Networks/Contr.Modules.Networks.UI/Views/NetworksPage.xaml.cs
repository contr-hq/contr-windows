using System;
using Contr.Modules.Networks.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Networks.UI.Views;

/// <summary>The engine's networks, with removal of user-defined ones.</summary>
public sealed partial class NetworksPage : Page
{
    public NetworksPage(NetworksViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public NetworksViewModel ViewModel { get; }

    public static InfoBarSeverity SeverityFor(bool engineDown)
    {
        return engineDown ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
    }

    public static bool IsWaiting(bool isLoaded, bool hasError)
    {
        return !isLoaded && !hasError;
    }

    public static string RemoveToolTip(bool canRemove)
    {
        return canRemove ? "Delete" : "The engine's own networks cannot be deleted";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.StartWatching();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.StopWatching();
    }

    private async void OnRemove(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is NetworkRow row)
        {
            await ViewModel.RemoveAsync(row);
        }
    }
}
