using System;
using System.Globalization;
using Contr.App.UI.Onboarding;
using Contr.UI.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>Settings in the design's tabs; the tabs the daemon cannot serve yet show their layout only.</summary>
public sealed partial class SettingsPage : Page, IHasViewModel
{
    private readonly IServiceProvider _services;

    public SettingsPage(SettingsViewModel viewModel, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(services);
        ViewModel = viewModel;
        _services = services;

        InitializeComponent();

        Loaded += OnLoaded;
    }

    public SettingsViewModel ViewModel { get; }

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    public static Visibility When(int tab, int shown)
    {
        return tab == shown ? Visibility.Visible : Visibility.Collapsed;
    }

    public static string Or(string text, string fallback)
    {
        return text.Length > 0 ? text : fallback;
    }

    public static string OrDash(string text)
    {
        return text.Length > 0 ? text : "—";
    }

    /// <summary>The installed version, else the bundled one.</summary>
    public static string ToolVersion(string installed, string bundled)
    {
        return installed.Length > 0 ? installed : bundled.Length > 0 ? $"{bundled} (bundled)" : "—";
    }

    public static string ProcessorsText()
    {
        return $"This PC has {Environment.ProcessorCount.ToString(CultureInfo.CurrentCulture)} logical processors.";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Tabs.SelectedItem ??= GeneralTab;
        await ViewModel.LoadToolsAsync();
        await ViewModel.LoadWslAsync();
    }

    private void OnTabSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        ViewModel.TabIndex = sender.Items.IndexOf(sender.SelectedItem);
    }

    private void OnRunSetup(object sender, RoutedEventArgs e)
    {
        _services
            .GetRequiredService<OnboardingWindow>()
            .Activate();
    }

    private void OnOpenKubernetes(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenKubernetes();
    }
}
