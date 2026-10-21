using System;
using System.IO;
using Contr.App.Composition;
using Contr.App.Core;
using Contr.App.Hosting.Orchestration;
using Contr.App.Infrastructure.Observability;
using Contr.App.Services;
using Contr.App.Services.Platform;
using Contr.App.UI;
using Contr.Daemon.UI;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Contr.App.Hosting;

/// <summary>
/// Builds the application's <see cref="IHost"/>: configuration, logging, the daemon connection, the
/// platform services, windows and pages, the startup orchestrators, and the feature modules.
/// </summary>
internal static class AppHost
{
    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            optional: true,
            reloadOnChange: false);
#if DEBUG
        // Gitignored developer overrides, such as a locally built contrd and its arguments.
        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"),
            optional: true,
            reloadOnChange: false);
#endif

        // OutputDebugString reaches only an attached debugger; the file is the record elsewhere.
        builder.Logging.AddDebug();
        builder.Logging.AddProvider(new FileLoggerProvider(
            builder.Configuration.GetValue("Logging:File:MinimumLevel", LogLevel.Information)));

        builder.Services.AddObservability(builder.Configuration);
        builder.Services.AddDaemonConnection(builder.Configuration);

        AddFundamentals(builder.Services);
        AddPlatformServices(builder.Services);
        AddViewsAndViewModels(builder.Services);
        AddOrchestrators(builder.Services);
        AddModules(builder.Services);

        return builder.Build();
    }

    private static void AddFundamentals(IServiceCollection services)
    {
        services.AddTransient<ISubscription, Subscription>();
        services.AddTransient(typeof(IStateManager<>), typeof(StateManager<>));
    }

    private static void AddPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAccentService, AccentService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IMainWindowProvider, MainWindowProvider>();
        services.AddSingleton<TrayService>();
        services.AddTransient<TrayViewModel>();
        services.AddSingleton<Func<TrayViewModel>>(provider => provider.GetRequiredService<TrayViewModel>);
    }

    private static void AddViewsAndViewModels(IServiceCollection services)
    {
        services.AddTransient<MainWindow>();
        services.AddTransient<SplashWindow>();
        services.AddTransient<ShellPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<KubernetesPage>();
        services.AddTransient<TroubleshootPage>();

        services.AddTransient<ShellViewModel>();
        services.AddTransient<SplashViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<KubernetesViewModel>();
        services.AddTransient<TroubleshootViewModel>();
    }

    private static void AddOrchestrators(IServiceCollection services)
    {
        services.AddSingleton<StartupContext>();
        services.AddSingleton<AppOrchestrator>();
        services.AddSingleton<IStartupOrchestrator, AccentOrchestrator>();
        services.AddSingleton<IStartupOrchestrator, SplashOrchestrator>();
        services.AddSingleton<IStartupOrchestrator, DaemonConnectionOrchestrator>();
        services.AddSingleton<IStartupOrchestrator, ShellOrchestrator>();
        services.AddSingleton<IStartupOrchestrator, ThemeOrchestrator>();
        services.AddSingleton<IStartupOrchestrator, TrayOrchestrator>();
        services.AddSingleton<IStartupOrchestrator, ActivationOrchestrator>();
    }

    private static void AddModules(IServiceCollection services)
    {
        foreach (var module in ModuleCatalog.Modules)
        {
            services.AddSingleton(module);
            module.RegisterServices(services);
        }
    }
}
