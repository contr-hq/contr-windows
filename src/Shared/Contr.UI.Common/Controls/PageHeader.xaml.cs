using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Contr.UI.Common.Controls;

/// <summary>One crumb of a <see cref="PageHeader"/>'s trail that leads back to an ancestor page.</summary>
/// <param name="Index">Its position in <see cref="PageHeader.Trail"/>.</param>
public sealed record Crumb(int Index, string Text);

/// <summary>
/// The top row of every page: a breadcrumb that starts at the product and ends at the page, then the page's
/// actions (the control's XAML content) on the right. Usage: docs/architecture.md, "Pages".
/// </summary>
/// <remarks>
/// The product and the ancestors are tertiary and the page itself SemiBold, all at body size; a page has no
/// title of its own. Actions are ordinary buttons, at most one of them accent.
/// </remarks>
[ContentProperty(Name = nameof(Commands))]
public sealed partial class PageHeader : UserControl
{
    /// <summary>The trail's first crumb; every page names the product the same way.</summary>
    public static readonly DependencyProperty RootProperty = DependencyProperty.Register(
        nameof(Root), typeof(string), typeof(PageHeader), new PropertyMetadata("Contr"));

    /// <summary>The page's own name when it has no ancestors.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    /// <summary>The crumbs after the product, the current page last; empty shows <see cref="Title"/>.</summary>
    public static readonly DependencyProperty TrailProperty = DependencyProperty.Register(
        nameof(Trail), typeof(IReadOnlyList<string>), typeof(PageHeader), new PropertyMetadata(null));

    /// <summary>The page's actions.</summary>
    public static readonly DependencyProperty CommandsProperty = DependencyProperty.Register(
        nameof(Commands), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    public PageHeader()
    {
        InitializeComponent();
    }

    /// <summary>Raised with the <see cref="Trail"/> index of a clicked ancestor.</summary>
    public event EventHandler<int>? TrailClicked;

    public string Root
    {
        get => (string)GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IReadOnlyList<string>? Trail
    {
        get => (IReadOnlyList<string>?)GetValue(TrailProperty);
        set => SetValue(TrailProperty, value);
    }

    public object? Commands
    {
        get => GetValue(CommandsProperty);
        set => SetValue(CommandsProperty, value);
    }

    /// <summary>Where the shell shows notifications while this page is open.</summary>
    public InfoBar Notice => NoticeBar;

    /// <summary>Every crumb of <paramref name="trail"/> but the last, which is the current page.</summary>
    public static IReadOnlyList<Crumb> Ancestors(IReadOnlyList<string>? trail)
    {
        return trail is { Count: > 1 }
            ? [.. trail
                .Take(trail.Count - 1)
                .Select((text, index) => new Crumb(index, text))]
            : [];
    }

    public static string Current(string title, IReadOnlyList<string>? trail)
    {
        return trail is { Count: > 0 } ? trail[^1] : title;
    }

    private void OnCrumbClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: int index })
        {
            TrailClicked?.Invoke(this, index);
        }
    }
}
