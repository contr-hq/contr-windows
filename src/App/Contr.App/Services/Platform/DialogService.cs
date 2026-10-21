using System;
using System.Threading.Tasks;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.Services.Platform;

/// <summary>Shows modal <see cref="ContentDialog"/>s anchored to the main window's XAML root.</summary>
public sealed class DialogService : IDialogService
{
    public async Task ShowMessageAsync(string title, string message, string closeText = "OK")
    {
        var dialog = Create(title, message);
        dialog.CloseButtonText = closeText;

        await dialog.ShowAsync();
    }

    public async Task<bool> ShowConfirmAsync(
        string title, string message, string primaryText = "Yes", string closeText = "No")
    {
        var dialog = Create(title, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        dialog.PrimaryButtonText = primaryText;
        dialog.PrimaryButtonStyle = (Style)Microsoft.UI.Xaml.Application.Current.Resources["DestructiveButtonStyle"];
        dialog.CloseButtonText = closeText;
        dialog.DefaultButton = ContentDialogButton.Close;

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<string?> ShowPromptAsync(
        string title, string label, string initialValue = "", string primaryText = "OK")
    {
        var input = new TextBox { Header = label, Text = initialValue, AcceptsReturn = false, MinWidth = 360 };
        var dialog = Create(title, input);
        dialog.PrimaryButtonText = primaryText;
        dialog.CloseButtonText = "Cancel";
        dialog.DefaultButton = ContentDialogButton.Primary;

        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text : null;
    }

    public async Task<bool> ShowPromptAsync(
        string title, string label, string initialValue, string primaryText, Func<string, Task<string?>> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);

        var error = new InfoBar {
            IsClosable = false,
            Severity = InfoBarSeverity.Error,
            Visibility = Visibility.Collapsed,
        };
        var input = new TextBox { Header = label, Text = initialValue, AcceptsReturn = false, MinWidth = 360 };
        var body = new StackPanel { Spacing = 16, Children = { error, input } };
        var dialog = Create(title, body);
        dialog.PrimaryButtonText = primaryText;
        dialog.CloseButtonText = "Cancel";
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.IsPrimaryButtonEnabled = initialValue.Trim().Length > 0;
        input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = input.Text.Trim().Length > 0;
        dialog.PrimaryButtonClick += async (_, args) => {
            var deferral = args.GetDeferral();
            var failure = await apply(input.Text);

            args.Cancel = failure is not null;
            error.Message = failure ?? string.Empty;
            error.IsOpen = failure is not null;
            error.Visibility = failure is not null ? Visibility.Visible : Visibility.Collapsed;
            deferral.Complete();
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// A dialog in the window's theme: a dialog's popup does not inherit the theme the window content requests.
    /// </summary>
    private static ContentDialog Create(string title, object content)
    {
        var root = App.MainWindow.Content as FrameworkElement;

        return new ContentDialog {
            Title = title,
            Content = content,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            RequestedTheme = root?.ActualTheme ?? ElementTheme.Default,
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"],
        };
    }
}
