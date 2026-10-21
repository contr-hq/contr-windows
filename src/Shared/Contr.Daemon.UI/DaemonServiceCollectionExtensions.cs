using System;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contr.Daemon.UI;

/// <summary>Registers the application's one daemon connection.</summary>
public static class DaemonServiceCollectionExtensions
{
    public static IServiceCollection AddDaemonConnection(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DaemonOptions>(configuration.GetSection(DaemonOptions.SectionName));
        services.TryAddSingleton<DaemonLauncher>();
        services.TryAddSingleton<DaemonConnection>();
        services.TryAddSingleton<IDaemonConnection>(provider => provider.GetRequiredService<DaemonConnection>());
        services.TryAddSingleton<IDaemonClient>(provider => provider.GetRequiredService<DaemonConnection>());
        services.TryAddSingleton<IntegrationClient>();
        services.TryAddSingleton<ICliTools>(provider => provider.GetRequiredService<IntegrationClient>());
        services.TryAddSingleton<IWslDistributions>(provider => provider.GetRequiredService<IntegrationClient>());
        services.TryAddSingleton<IDiagnostics, DiagnosticsClient>();
        services.TryAddSingleton<IDaemonSettings, DaemonSettingsClient>();

        return services;
    }
}
