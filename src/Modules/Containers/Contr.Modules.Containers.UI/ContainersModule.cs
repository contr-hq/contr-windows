using System;
using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;
using Contr.Modules.Containers.UI.Infrastructure;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.Modules.Containers.UI.Views;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Containers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contr.Modules.Containers.UI;

/// <summary>Composition entry point of the Containers module.</summary>
public sealed class ContainersModule : IModule
{
    private static readonly TimeSpan _badgeRetryDelay = TimeSpan.FromSeconds(5);

    public string Name => "Containers";

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddContainersApplication();
        services.TryAddSingleton<IContainersGateway, ContainersGateway>();
        services.TryAddSingleton<IRunningContainers, RunningContainers>();
        services.AddTransient<ContainersViewModel>();
        services.AddTransient<ContainersPage>();
        services.AddTransient<ContainerDetailViewModel>();
        services.AddTransient<ContainerDetailPage>();
        services.AddTransient<ComposeViewModel>();
        services.AddTransient<ComposePage>();
    }

    public IReadOnlyList<NavigationItem> GetNavigationItems()
    {
        var containers = new NavigationItem("Containers", "\uE7B8", typeof(ContainersPage)) {
            Badge = CountContainers,
            Order = 10,
        };
        var compose = new NavigationItem("Compose", "\uE8FD", typeof(ComposePage)) { Order = 50 };

        return [containers, compose];
    }

    public IReadOnlyList<Type> GetPages()
    {
        return [typeof(ContainerDetailPage)];
    }

    private static IObservable<string> CountContainers(IServiceProvider services)
    {
        var sender = services.GetRequiredService<ISender>();

        return ListBadge.Count<ContainerDto>(
            cancellationToken => sender.Send(new WatchContainersQuery(), cancellationToken),
            container => container.Id.Value,
            _badgeRetryDelay);
    }
}
