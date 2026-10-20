using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Contr.UI.Common.Controls;

/// <summary>
/// The top of every page: the title or a breadcrumb trail, an optional subtitle line, and the page's
/// commands (the control's XAML content) on the right. Usage: docs/architecture.md, "Pages".
/// </summary>
[ContentProperty(Name = nameof(Commands))]
public sealed partial class PageHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    /// <summary>Text or content under the title, such as counts; null or empty hides it.</summary>
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    /// <summary>The crumbs shown instead of the title, the current page last; empty shows the title.</summary>
    public static readonly DependencyProperty TrailProperty = DependencyProperty.Register(
        nameof(Trail), typeof(IReadOnlyList<string>), typeof(PageHeader), new PropertyMetadata(null));

    public static readonly DependencyProperty CommandsProperty = DependencyProperty.Register(
        nameof(Commands), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    public PageHeader()
    {
        InitializeComponent();
    }

    /// <summary>Raised with the index of a clicked crumb; the last crumb, the current page, never raises it.</summary>
    public event EventHandler<int>? TrailClicked;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
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

    public static Visibility WhenPresent(object? value)
    {
        return value is null or "" ? Visibility.Collapsed : Visibility.Visible;
    }

    public static Visibility WhenAny(IReadOnlyList<string>? trail)
    {
        return trail is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility WhenEmpty(IReadOnlyList<string>? trail)
    {
        return trail is { Count: > 0 } ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnTrailItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (Trail is { } trail && args.Index < trail.Count - 1)
        {
            TrailClicked?.Invoke(this, args.Index);
        }
    }
}
