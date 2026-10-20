using System;
using System.Collections.Generic;
using Contr.Modules.Containers.Application;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.UI.Infrastructure;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.Modules.Containers.UI.Views;
using Contr.UI.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contr.Modules.Containers.UI;

/// <summary>Composition entry point of the Containers module.</summary>
public sealed class ContainersModule : IModule
{
    public string Name => "Containers";

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddContainersApplication();
        services.TryAddSingleton<IContainersGateway, ContainersGateway>();
        services.AddTransient<ContainersViewModel>();
        services.AddTransient<ContainersPage>();
    }

    public IReadOnlyList<NavigationItem> GetNavigationItems()
    {
        return [new NavigationItem("Containers", "", typeof(ContainersPage))];
    }
}
