using System;
using Contr.Modules.Images.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Images.UI.Views;

/// <summary>The local images, with pulling and removal.</summary>
public sealed partial class ImagesPage : Page
{
    public ImagesPage(ImagesViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ImagesViewModel ViewModel { get; }

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

    private async void OnPull(object sender, RoutedEventArgs e)
    {
        await ViewModel.PromptAndPullAsync();
    }

    private void OnCancelPull(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelPull();
    }

    private async void OnRemove(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ImageRow row)
        {
            await ViewModel.RemoveAsync(row);
        }
    }
}
