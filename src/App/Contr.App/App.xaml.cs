using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Contr.App.Hosting;
using Contr.App.Hosting.Orchestration;
using Contr.App.Services.Platform;
using Contr.App.UI;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace Contr.App;

/// <summary>
/// Composition root: builds the host, runs the <see cref="AppOrchestrator"/> startup pipeline, and
/// exposes service accessors for the few XAML types the framework constructs itself.
/// </summary>
/// <remarks>
/// The base type is fully qualified because the <c>Contr.Application</c> namespace shadows
/// <see cref="Microsoft.UI.Xaml.Application"/>.
/// </remarks>
public sealed partial class App : Microsoft.UI.Xaml.Application
{
    private IHost? _host;
    private WindowEx? _window;
    private UIElement? _appTitleBar;

    public App()
    {
        InitializeComponent();

        UnhandledException += OnUnhandledException;

        // XAML's handler misses faulted fire-and-forget tasks and exceptions off the UI thread.
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    public new static App Current =>
        Microsoft.UI.Xaml.Application.Current as App
        ?? throw new InvalidOperationException("Current application is not App.");

    /// <summary>Throws if accessed before the host is built.</summary>
    public static IServiceProvider Services =>
        Current._host?.Services
        ?? throw new InvalidOperationException("The host has not been built yet.");

    /// <summary>Throws if accessed before the window is created.</summary>
    public static WindowEx MainWindow =>
        Current._window
        ?? throw new InvalidOperationException("The main window has not been created yet.");

    public static UIElement AppTitleBar
    {
        get => Current._appTitleBar
            ?? throw new InvalidOperationException("The app title bar has not been set yet.");
        set => Current._appTitleBar = value;
    }

    /// <summary>Whether <see cref="QuitAsync"/> is running; until then closing the window hides it.</summary>
    public bool IsQuitting { get; private set; }

    /// <summary>Throws when the service is not registered.</summary>
    public static TService GetService<TService>() where TService : IUiContractService
    {
        return Services.GetService(typeof(TService)) is TService service
            ? service
            : throw new InvalidOperationException(
                $"Service of type {typeof(TService)} is not registered.");
    }

    public static IEnumerable<TService> GetServices<TService>() where TService : class
    {
        return Services.GetServices<TService>();
    }

    /// <summary>Throws when the view model is not registered.</summary>
    public static TViewModel GetViewModel<TViewModel>() where TViewModel : ViewModel
    {
        return Services.GetService(typeof(TViewModel)) is TViewModel viewModel
            ? viewModel
            : throw new InvalidOperationException(
                $"View model of type {typeof(TViewModel)} is not registered.");
    }

    /// <summary>Stops and disposes the host within five seconds; failures are ignored on the exit path.</summary>
    public static async Task ShutdownAsync()
    {
        var host = Current._host;

        if (host is null)
        {
            return;
        }

        Current._host = null;
        try
        {
            await host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Teardown must not crash the exit path.
        }
        finally
        {
            host.Dispose();
        }
    }

    /// <summary>Shows the main window from the tray or a second launch; ignored before startup creates it.</summary>
    public void ShowMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.Restore();
        }

        _window.BringToFront();
        _window.Activate();
    }

    /// <summary>Ends the process; the daemon keeps running.</summary>
    public async Task QuitAsync()
    {
        IsQuitting = true;

        // The tray icon's window belongs to this thread; the host disposes services on another.
        Services.GetService<TrayService>()?.Dispose();
        await ShutdownAsync();
        _window?.Close();
        Exit();
    }

    internal void SetMainWindow(WindowEx window)
    {
        _window = window;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _host = AppHost.Build();
            ContractServices.Configure(_host.Services);

            await _host.StartAsync();
            await _host.Services
                .GetRequiredService<AppOrchestrator>()
                .StartAsync();
        }
        catch (Exception ex)
        {
            ExceptionWindow.ShowException(ex);
        }
    }

    /// <summary>Records a task that faulted with nobody awaiting it.</summary>
    /// <remarks>
    /// SetObserved keeps the exception from escalating at finalization, which would end the process
    /// later with a stack pointing at the finalizer.
    /// </remarks>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }

    /// <summary>Logs anything that escapes a non-UI thread; the runtime terminates regardless.</summary>
    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        Log("Unhandled exception on a background thread.", e.ExceptionObject as Exception);
    }

    private void Log(string message, Exception? exception)
    {
        try
        {
            _host?.Services.GetService<ILogger<App>>()?.LogError(exception, "{Message}", message);
        }
        catch (Exception)
        {
            // A failing logger must not replace the exception being reported.
        }
    }

    private void OnUnhandledException(
        object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Log("Unhandled exception.", e.Exception);
        ExceptionWindow.ShowException(e.Exception);
    }
}
