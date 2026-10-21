using System;
using System.Collections.Generic;
using Contr.UI.Contracts;

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
        new NavigationItem("Kubernetes", "\uEA38", typeof(KubernetesPage)) { Order = 60 },
        new NavigationItem("Troubleshoot", "\uE90F", typeof(TroubleshootPage), NavigationItemPlacement.Tools),
    ];
}
