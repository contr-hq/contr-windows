using System;
using System.IO;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Contr.App.UI;

/// <summary>The status bar's activity flyout: what the daemon runs and ran.</summary>
public sealed partial class ActivityPanel : UserControl
{
    private readonly IMainWindowProvider _mainWindow;

    public ActivityPanel(ActivityViewModel viewModel, IMainWindowProvider mainWindow)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(mainWindow);
        ViewModel = viewModel;
        _mainWindow = mainWindow;

        InitializeComponent();
    }

    public ActivityViewModel ViewModel { get; }

    public static string CancelName(string title)
    {
        return $"Cancel {title}";
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ActivityRow row)
        {
            await ViewModel.CancelAsync(row);
        }
    }

    private async void OnFix(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is CheckFix fix)
        {
            await ViewModel.FixAsync(fix);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ActivityRow row || _mainWindow.MainWindow is not { } window)
        {
            return;
        }

        var picker = new FileSavePicker {
            SuggestedStartLocation = PickerLocationId.Downloads,
            SuggestedFileName = TroubleshootViewModel.BundleName(DateTimeOffset.Now),
        };
        picker.FileTypeChoices.Add("Zip archive", [".zip"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        if (await picker.PickSaveFileAsync() is not { } file)
        {
            return;
        }

        bool saved;
        using (var stream = await file.OpenStreamForWriteAsync())
        {
            stream.SetLength(0);
            saved = await ViewModel.SaveBundleAsync(row, stream, file.Path);
        }

        if (!saved)
        {
            await file.DeleteAsync();
        }
    }
}
