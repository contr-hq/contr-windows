using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using WinRT.Interop;

namespace Contr.Modules.Containers.UI.Views;

/// <summary>The engine's Compose projects, with their services, the Compose commands and the merged logs.</summary>
public sealed partial class ComposePage : Page, IHasViewModel
{
    /// <summary>Service colours for the light log background, each 4.5:1 or more against it.</summary>
    private static readonly Color[] _lightServiceColors = [
        Color.FromArgb(255, 0x39, 0x72, 0x47),
        Color.FromArgb(255, 0x84, 0x63, 0x24),
        Color.FromArgb(255, 0x32, 0x68, 0x93),
        Color.FromArgb(255, 0xA3, 0x43, 0x3B),
        Color.FromArgb(255, 0x79, 0x52, 0x95),
        Color.FromArgb(255, 0x2B, 0x72, 0x76),
    ];

    /// <summary>Service colours for the dark log background, each 4.5:1 or more against it.</summary>
    private static readonly Color[] _darkServiceColors = [
        Color.FromArgb(255, 0x82, 0xC3, 0x8F),
        Color.FromArgb(255, 0xDB, 0xB9, 0x70),
        Color.FromArgb(255, 0x86, 0xB7, 0xE1),
        Color.FromArgb(255, 0xE8, 0x8F, 0x87),
        Color.FromArgb(255, 0xC3, 0xA1, 0xDC),
        Color.FromArgb(255, 0x76, 0xC7, 0xCC),
    ];

    /// <summary>Shared by every log line, so recolouring them follows the page's theme.</summary>
    private static readonly SolidColorBrush[] _serviceBrushes =
        [.. _darkServiceColors.Select(color => new SolidColorBrush(color))];

    private readonly IMainWindowProvider _mainWindow;
    private bool _isShowingFilters;
    private bool _isShowingSelection;

    public ComposePage(ComposeViewModel viewModel, IMainWindowProvider mainWindow)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(mainWindow);
        ViewModel = viewModel;
        _mainWindow = mainWindow;

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

    public static string AddedText(bool isAdded)
    {
        return isAdded ? " · added by you" : string.Empty;
    }

    /// <summary>The port of an address such as <c>localhost:8080</c>.</summary>
    public static string PortNumber(string address)
    {
        return address[(address.LastIndexOf(':') + 1)..];
    }

    public static string RestartName(string service)
    {
        return $"Restart {service}";
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
        ViewModel.OutputAdded += OnOutputAdded;
        ViewModel.Filters.CollectionChanged += OnFiltersChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ActualThemeChanged += OnActualThemeChanged;
        ViewModel.Activate();
        ShowServiceColors();
        ShowFilters();
        ShowSelection();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LineAdded -= OnLineAdded;
        ViewModel.OutputAdded -= OnOutputAdded;
        ViewModel.Filters.CollectionChanged -= OnFiltersChanged;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ActualThemeChanged -= OnActualThemeChanged;
        ViewModel.Deactivate();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ShowServiceColors();
    }

    private void ShowServiceColors()
    {
        var colors = ActualTheme == ElementTheme.Light ? _lightServiceColors : _darkServiceColors;
        for (var i = 0; i < _serviceBrushes.Length; i++)
        {
            _serviceBrushes[i].Color = colors[i];
        }
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
        else if (e.PropertyName == nameof(ComposeViewModel.Selected))
        {
            ShowSelection();
        }
    }

    /// <summary>Selects the view model's project; each change to the list replaces the items the list selected.
    /// </summary>
    private void ShowSelection()
    {
        _isShowingSelection = true;
        ProjectList.SelectedItem = ViewModel.Selected;
        _isShowingSelection = false;
    }

    private void OnProjectSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isShowingSelection)
        {
            return;
        }

        if (ProjectList.SelectedItem is ComposeProjectItem project)
        {
            ViewModel.Select(project);
        }
        else
        {
            // The list dropped its selection when the selected item was replaced.
            ShowSelection();
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

    private void OnOutputAdded(object? sender, ComposeOutputLine line)
    {
        OutputList.ScrollIntoView(line);
    }

    private async void OnRemoveProject(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ComposeProjectItem project)
        {
            await ViewModel.RemoveProjectAsync(project);
        }
    }

    private async void OnAddProject(object sender, RoutedEventArgs e)
    {
        if (_mainWindow.MainWindow is not { } window)
        {
            return;
        }

        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add(".yaml");
        picker.FileTypeFilter.Add(".yml");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        if (await picker.PickSingleFileAsync() is { } file)
        {
            await ViewModel.AddProjectAsync(file.Path);
        }
    }

    private async void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        if (ViewModel.WorkingDir is { Length: > 0 } folder && Directory.Exists(folder))
        {
            await Launcher.LaunchFolderPathAsync(folder);
        }
    }

    private async void OnUp(object sender, RoutedEventArgs e)
    {
        await ViewModel.UpAsync();
    }

    private async void OnPull(object sender, RoutedEventArgs e)
    {
        await ViewModel.PullAsync();
    }

    private async void OnRestart(object sender, RoutedEventArgs e)
    {
        await ViewModel.RestartAsync();
    }

    private async void OnStop(object sender, RoutedEventArgs e)
    {
        await ViewModel.StopAsync();
    }

    private async void OnRestartService(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ComposeServiceRow service)
        {
            await ViewModel.RestartServiceAsync(service);
        }
    }

    /// <summary>Asks before Down, whose safe answer is the default, and offers to delete the volumes too.</summary>
    private async void OnDown(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is not { } project)
        {
            return;
        }

        var resources = Microsoft.UI.Xaml.Application.Current.Resources;
        var removeVolumes = new CheckBox { Content = "Also delete its volumes and the data in them" };
        AutomationProperties.SetAutomationId(removeVolumes, "ComposeDownVolumes");
        var message = new TextBlock {
            Text = "Compose stops and removes the project's containers and networks. Its images stay, and Up "
                + "creates the containers again.",
            TextWrapping = TextWrapping.Wrap,
        };

        // A dialog's popup does not inherit the theme the window content requests.
        var dialog = new ContentDialog {
            Title = $"Take down {project.Name}?",
            Content = new StackPanel { Spacing = 12, Children = { message, removeVolumes } },
            PrimaryButtonText = "Take down",
            PrimaryButtonStyle = (Style)resources["DestructiveButtonStyle"],
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Style = (Style)resources["DefaultContentDialogStyle"],
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.DownAsync(removeVolumes.IsChecked == true);
        }
    }
}
