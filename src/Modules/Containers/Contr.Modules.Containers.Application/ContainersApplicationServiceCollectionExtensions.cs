using System;
using System.Reflection;
using Contr.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Contr.Modules.Containers.Application;

/// <summary>Registers the Containers application layer: its message handlers.</summary>
public static class ContainersApplicationServiceCollectionExtensions
{
    public static Assembly Assembly => typeof(ContainersApplicationServiceCollectionExtensions).Assembly;

    public static IServiceCollection AddContainersApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMediator(Assembly);

        return services;
    }
}
