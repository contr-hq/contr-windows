using System;
using System.Collections.Generic;
using Contr.Modules.Volumes.Application;
using Contr.Modules.Volumes.Application.Abstractions;
using Contr.Modules.Volumes.UI.Infrastructure;
using Contr.Modules.Volumes.UI.ViewModels;
using Contr.Modules.Volumes.UI.Views;
using Contr.UI.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contr.Modules.Volumes.UI;

/// <summary>Composition entry point of the Volumes module.</summary>
public sealed class VolumesModule : IModule
{
    public string Name => "Volumes";

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddVolumesApplication();
        services.TryAddSingleton<IVolumesGateway, VolumesGateway>();
        services.AddTransient<VolumesViewModel>();
        services.AddTransient<VolumesPage>();
    }

    public IReadOnlyList<NavigationItem> GetNavigationItems()
    {
        return [new NavigationItem("Volumes", "", typeof(VolumesPage))];
    }
}
