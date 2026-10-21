using System;
using System.Collections.Generic;
using Contr.Modules.Images.Application;
using Contr.Modules.Images.Application.Abstractions;
using Contr.Modules.Images.UI.Infrastructure;
using Contr.Modules.Images.UI.ViewModels;
using Contr.Modules.Images.UI.Views;
using Contr.UI.Common.Icons;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contr.Modules.Images.UI;

/// <summary>Composition entry point of the Images module.</summary>
public sealed class ImagesModule : IModule
{
    public string Name => "Images";

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddImagesApplication();
        services.TryAddSingleton<IImagesGateway, ImagesGateway>();
        services.AddSingleton<ISearchSource, ImageSearch>();
        services.AddTransient<ImagesViewModel>();
        services.AddTransient<ImagesPage>();
    }

    public IReadOnlyList<NavigationItem> GetNavigationItems()
    {
        return [new NavigationItem("Images", ContrIcons.Images, typeof(ImagesPage)) { Order = 20 }];
    }
}
