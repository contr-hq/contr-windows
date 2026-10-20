using System;
using System.Collections.Generic;

namespace Contr.App.UI;

/// <summary>The pages the shell owns rather than a module.</summary>
public static class HostNavigation
{
    public const string SettingsPageKey = "Settings";

    /// <summary>Settings, which the navigation pane draws as its built-in footer item.</summary>
    public static IReadOnlyList<Type> ShellPages { get; } = [typeof(SettingsPage)];
}
