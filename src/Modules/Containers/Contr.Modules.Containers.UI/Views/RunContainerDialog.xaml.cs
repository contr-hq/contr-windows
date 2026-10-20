using System;
using Contr.Modules.Containers.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Contr.Modules.Containers.UI.Views;

/// <summary>Runs a container from a local image; it stays open, its error on top, until the container runs.</summary>
public sealed partial class RunContainerDialog : ContentDialog
{
    private readonly Window _owner;

    /// <param name="owner">The window a folder picker belongs to.</param>
    public RunContainerDialog(RunContainerViewModel viewModel, Window owner)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(owner);
        ViewModel = viewModel;
        _owner = owner;

        InitializeComponent();
    }

    public RunContainerViewModel ViewModel { get; }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        args.Cancel = !await ViewModel.RunAsync();
        deferral.Complete();
    }

    private void OnAddPort(object sender, RoutedEventArgs e)
    {
        ViewModel.AddPort();
    }

    private void OnRemovePort(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is PortEntry entry)
        {
            ViewModel.RemovePort(entry);
        }
    }

    private void OnAddMount(object sender, RoutedEventArgs e)
    {
        ViewModel.AddMount();
    }

    private void OnRemoveMount(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is MountEntry entry)
        {
            ViewModel.RemoveMount(entry);
        }
    }

    private async void OnBrowse(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not MountEntry entry)
        {
            return;
        }

        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_owner));

        if (await picker.PickSingleFolderAsync() is { } folder)
        {
            entry.Source = folder.Path;
        }
    }

    private void OnCopyCommand(object sender, RoutedEventArgs e)
    {
        ViewModel.CopyCommand();
    }
}
