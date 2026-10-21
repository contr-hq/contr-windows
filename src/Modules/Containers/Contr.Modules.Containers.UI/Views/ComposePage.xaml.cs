using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI;

namespace Contr.Modules.Containers.UI.Views;

/// <summary>Compose projects as the containers' labels describe them, with their services and merged logs.</summary>
public sealed partial class ComposePage : Page, IHasViewModel
{
    /// <summary>Service colours readable on both the light and the dark log background.</summary>
    private static readonly SolidColorBrush[] _serviceBrushes = [
        new(Color.FromArgb(255, 0x3F, 0xB9, 0x50)),
        new(Color.FromArgb(255, 0xD2, 0x99, 0x22)),
        new(Color.FromArgb(255, 0x58, 0xA6, 0xFF)),
        new(Color.FromArgb(255, 0xF8, 0x51, 0x49)),
        new(Color.FromArgb(255, 0xBC, 0x8C, 0xFF)),
        new(Color.FromArgb(255, 0x39, 0xC5, 0xCF)),
    ];

    private bool _isShowingFilters;

    public ComposePage(ComposeViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ComposeViewModel ViewModel { get; }

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    public static Brush ServiceBrush(int color)
    {
        return _serviceBrushes[Math.Abs(color) % _serviceBrushes.Length];
    }

    public static string Prefix(string service)
    {
        return $"{service} |";
    }

    public static string ServiceName(string service, string name)
    {
        return service.Length > 0 ? service : name;
    }

    /// <summary>The published host ports, such as <c>8080, 8443</c>; a dash without any.</summary>
    public static string PortsText(IReadOnlyList<PortLink> ports)
    {
        return ports.Count == 0
            ? "—"
            : string.Join(", ", ports.Select(port => port.Address[(port.Address.LastIndexOf(':') + 1)..]));
    }

    public static Visibility WhenNone(int count)
    {
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility ShowsBoth(bool first, bool second)
    {
        return first && second ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility ShowsEmpty(bool engineOpen, bool isLoaded, bool hasProjects)
    {
        return engineOpen && isLoaded && !hasProjects ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>A folder Explorer can open: Compose ran on Windows rather than inside a Linux distribution.</summary>
    public static Visibility IsWindowsPath(string path)
    {
        var isWindows = path.Length > 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

        return isWindows ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LineAdded += OnLineAdded;
        ViewModel.Filters.CollectionChanged += OnFiltersChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Activate();
        ShowFilters();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LineAdded -= OnLineAdded;
        ViewModel.Filters.CollectionChanged -= OnFiltersChanged;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Deactivate();
    }

    private void OnFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ShowFilters();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ComposeViewModel.FilterIndex))
        {
            SelectFilter();
        }
    }

    /// <summary>One token per entry of <see cref="ComposeViewModel.Filters"/>: everything, then each service.</summary>
    private void ShowFilters()
    {
        var style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["TokenViewSelectorBarItemStyle"];
        _isShowingFilters = true;
        ServiceFilter.Items.Clear();

        foreach (var filter in ViewModel.Filters)
        {
            ServiceFilter.Items.Add(new SelectorBarItem { Text = filter, Style = style });
        }

        _isShowingFilters = false;
        SelectFilter();
    }

    private void SelectFilter()
    {
        var index = ViewModel.FilterIndex;
        ServiceFilter.SelectedItem = index >= 0 && index < ServiceFilter.Items.Count
            ? ServiceFilter.Items[index]
            : null;
    }

    private void OnServiceFilterChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (!_isShowingFilters && sender.SelectedItem is { } item)
        {
            ViewModel.FilterIndex = sender.Items.IndexOf(item);
        }
    }

    private void OnLineAdded(object? sender, ComposeLogLine line)
    {
        LogList.ScrollIntoView(line);
    }

    private void OnProjectTapped(object sender, TappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ComposeProjectItem project)
        {
            ViewModel.Select(project);
        }
    }

    private async void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is { Location: var location } && Directory.Exists(location))
        {
            await Launcher.LaunchFolderPathAsync(location);
        }
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        await ViewModel.StartAllAsync();
    }

    private async void OnStop(object sender, RoutedEventArgs e)
    {
        await ViewModel.StopAllAsync();
    }

    private async void OnRestart(object sender, RoutedEventArgs e)
    {
        await ViewModel.RestartAllAsync();
    }
}
