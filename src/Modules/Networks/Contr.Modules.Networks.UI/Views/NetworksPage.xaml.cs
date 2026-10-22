using System;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.UI.ViewModels;
using Contr.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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

    public static Visibility WaitVisibility(bool engineOpen, bool isLoaded, bool hasError)
    {
        return IsWaiting(engineOpen, isLoaded, hasError) ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility WhenText(string? text)
    {
        return string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    public static string OrDash(string? text)
    {
        return string.IsNullOrEmpty(text) ? "—" : text;
    }

    /// <summary>The engine keeps its own networks; Compose removes the networks it created.</summary>
    public static Visibility Not(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }

    public static string DisconnectName(string container)
    {
        return $"Disconnect {container}";
    }

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

    /// <summary>Picks one of the containers not on the network and connects it.</summary>
    private async void OnConnect(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is not { } network)
        {
            return;
        }

        var resources = Microsoft.UI.Xaml.Application.Current.Resources;
        var containers = new ComboBox {
            Header = "Container",
            ItemsSource = ViewModel.Others,
            DisplayMemberPath = nameof(NetworkContainerDto.Name),
            SelectedIndex = 0,
            MinWidth = 320,
        };
        AutomationProperties.SetAutomationId(containers, "ConnectContainerChoice");

        // A dialog's popup does not inherit the theme the window content requests.
        var dialog = new ContentDialog {
            Title = $"Connect a container to {network.Name}",
            Content = containers,
            PrimaryButtonText = "Connect",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Style = (Style)resources["DefaultContentDialogStyle"],
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary
            && containers.SelectedItem is NetworkContainerDto container)
        {
            await ViewModel.ConnectAsync(container);
        }
    }

    private async void OnDisconnect(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is NetworkMemberDto member)
        {
            await ViewModel.DisconnectAsync(member);
        }
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is { } row)
        {
            await ViewModel.RemoveAsync(row);
        }
    }
}
