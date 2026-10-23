using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.Services.Platform;
using Contr.Interoperability;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Velopack;

namespace Contr.App;

/// <summary>
/// Entry point: a second launch hands its activation to the running instance and exits, so each
/// Windows user runs one Contr.
/// </summary>
public static class Program
{
    /// <summary>Within the 15 seconds Velopack's MSI gives the update hook before it kills it.</summary>
    private static readonly TimeSpan _hookQuitTimeout = TimeSpan.FromSeconds(10);

    private static DispatcherQueue? _dispatcher;

    [STAThread]
    private static int Main()
    {
        // The installer runs this executable for its install, update and uninstall hooks; Run handles them and exits.
        VelopackApp
            .Build()
            .OnBeforeUpdateFastCallback(_ => QuitRunningInstance())
            .OnAfterUpdateFastCallback(_ => QuitRunningInstance())
            .OnBeforeUninstallFastCallback(_ => QuitRunningInstance())
            .Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();

        var instance = AppInstance.FindOrRegisterForKey(InstanceKey());

        if (!instance.IsCurrent)
        {
            Redirect(instance);

            return 0;
        }

        instance.Activated += OnActivated;
        using var quitSignal = new QuitSignal(QuitSignal.NameFor(InstanceKey()), OnQuitRequested);
        RestartInterop.RegisterForInstallerRestart(SignInStartup.BackgroundArgument);
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

    /// <summary>Asks the running Contr to quit and waits for it, so the installer finds its files free.</summary>
    private static void QuitRunningInstance()
    {
        QuitSignal.Send(QuitSignal.NameFor(InstanceKey()), _hookQuitTimeout);
    }

    private static void OnQuitRequested()
    {
        _dispatcher?.TryEnqueue(() => App.Current.QuitOnRequest("an installer's Velopack hook"));
    }
}
