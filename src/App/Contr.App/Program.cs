using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Contr.Interoperability;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Contr.App;

/// <summary>
/// Entry point: a second launch hands its activation to the running instance and exits, so each
/// Windows user runs one Contr.
/// </summary>
public static class Program
{
    private static DispatcherQueue? _dispatcher;

    [STAThread]
    private static int Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var instance = AppInstance.FindOrRegisterForKey(InstanceKey());

        if (!instance.IsCurrent)
        {
            Redirect(instance);

            return 0;
        }

        instance.Activated += OnActivated;
        Microsoft.UI.Xaml.Application.Start(Launch);

        return 0;
    }

    private static void Launch(ApplicationInitializationCallbackParams parameters)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(_dispatcher));
        _ = new App();
    }

    private static string InstanceKey()
    {
        using var identity = WindowsIdentity.GetCurrent();

        return $"contr-{identity.User?.Value}";
    }

    /// <summary>Forwards this launch to the running instance and lets it take the foreground.</summary>
    /// <remarks>
    /// The redirect runs on a worker thread; <see cref="WaitHandle.WaitOne()"/> on this STA thread
    /// keeps pumping COM, which the redirect needs.
    /// </remarks>
    private static void Redirect(AppInstance running)
    {
        WindowInterop.AllowSetForegroundWindow(running.ProcessId);

        var arguments = AppInstance
            .GetCurrent()
            .GetActivatedEventArgs();
        using var redirected = new ManualResetEvent(false);
        _ = Task.Run(async () => {
            try
            {
                await running.RedirectActivationToAsync(arguments);
            }
            finally
            {
                redirected.Set();
            }
        });
        redirected.WaitOne();
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        _dispatcher?.TryEnqueue(() => App.Current.ShowMainWindow());
    }
}
