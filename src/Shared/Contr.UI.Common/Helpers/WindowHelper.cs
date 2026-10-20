using System;
using System.IO;
using Microsoft.UI.Xaml;

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
}
