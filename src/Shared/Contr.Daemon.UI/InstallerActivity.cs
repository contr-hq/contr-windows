using System;
using System.Threading;

namespace Contr.Daemon.UI;

/// <summary>Whether Windows Installer is running an installation, read from the mutex it holds meanwhile.</summary>
public sealed class InstallerActivity
{
    /// <summary>The Windows Installer service holds it while it runs an installation's execute sequence.</summary>
    public const string WindowsInstallerMutex = @"Global\_MSIExecute";

    private readonly string _mutexName;

    public InstallerActivity(string mutexName = WindowsInstallerMutex)
    {
        _mutexName = mutexName;
    }

    public bool IsInstalling()
    {
        try
        {
            if (!Mutex.TryOpenExisting(_mutexName, out var mutex))
            {
                return false;
            }

            mutex.Dispose();

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // The mutex exists; its owner only denies this user access to it.
            return true;
        }
    }
}
