using System;
using System.Reflection;
using Contr.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Contr.Modules.Volumes.Application;

/// <summary>Registers the Volumes application layer: its message handlers.</summary>
public static class VolumesApplicationServiceCollectionExtensions
{
    public static Assembly Assembly => typeof(VolumesApplicationServiceCollectionExtensions).Assembly;

    public static IServiceCollection AddVolumesApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMediator(Assembly);

        return services;
    }
}
