using System;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.Domain;

namespace Contr.Modules.Networks.UI.ViewModels;

/// <summary>One row of the networks list.</summary>
/// <param name="Subnets">Address ranges, such as <c>172.20.0.0/16</c>; empty when the network has none.</param>
/// <param name="IsSelected">Whether the details panel shows this network.</param>
public sealed record NetworkRow(
    NetworkId NetworkId,
    string Name,
    string Project,
    string Driver,
    string Scope,
    string Subnets,
    string Gateway,
    bool Internal,
    bool IsSelected = false)
{
    public string Id => NetworkId.Value;

    public string ShortId => NetworkId.Short;

    public bool HasProject => Project.Length > 0;

    /// <summary>One of the engine's own networks, which cannot be removed.</summary>
    public bool IsBuiltIn => PredefinedNetworks.IsPredefined(Name);

    public bool CanRemove => !IsBuiltIn;

    public string SubnetText => Subnets.Length > 0 ? Subnets : "—";

    /// <summary>Such as <c>bridge · gateway 172.20.0.1</c>.</summary>
    public string Details => Gateway.Length > 0 ? $"{Driver} · gateway {Gateway}" : Driver;

    /// <summary>Why the network cannot be deleted here, or how Compose removes it; empty otherwise.</summary>
    public string DeleteNote => IsBuiltIn
        ? "The engine created this network and keeps it."
        : HasProject
            ? $"Compose manages this network. Run Down on {Project} to remove it."
            : string.Empty;

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
            network.Gateway,
            network.Internal);
    }

    /// <summary>User-defined networks by name, then the engine's own.</summary>
    public static int Compare(NetworkRow? left, NetworkRow? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var byKind = left.IsBuiltIn.CompareTo(right.IsBuiltIn);

        return byKind != 0 ? byKind : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}
