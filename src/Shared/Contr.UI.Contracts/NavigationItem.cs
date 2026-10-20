using System;

namespace Contr.UI.Contracts;

/// <summary>Where the shell places a navigation item.</summary>
public enum NavigationItemPlacement
{
    /// <summary>The main menu area of the navigation pane.</summary>
    Menu,

    /// <summary>The footer area of the navigation pane, next to the built-in Settings item.</summary>
    Footer,
}

/// <summary>A navigation destination a module contributes to the host shell.</summary>
/// <param name="Title">The label shown in the navigation menu.</param>
/// <param name="IconGlyph">A Segoe Fluent Icons glyph, written as an escape such as <c></c>.</param>
/// <param name="PageType">The page navigated to when the item is selected.</param>
/// <param name="Placement">Where the shell places the item.</param>
public sealed record NavigationItem(
    string Title,
    string IconGlyph,
    Type PageType,
    NavigationItemPlacement Placement = NavigationItemPlacement.Menu);
