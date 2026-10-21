using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Windows.UI.Text;

namespace Contr.App.UI;

/// <summary>The Kubernetes page: the daemon's cluster, its settings, and the commands that maintain it.</summary>
public sealed partial class KubernetesPage : Page, IHasViewModel
{
    private readonly IClipboardService _clipboard;
    private bool _isShowingVersion;

    public KubernetesPage(KubernetesViewModel viewModel, IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(clipboard);
        ViewModel = viewModel;
        _clipboard = clipboard;

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public KubernetesViewModel ViewModel { get; }

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    /// <summary>Such as <c>k3s v1.37.1</c>.</summary>
    public static string K3s(string version)
    {
        return $"k3s {version}";
    }

    /// <summary>The running step reads strongest.</summary>
    public static FontWeight StepWeight(bool isRunning)
    {
        return isRunning ? FontWeights.SemiBold : FontWeights.Normal;
    }

    /// <summary>A step not reached yet reads fainter.</summary>
    public static double StepOpacity(bool isPending)
    {
        return isPending ? 0.72 : 1;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Activate();
        ShowVersion();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Deactivate();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KubernetesViewModel.ChosenVersion))
        {
            ShowVersion();
        }
    }

    /// <summary>Selects the version the settings ask for; the list's items change with every cluster state.</summary>
    private void ShowVersion()
    {
        _isShowingVersion = true;
        VersionBox.SelectedItem = ViewModel.Versions.Contains(ViewModel.ChosenVersion) ? ViewModel.ChosenVersion : null;
        _isShowingVersion = false;
    }

    private async void OnSwitch(object sender, RoutedEventArgs e)
    {
        await ViewModel.SwitchAsync();
    }

    /// <summary>A new version deletes the cluster's data, so the user confirms it; the safe answer is the default.
    /// </summary>
    private async void OnVersionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isShowingVersion || VersionBox.SelectedItem is not string version || version == ViewModel.ChosenVersion)
        {
            return;
        }

        var confirmed = await ConfirmAsync(
            $"Change Kubernetes to {version}?",
            "The cluster is recreated with this version, which deletes its workloads and data. Your images stay.",
            "Change version");

        if (confirmed)
        {
            await ViewModel.ChooseVersionAsync(version);
        }

        ShowVersion();
    }

    private async void OnContextToggled(object sender, RoutedEventArgs e)
    {
        if (ContextSwitch.IsOn == ViewModel.IsCurrentContext)
        {
            return;
        }

        await ViewModel.ChooseContextAsync(ContextSwitch.IsOn);
        ContextSwitch.IsOn = ViewModel.IsCurrentContext;
    }

    private async void OnTraefikToggled(object sender, RoutedEventArgs e)
    {
        if (TraefikSwitch.IsOn == ViewModel.UsesTraefik)
        {
            return;
        }

        await ViewModel.ChooseTraefikAsync(TraefikSwitch.IsOn);
        TraefikSwitch.IsOn = ViewModel.UsesTraefik;
    }

    private void OnCopyApiServer(object sender, RoutedEventArgs e)
    {
        _clipboard.SetText(ViewModel.ApiServer);
    }

    private async void OnOpenKubeconfigFolder(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanOpenKubeconfig)
        {
            await Launcher.LaunchFolderPathAsync(ViewModel.KubeconfigFolder);
        }
    }

    private async void OnReset(object sender, RoutedEventArgs e)
    {
        var confirmed = await ConfirmAsync(
            "Reset the Kubernetes cluster?",
            "Deletes every workload and the cluster's data; a new cluster starts while Kubernetes is on. Your images "
                + "stay.",
            "Reset");

        if (confirmed)
        {
            await ViewModel.ResetAsync();
        }
    }

    /// <summary>Lets the user pick the engine's images and imports them, each one's outcome showing as it lands.
    /// </summary>
    private async void OnImportImages(object sender, RoutedEventArgs e)
    {
        var images = await ViewModel.ListEngineImagesAsync();

        if (images.IsFailure)
        {
            await ShowDialogAsync(Dialog("The engine's images aren't available", images.Error.Message, "Close"));

            return;
        }

        var list = new ListView {
            ItemsSource = images.Value,
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 320,
        };
        AutomationProperties.SetAutomationId(list, "KubernetesImportImages");
        AutomationProperties.SetName(list, "Images");
        var progress = new ProgressBar { IsIndeterminate = true, Visibility = Visibility.Collapsed };
        var outcome = new TextBlock {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Text = images.Value.Count == 0
                ? "The engine has no tagged images yet. Pull or build one first."
                : "Pods in the cluster use these without pulling them from a registry.",
        };
        var dialog = Dialog(
            "Import images into the cluster",
            new StackPanel { Spacing = 12, MinWidth = 420, Children = { outcome, list, progress } },
            "Cancel");
        dialog.PrimaryButtonText = "Import";
        dialog.PrimaryButtonStyle = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];
        dialog.IsPrimaryButtonEnabled = false;
        list.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = list.SelectedItems.Count > 0;
        dialog.PrimaryButtonClick += async (_, args) => {
            var deferral = args.GetDeferral();
            List<string> chosen = [.. list.SelectedItems.OfType<string>()];
            list.IsEnabled = false;
            dialog.IsPrimaryButtonEnabled = false;
            progress.Visibility = Visibility.Visible;
            outcome.Text = $"Importing {Count(chosen.Count)}…";
            var done = 0;
            await ViewModel.ImportAsync(
                chosen, new Progress<ImageImport>(_ => outcome.Text = $"{++done} of {chosen.Count} done…"));
            deferral.Complete();
        };

        await ShowDialogAsync(dialog);
    }

    private static string Count(int images)
    {
        return images == 1 ? "1 image" : $"{images} images";
    }

    private async Task<bool> ConfirmAsync(string title, string message, string action)
    {
        var dialog = Dialog(title, message, "Cancel");
        dialog.PrimaryButtonText = action;
        dialog.PrimaryButtonStyle = (Style)Microsoft.UI.Xaml.Application.Current.Resources["DestructiveButtonStyle"];

        return await ShowDialogAsync(dialog) == ContentDialogResult.Primary;
    }

    /// <summary>A dialog in the page's theme, whose default answer is <paramref name="close"/>.</summary>
    private ContentDialog Dialog(string title, object content, string close)
    {
        // A dialog's popup does not inherit the theme the window content requests.
        var body = content is string text ? new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } : content;

        return new ContentDialog {
            Title = title,
            Content = body,
            CloseButtonText = close,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"],
        };
    }

    private static async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        return await dialog.ShowAsync();
    }
}
