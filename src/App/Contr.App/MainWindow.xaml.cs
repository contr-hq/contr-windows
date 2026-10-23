using System;
using Contr.App.Services.Platform;
using Contr.App.UI;
using Contr.Interoperability;
using Contr.UI.Common.Helpers;
using Microsoft.UI.Windowing;
using WinUIEx;
using WinUIEx.Messaging;

namespace Contr.App;

/// <summary>
/// Hosts the <see cref="ShellPage"/>; the user closing it hides it to the tray, while sign-out, an installer or another
/// program closing it quits the app.
/// </summary>
public sealed partial class MainWindow : WindowEx
{
    private readonly CloseRequests _closeRequests = new();

    /// <summary>Held here because WinUIEx keeps only a weak reference to it.</summary>
    private readonly WindowMessageMonitor _messages;

    public MainWindow()
    {
        InitializeComponent();

        WindowHelper.FitToWorkArea(this);
        WindowHelper.TrySetAppIcon(this);

        _messages = new WindowMessageMonitor(this);
        _messages.WindowMessageReceived += OnWindowMessage;
        AppWindow.Closing += OnClosing;
    }

    internal void AttachShell(ShellPage shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        ExtendsContentIntoTitleBar = true;
        Root.Children.Add(shell);
        SetTitleBar(shell.TitleBar);
    }

    private void OnWindowMessage(object? sender, WindowMessageEventArgs args)
    {
        if (_closeRequests.EndsApp(args.Message.MessageId, args.Message.WParam))
        {
            App.Current.QuitOnRequest("the Windows session is ending or an installer is closing Contr");
        }
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (App.Current.IsQuitting)
        {
            return;
        }

        args.Cancel = true;

        if (_closeRequests.OnClose(WindowInterop.IsWindowVisible(this.GetWindowHandle())) == CloseAction.Quit)
        {
            App.Current.QuitOnRequest("another program closed the window");

            return;
        }

        this.Hide();
    }
}
