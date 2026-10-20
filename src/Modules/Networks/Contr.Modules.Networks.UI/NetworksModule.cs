using System;
using System.Collections.Generic;
using Contr.Modules.Networks.Application;
using Contr.Modules.Networks.Application.Abstractions;
using Contr.Modules.Networks.UI.Infrastructure;
using Contr.Modules.Networks.UI.ViewModels;
using Contr.Modules.Networks.UI.Views;
using Contr.UI.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contr.Modules.Networks.UI;

/// <summary>Composition entry point of the Networks module.</summary>
public sealed class NetworksModule : IModule
{
    public string Name => "Networks";

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddNetworksApplication();
        services.TryAddSingleton<INetworksGateway, NetworksGateway>();
        services.AddTransient<NetworksViewModel>();
        services.AddTransient<NetworksPage>();
    }

    public IReadOnlyList<NavigationItem> GetNavigationItems()
    {
        return [new NavigationItem("Networks", "", typeof(NetworksPage))];
    }
}
