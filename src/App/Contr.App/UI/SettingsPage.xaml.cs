using System;
using System.Collections.Generic;
using System.Globalization;
using Contr.App.UI.Onboarding;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>Settings in the design's tabs; the tabs the daemon cannot serve yet show their layout only.</summary>
public sealed partial class SettingsPage : Page, IHasViewModel
{
    private static readonly string[] _tabNames = [
        "General",
        "Resources",
        "Proxies and certificates",
        "Engine",
        "Kubernetes",
        "Updates",
    ];

    private readonly IServiceProvider _services;

    public SettingsPage(SettingsViewModel viewModel, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(services);
        ViewModel = viewModel;
        _services = services;

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SettingsViewModel ViewModel { get; }

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    public static Visibility When(int tab, int shown)
    {
        return tab == shown ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Whether a tab's message shows: its tab is open and it has something to say.</summary>
    public static bool Shows(int tab, int shown, bool hasMessage)
    {
        return hasMessage && tab == shown;
    }

    public static Visibility WhenShown(int tab, int shown, bool hasMessage)
    {
        return Shows(tab, shown, hasMessage) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The breadcrumb after the product: Settings, then the open tab.</summary>
    public static IReadOnlyList<string> Trail(int tab)
    {
        return ["Settings", _tabNames[Math.Clamp(tab, 0, _tabNames.Length - 1)]];
    }

    public static string Or(string text, string fallback)
    {
        return text.Length > 0 ? text : fallback;
    }

    public static string OrDash(string text)
    {
        return text.Length > 0 ? text : "—";
    }

    public static string ProcessorsText()
    {
        return $"This PC has {Environment.ProcessorCount.ToString(CultureInfo.CurrentCulture)} logical processors.";
    }

    public static bool Not(bool value)
    {
        return !value;
    }

    public static PillKind KindOf(PillItem? pill)
    {
        return pill?.Kind ?? PillKind.Plain;
    }

    public static string TextOf(PillItem? pill)
    {
        return pill?.Text ?? string.Empty;
    }

    public static bool Is(int value, int expected)
    {
        return value == expected;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Tabs.SelectedItem ??= GeneralTab;
        ViewModel.Activate();
        await ViewModel.LoadToolsAsync();
        await ViewModel.LoadWslAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Deactivate();
    }

    private async void OnResetSettings(object sender, RoutedEventArgs e)
    {
        await ViewModel.ResetSettingsAsync();
    }

    private void OnProxyModeChecked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag }
            && int.TryParse(tag, CultureInfo.InvariantCulture, out var mode))
        {
            ViewModel.Proxies.ModeIndex = mode;
        }
    }

    private void OnCopyVersions(object sender, RoutedEventArgs e)
    {
        ViewModel.CopyVersions();
    }

    private void OnDiscardProxyChanges(object sender, RoutedEventArgs e)
    {
        ViewModel.Proxies.Discard();
    }

    private async void OnApplyProxyChanges(object sender, RoutedEventArgs e)
    {
        await ViewModel.Proxies.ApplyAsync();
    }

    private void OnTabSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        ViewModel.TabIndex = sender.Items.IndexOf(sender.SelectedItem);
    }

    private void OnTrailClicked(object? sender, int index)
    {
        Tabs.SelectedItem = GeneralTab;
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
