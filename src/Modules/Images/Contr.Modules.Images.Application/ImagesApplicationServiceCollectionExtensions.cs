using System;
using System.Reflection;
using Contr.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Contr.Modules.Images.Application;

/// <summary>Registers the Images application layer: its message handlers.</summary>
public static class ImagesApplicationServiceCollectionExtensions
{
    public static Assembly Assembly => typeof(ImagesApplicationServiceCollectionExtensions).Assembly;

    public static IServiceCollection AddImagesApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMediator(Assembly);

        return services;
    }
}
