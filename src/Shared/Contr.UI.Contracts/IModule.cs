using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Contr.UI.Contracts;

/// <summary>
/// A self-contained feature module: it registers its own services and contributes navigation
/// entries to the shell. Modules never reference one another.
/// </summary>
public interface IModule
{
    /// <summary>A stable, human-readable module name used in diagnostics.</summary>
    string Name { get; }

    /// <summary>Registers the module's application and adapter services.</summary>
    void RegisterServices(IServiceCollection services);

    /// <summary>The navigation entries this module contributes to the shell.</summary>
    IReadOnlyList<NavigationItem> GetNavigationItems();

    /// <summary>Pages reached from the module's pages rather than the navigation pane, such as a detail page.</summary>
    IReadOnlyList<Type> GetPages()
    {
        return [];
    }
}
