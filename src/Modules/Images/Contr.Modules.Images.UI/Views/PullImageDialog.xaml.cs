using System;
using System.Threading.Tasks;
using Contr.Modules.Images.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Images.UI.Views;

/// <summary>
/// Asks for an image reference and a platform, and starts the pull; the primary button names the image it pulls.
/// </summary>
public sealed partial class PullImageDialog : ContentDialog
{
    private readonly Func<string, string, Task<string?>> _start;

    /// <param name="start">Starts a pull of a reference for a platform; returns why it could not, or null.</param>
    public PullImageDialog(Func<string, string, Task<string?>> start)
    {
        ArgumentNullException.ThrowIfNull(start);
        _start = start;

        InitializeComponent();
    }

    /// <summary>The reference to pull, such as <c>postgres:17.2-alpine</c>; set while the input is valid.</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Such as <c>linux/arm64</c>; empty for the engine's own platform.</summary>
    public string Platform => PlatformBox.SelectedIndex > 0 ? (string)PlatformBox.SelectedItem : string.Empty;

    private void OnInputChanged(object sender, TextChangedEventArgs e)
    {
        var name = ImageNameBox.Text.Trim();
        var tag = TagBox.Text.Trim();
        var parsed = ImageReference.Parse(tag.Length > 0 ? $"{name}:{tag}" : name);

        Reference = parsed.IsSuccess ? parsed.Value.Display : string.Empty;
        IsPrimaryButtonEnabled = parsed.IsSuccess;
        PrimaryButtonText = parsed.IsSuccess ? $"Pull {Reference}" : "Pull";
        ShowError(name.Length > 0 && parsed.IsFailure ? parsed.Error.Message : null);
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        IsPrimaryButtonEnabled = false;
        var error = await _start(Reference, Platform);

        args.Cancel = error is not null;
        ShowError(error);
        IsPrimaryButtonEnabled = true;
        deferral.Complete();
    }

    private void ShowError(string? message)
    {
        PullError.Message = message ?? string.Empty;
        PullError.IsOpen = message is not null;
        PullError.Visibility = message is not null ? Visibility.Visible : Visibility.Collapsed;
    }
}
