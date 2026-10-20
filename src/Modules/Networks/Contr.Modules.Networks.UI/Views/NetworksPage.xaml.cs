using System;
using Contr.Modules.Networks.UI.ViewModels;
using Contr.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Contr.Modules.Networks.UI.Views;

/// <summary>The engine's networks and a details panel with the containers connected to the selected one.</summary>
public sealed partial class NetworksPage : Page, IHasViewModel
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

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    public static string Shown(string text, bool engineOpen)
    {
        return engineOpen ? text : string.Empty;
    }

    public static string ProjectText(string project)
    {
        return $"{project} project";
    }

    public static Visibility Visible(bool first, bool second)
    {
        return first && second ? Visibility.Visible : Visibility.Collapsed;
    }

    public static bool ShowsBoth(bool first, bool second)
    {
        return first && second;
    }

    public static bool IsWaiting(bool engineOpen, bool isLoaded, bool hasError)
    {
        return engineOpen && !isLoaded && !hasError;
    }

    /// <summary>The engine keeps its own networks; Compose removes the networks it created.</summary>
    public static bool CanDelete(bool canRemove, bool hasProject)
    {
        return canRemove && !hasProject;
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
        if ((sender as FrameworkElement)?.Tag is NetworkRow row)
        {
            ViewModel.Select(row);
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

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is { } row)
        {
            await ViewModel.RemoveAsync(row);
        }
    }
}
