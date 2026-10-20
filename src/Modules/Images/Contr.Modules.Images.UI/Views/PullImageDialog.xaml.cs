using Contr.Modules.Images.Domain;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Images.UI.Views;

/// <summary>Asks for an image reference and a platform; the primary button names the image it pulls.</summary>
public sealed partial class PullImageDialog : ContentDialog
{
    public PullImageDialog()
    {
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
        ReferenceError.IsOpen = name.Length > 0 && parsed.IsFailure;
        ReferenceError.Message = parsed.IsFailure ? parsed.Error.Message : string.Empty;
    }
}
