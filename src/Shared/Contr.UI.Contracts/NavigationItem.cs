using System;

namespace Contr.UI.Contracts;

/// <summary>Where the shell places a navigation item.</summary>
public enum NavigationItemPlacement
{
    /// <summary>The main menu area of the navigation pane.</summary>
    Menu,

    /// <summary>Below a separator after the main menu, for tools such as Troubleshoot.</summary>
    Tools,

    /// <summary>The footer area of the navigation pane, next to the built-in Settings item.</summary>
    Footer,
}

/// <summary>A navigation destination a module contributes to the host shell.</summary>
/// <param name="Title">The label shown in the navigation menu.</param>
/// <param name="IconGlyph">A Segoe Fluent Icons glyph, written as an escape such as <c>\uE7B8</c>.</param>
/// <param name="PageType">The page navigated to when the item is selected.</param>
/// <param name="Placement">Where the shell places the item.</param>
public sealed record NavigationItem(
    string Title,
    string IconGlyph,
    Type PageType,
    NavigationItemPlacement Placement = NavigationItemPlacement.Menu)
{
    /// <summary>
    /// Resolves the text shown at the end of the item, such as a count; an empty string hides it.
    /// </summary>
    /// <remarks>The observable may emit on any thread; the shell marshals to the UI thread.</remarks>
    public Func<IServiceProvider, IObservable<string>>? Badge { get; init; }

    /// <summary>The item's position within its placement; lower comes first.</summary>
    public int Order { get; init; }
}
