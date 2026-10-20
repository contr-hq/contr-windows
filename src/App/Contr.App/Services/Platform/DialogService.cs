using System;
using System.Threading.Tasks;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.Services.Platform;

/// <summary>Shows modal <see cref="ContentDialog"/>s anchored to the main window's XAML root.</summary>
public sealed class DialogService : IDialogService
{
    public async Task ShowMessageAsync(string title, string message, string closeText = "OK")
    {
        var dialog = new ContentDialog {
            Title = title,
            Content = message,
            CloseButtonText = closeText,
            XamlRoot = App.MainWindow.Content.XamlRoot,
        };

        await dialog.ShowAsync();
    }

    public async Task<bool> ShowConfirmAsync(
        string title, string message, string primaryText = "Yes", string closeText = "No")
    {
        var dialog = new ContentDialog {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content.XamlRoot,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<string?> ShowPromptAsync(
        string title, string label, string initialValue = "", string primaryText = "OK")
    {
        var input = new TextBox { Header = label, Text = initialValue, AcceptsReturn = false, MinWidth = 360 };
        var dialog = new ContentDialog {
            Title = title,
            Content = input,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow.Content.XamlRoot,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text : null;
    }
}
