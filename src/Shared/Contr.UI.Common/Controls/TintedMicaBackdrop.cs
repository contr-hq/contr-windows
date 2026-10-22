using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Contr.UI.Common.Controls;

/// <summary>Mica tinted with Contr's slate surfaces, so a window looks the same over any wallpaper.</summary>
/// <remarks>
/// Where Mica is unsupported, desktop acrylic takes the same tint; where neither is, the window has no backdrop.
/// </remarks>
public sealed partial class TintedMicaBackdrop : SystemBackdrop
{
    private static readonly Color _lightTint = Color.FromArgb(255, 0xE8, 0xEC, 0xF0);
    private static readonly Color _darkTint = Color.FromArgb(255, 0x18, 0x1C, 0x20);

    /// <summary>High enough that the wallpaper only shades the tint.</summary>
    private const float _tintOpacity = 0.85f;

    private ISystemBackdropControllerWithTargets? _controller;

    /// <summary>The framework keeps this up to date; asking for it again after connecting throws.</summary>
    private SystemBackdropConfiguration? _configuration;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        _controller = CreateController();

        if (_controller is null)
        {
            return;
        }

        _configuration = GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot);
        Tint(_configuration.Theme);
        _controller.SetSystemBackdropConfiguration(_configuration);
        _controller.AddSystemBackdropTarget(connectedTarget);
    }

    protected override void OnDefaultSystemBackdropConfigurationChanged(
        ICompositionSupportsSystemBackdrop target,
        XamlRoot xamlRoot)
    {
        // The base implementation throws ArgumentException for this target.
        if (_configuration is not null)
        {
            Tint(_configuration.Theme);
        }
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);

        if (_controller is null)
        {
            return;
        }

        _controller.RemoveSystemBackdropTarget(disconnectedTarget);
        _controller.Dispose();
        _controller = null;
        _configuration = null;
    }

    private static ISystemBackdropControllerWithTargets? CreateController()
    {
        if (MicaController.IsSupported())
        {
            return new MicaController();
        }

        return DesktopAcrylicController.IsSupported() ? new DesktopAcrylicController() : null;
    }

    private void Tint(SystemBackdropTheme theme)
    {
        var tint = theme == SystemBackdropTheme.Dark ? _darkTint : _lightTint;

        switch (_controller)
        {
            case MicaController mica:
                mica.TintColor = tint;
                mica.TintOpacity = _tintOpacity;
                mica.LuminosityOpacity = 1;
                mica.FallbackColor = tint;
                break;
            case DesktopAcrylicController acrylic:
                acrylic.TintColor = tint;
                acrylic.TintOpacity = _tintOpacity;
                acrylic.LuminosityOpacity = 1;
                acrylic.FallbackColor = tint;
                break;
        }
    }
}
