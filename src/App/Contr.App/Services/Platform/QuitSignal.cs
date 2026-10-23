using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace Contr.App.Services.Platform;

/// <summary>A named event through which another process, such as an installer's hook, asks Contr to quit.</summary>
/// <remarks>The event is in the session's <c>Local\</c> namespace: only processes in this session reach it.</remarks>
public sealed class QuitSignal : IDisposable
{
    private readonly EventWaitHandle _event;
    private readonly RegisteredWaitHandle _wait;

    /// <summary>Calls <paramref name="onQuit"/> on a thread-pool thread each time another process signals.</summary>
    public QuitSignal(string name, Action onQuit)
    {
        ArgumentNullException.ThrowIfNull(onQuit);
        _event = new EventWaitHandle(false, EventResetMode.AutoReset, name);
        _wait = ThreadPool.RegisterWaitForSingleObject(
            _event, (_, _) => onQuit(), state: null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public static string NameFor(string instanceKey)
    {
        return $@"Local\{instanceKey}-quit";
    }

    /// <summary>Signals the running instance and waits up to <paramref name="timeout"/> for it to exit.</summary>
    /// <returns>Whether an instance was listening.</returns>
    public static bool Send(string name, TimeSpan timeout)
    {
        if (!EventWaitHandle.TryOpenExisting(name, out var signal))
        {
            return false;
        }

        using (signal)
        {
            signal.Set();
        }

        WaitForOtherInstances(timeout);

        return true;
    }

    public void Dispose()
    {
        _wait.Unregister(null);
        _event.Dispose();
    }

    /// <summary>Waits for this executable's other processes in this session, which hold its files open.</summary>
    private static void WaitForOtherInstances(TimeSpan timeout)
    {
        using var current = Process.GetCurrentProcess();
        var deadline = DateTime.UtcNow + timeout;

        foreach (var process in Process.GetProcessesByName(current.ProcessName))
        {
            using (process)
            {
                var remaining = deadline - DateTime.UtcNow;
                try
                {
                    if (process.Id != current.Id && process.SessionId == current.SessionId && remaining > TimeSpan.Zero)
                    {
                        process.WaitForExit(remaining);
                    }
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
                {
                    // The process exited between the listing and the wait.
                }
            }
        }
    }
}
