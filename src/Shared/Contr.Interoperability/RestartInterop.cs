using Windows.Win32;
using Windows.Win32.System.Recovery;

namespace Contr.Interoperability;

/// <summary>This process's registration with Restart Manager.</summary>
public static class RestartInterop
{
    /// <summary>
    /// Has Restart Manager start this executable with <paramref name="arguments"/> after an installer closes it, and
    /// never after a crash, a hang or a reboot.
    /// </summary>
    /// <remarks>Windows adds the executable's path itself; the arguments are at most 1024 characters.</remarks>
    public static bool RegisterForInstallerRestart(string arguments)
    {
        const REGISTER_APPLICATION_RESTART_FLAGS installerOnly = REGISTER_APPLICATION_RESTART_FLAGS.RESTART_NO_CRASH
            | REGISTER_APPLICATION_RESTART_FLAGS.RESTART_NO_HANG
            | REGISTER_APPLICATION_RESTART_FLAGS.RESTART_NO_REBOOT;

        return PInvoke.RegisterApplicationRestart(arguments, installerOnly).Succeeded;
    }
}
