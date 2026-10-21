using System;
using System.Collections.Generic;
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.UI.ViewModels;
using Contr.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Contr.Modules.Volumes.UI.Views;

/// <summary>The volumes, the containers that use them, and a details panel for the selected one.</summary>
public sealed partial class VolumesPage : Page, IHasViewModel
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

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    public static string Shown(string text, bool engineOpen)
    {
        return engineOpen ? text : string.Empty;
    }

    public static Visibility Visible(bool first, bool second)
    {
        return first && second ? Visibility.Visible : Visibility.Collapsed;
    }

    public static bool ShowsBoth(bool first, bool second)
    {
        return first && second;
    }

    public static Visibility WhenTrue(bool value)
    {
        return value ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility WhenText(string text)
    {
        return string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    public static bool IsWaiting(bool engineOpen, bool isLoaded, bool hasError)
    {
        return engineOpen && !isLoaded && !hasError;
    }

    public static Visibility WaitVisibility(bool engineOpen, bool isLoaded, bool hasError)
    {
        return IsWaiting(engineOpen, isLoaded, hasError) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>A volume can be deleted once it is known that no container mounts it.</summary>
    public static bool CanDelete(bool inUse, IReadOnlyList<VolumeMountDto>? mounts)
    {
        return !inUse && mounts is not null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Activate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Deactivate();
    }

    private void OnRowTapped(object sender, TappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is VolumeItem item)
        {
            ViewModel.Select(item);
        }
    }

    private async void OnCreate(object sender, RoutedEventArgs e)
    {
        await ViewModel.PromptAndCreateAsync();
    }

    private async void OnRemoveUnused(object sender, RoutedEventArgs e)
    {
        await ViewModel.RemoveUnusedAsync();
    }

    private void OnCopyName(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is { } item)
        {
            ViewModel.CopyName(item);
        }
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is { } item)
        {
            await ViewModel.RemoveAsync(item);
        }
    }
}
