using System;
using Contr.Modules.Volumes.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Volumes.UI.Views;

/// <summary>The engine's volumes, with creation and removal.</summary>
public sealed partial class VolumesPage : Page
{
    public VolumesPage(VolumesViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public VolumesViewModel ViewModel { get; }

    public static InfoBarSeverity SeverityFor(bool engineDown)
    {
        return engineDown ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
    }

    public static bool IsWaiting(bool isLoaded, bool hasError)
    {
        return !isLoaded && !hasError;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.StartWatching();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.StopWatching();
    }

    private async void OnCreate(object sender, RoutedEventArgs e)
    {
        await ViewModel.PromptAndCreateAsync();
    }

    private async void OnRemove(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is VolumeRow row)
        {
            await ViewModel.RemoveAsync(row);
        }
    }
}
