using System;
using Contr.Modules.Images.UI.ViewModels;
using Contr.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Images.UI.Views;

/// <summary>The local images, with pulling, removal and pruning.</summary>
public sealed partial class ImagesPage : Page, IHasViewModel
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

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    public static string Shown(string text, bool engineOpen)
    {
        return engineOpen ? text : string.Empty;
    }

    public static string PlatformSuffix(string platform)
    {
        return $"· {platform}";
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

    public static bool IsWaiting(bool engineOpen, bool isLoaded, bool hasError)
    {
        return engineOpen && !isLoaded && !hasError;
    }

    private static ImageRow? RowOf(object sender)
    {
        return (sender as FrameworkElement)?.Tag as ImageRow;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Activate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Deactivate();
    }

    private async void OnPull(object sender, RoutedEventArgs e)
    {
        // A dialog's popup does not inherit the theme the window content requests.
        var dialog = new PullImageDialog { XamlRoot = XamlRoot, RequestedTheme = ActualTheme };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.PullAsync(dialog.Reference, dialog.Platform);
        }
    }

    private void OnCancelPull(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelPull();
    }

    private async void OnRemoveUnused(object sender, RoutedEventArgs e)
    {
        await ViewModel.RemoveUnusedAsync();
    }

    private async void OnRemoveDangling(object sender, RoutedEventArgs e)
    {
        await ViewModel.RemoveDanglingAsync();
    }

    private async void OnRemove(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row)
        {
            await ViewModel.RemoveAsync(row);
        }
    }

    private void OnCopyReference(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row)
        {
            ViewModel.CopyReference(row);
        }
    }

    private void OnCopyId(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row)
        {
            ViewModel.CopyId(row);
        }
    }
}
