using System;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Contr.Interoperability;

/// <summary>The Win32 window calls the WinUI APIs do not cover.</summary>
public static class WindowInterop
{
    /// <summary>Window message identifiers, typed for <see cref="SendMessage"/>.</summary>
    public static class WindowMessage
    {
        /// <summary>Sent to the window being activated and to the one being deactivated.</summary>
        public static uint Activate => PInvoke.WM_ACTIVATE;
    }

    /// <summary>The activation states <c>WM_ACTIVATE</c> carries.</summary>
    public static class WindowActivation
    {
        /// <summary>Activated by anything but a mouse click.</summary>
        public static uint Active => PInvoke.WA_ACTIVE;

        /// <summary>Deactivated.</summary>
        public static uint InActive => PInvoke.WA_INACTIVE;
    }

    /// <summary>The active window of the calling thread, or <see cref="IntPtr.Zero"/> when it has none.</summary>
    public static IntPtr GetActiveWindow()
    {
        HWND hWnd = PInvoke.GetActiveWindow();
        unsafe { return (nint)hWnd.Value; }
    }

    /// <summary>Sends a message and returns once the window procedure has processed it.</summary>
    public static IntPtr SendMessage(IntPtr hWnd, uint msg, uint wParam, IntPtr lParam)
    {
        return PInvoke.SendMessage(new HWND(hWnd), msg, wParam, lParam).Value;
    }

    /// <summary>Lets the process with <paramref name="processId"/> bring its window to the foreground.</summary>
    /// <remarks>Only the process that owns the foreground, or was just launched by the user, can grant this.</remarks>
    public static bool AllowSetForegroundWindow(uint processId)
    {
        return PInvoke.AllowSetForegroundWindow(processId);
    }
}
