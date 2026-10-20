using System;
using System.Reflection;
using Contr.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Contr.Modules.Networks.Application;

/// <summary>Registers the Networks application layer: its message handlers.</summary>
public static class NetworksApplicationServiceCollectionExtensions
{
    public static Assembly Assembly => typeof(NetworksApplicationServiceCollectionExtensions).Assembly;

    public static IServiceCollection AddNetworksApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMediator(Assembly);

        return services;
    }
}
