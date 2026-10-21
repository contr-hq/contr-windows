using System;
using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Contr.UI.Common.Helpers;

/// <summary>Window chrome shared by every window the app opens.</summary>
public static class WindowHelper
{
    /// <summary>The application icon, beside the executable.</summary>
    public static string AppIconPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");

    /// <summary>Sets the taskbar, Alt+Tab and title-bar icon; a missing icon file is ignored.</summary>
    public static void TrySetAppIcon(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (File.Exists(AppIconPath))
        {
            window.AppWindow.SetIcon(AppIconPath);
        }
    }

    /// <summary>Centers the window in its display's work area, shrinking it to fit beside the taskbar.</summary>
    public static void FitToWorkArea(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var appWindow = window.AppWindow;
        var workArea = DisplayArea
            .GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary)
            .WorkArea;
        var width = Math.Min(appWindow.Size.Width, workArea.Width);
        var height = Math.Min(appWindow.Size.Height, workArea.Height);

        appWindow.MoveAndResize(new RectInt32(
            workArea.X + ((workArea.Width - width) / 2),
            workArea.Y + ((workArea.Height - height) / 2),
            width,
            height));
    }
}
