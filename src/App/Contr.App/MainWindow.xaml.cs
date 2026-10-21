using System;
using Contr.App.UI;
using Contr.UI.Common.Helpers;
using Microsoft.UI.Windowing;
using WinUIEx;

namespace Contr.App;

/// <summary>Hosts the <see cref="ShellPage"/>; closing it hides it to the tray until the app quits.</summary>
public sealed partial class MainWindow : WindowEx
{
    public MainWindow()
    {
        InitializeComponent();

        WindowHelper.FitToWorkArea(this);
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
