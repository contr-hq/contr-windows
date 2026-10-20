using System;
using Contr.App.UI;
using Contr.UI.Common.Helpers;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinUIEx;

namespace Contr.App;

/// <summary>Hosts the <see cref="ShellPage"/>; closing it hides it to the tray until the app quits.</summary>
public sealed partial class MainWindow : WindowEx
{
    public MainWindow()
    {
        InitializeComponent();

        FitToWorkArea();
        WindowHelper.TrySetAppIcon(this);

        AppWindow.Closing += OnClosing;
    }

    internal void AttachShell(ShellPage shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        ExtendsContentIntoTitleBar = true;
        Root.Children.Add(shell);
        SetTitleBar(shell.TitleBar);
    }

    /// <summary>Centers the window in the work area, shrinking it to fit beside the taskbar.</summary>
    private void FitToWorkArea()
    {
        var workArea = DisplayArea
            .GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary)
            .WorkArea;
        var width = Math.Min(AppWindow.Size.Width, workArea.Width);
        var height = Math.Min(AppWindow.Size.Height, workArea.Height);

        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + ((workArea.Width - width) / 2),
            workArea.Y + ((workArea.Height - height) / 2),
            width,
            height));
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (App.Current.IsQuitting)
        {
            return;
        }

        args.Cancel = true;
        this.Hide();
    }
}
