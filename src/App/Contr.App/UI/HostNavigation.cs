using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using Contr.UI.Common.Icons;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.Extensions.DependencyInjection;

namespace Contr.App.UI;

/// <summary>The pages the shell owns rather than a module.</summary>
public static class HostNavigation
{
    public const string SettingsPageKey = "Settings";

    public const string TroubleshootPageKey = "Troubleshoot";

    /// <summary>The Containers module's list page, which the tray opens.</summary>
    public const string ContainersPageKey = "Containers";

    /// <summary>The shell's pages; the navigation pane draws Settings as its built-in footer item.</summary>
    public static IReadOnlyList<Type> ShellPages { get; } = [
        typeof(SettingsPage),
        typeof(KubernetesPage),
        typeof(TroubleshootPage),
    ];

    /// <summary>The pane items of the shell's own pages, placed among the modules' items by order.</summary>
    public static IReadOnlyList<NavigationItem> Items { get; } = [
        new NavigationItem("Kubernetes", ContrIcons.Kubernetes, typeof(KubernetesPage)) {
            Order = 60,
            State = services => services
                .GetRequiredService<IKubernetesCluster>()
                .Changes
                .Select(cluster => StateOf(cluster?.State)),
        },
        new NavigationItem(
            "Troubleshoot", ContrIcons.Troubleshoot, typeof(TroubleshootPage), NavigationItemPlacement.Tools),
    ];

    /// <summary>A spinner while the cluster starts or stops, a dot while it runs or after it failed.</summary>
    public static NavigationItemState StateOf(ClusterState? state)
    {
        return state switch {
            ClusterState.Starting or ClusterState.Stopping => NavigationItemState.Busy,
            ClusterState.Ready => NavigationItemState.Good,
            ClusterState.Failed => NavigationItemState.Critical,
            _ => NavigationItemState.None,
        };
    }
}
