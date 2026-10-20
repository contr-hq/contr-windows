using System;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.Domain;

namespace Contr.Modules.Networks.UI.ViewModels;

/// <summary>One row of the networks list.</summary>
public sealed record NetworkRow(
    NetworkId NetworkId,
    string Name,
    string Project,
    string Driver,
    string Scope,
    string Subnets,
    bool Internal)
{
    public string Id => NetworkId.Value;

    public string ShortId => NetworkId.Short;

    public bool HasProject => Project.Length > 0;

    public bool CanRemove => !PredefinedNetworks.IsPredefined(Name);

    public static NetworkRow From(NetworkDto network)
    {
        ArgumentNullException.ThrowIfNull(network);

        return new NetworkRow(
            network.Id,
            network.Name,
            network.ComposeProject,
            network.Driver,
            network.Scope,
            string.Join(", ", network.Subnets),
            network.Internal);
    }

    /// <summary>The engine's own networks first, then the rest by name.</summary>
    public static int Compare(NetworkRow? left, NetworkRow? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var byKind = left.CanRemove.CompareTo(right.CanRemove);

        return byKind != 0 ? byKind : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}
