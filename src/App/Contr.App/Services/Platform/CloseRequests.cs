using Contr.Interoperability;

namespace Contr.App.Services.Platform;

/// <summary>What the main window does when something closes it.</summary>
public enum CloseAction
{
    /// <summary>Hide to the notification area: the user closed the window.</summary>
    Hide,

    /// <summary>End the process: Windows, an installer or another program closed it.</summary>
    Quit,
}

/// <summary>Tells the user closing the main window from sign-out, an installer or another program closing it.</summary>
/// <remarks>
/// Restart Manager closes an app for an installer with <c>WM_QUERYENDSESSION</c> and <c>WM_ENDSESSION</c> and counts
/// it closed once it answers, so an app that only hides keeps its files, and its contrd, in use during the install.
/// </remarks>
public sealed class CloseRequests
{
    private bool _sessionEnding;

    /// <summary>Whether <paramref name="message"/> ends the app: a <c>WM_ENDSESSION</c> whose session ends.</summary>
    public bool EndsApp(uint message, nuint wParam)
    {
        if (message == WindowInterop.WindowMessage.QueryEndSession)
        {
            _sessionEnding = true;

            return false;
        }

        if (message == WindowInterop.WindowMessage.EndSession)
        {
            // Zero: another app refused, and the session goes on.
            _sessionEnding = wParam != 0;

            return _sessionEnding;
        }

        return false;
    }

    /// <summary>Hides a visible window; quits for a hidden one, which no user closes, or a session ending.</summary>
    public CloseAction OnClose(bool isVisible)
    {
        return _sessionEnding || !isVisible ? CloseAction.Quit : CloseAction.Hide;
    }
}
