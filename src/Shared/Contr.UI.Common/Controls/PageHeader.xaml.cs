using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Contr.UI.Common.Controls;

/// <summary>
/// The top of every page: the title or a breadcrumb trail, an optional subtitle line, and the page's commands
/// (the control's XAML content) in one bordered command group. Usage: docs/architecture.md, "Pages".
/// </summary>
/// <remarks>
/// Commands use <c>AccentToolbarButtonStyle</c> for the page's one primary action, <c>ToolbarButtonStyle</c> for the
/// rest, <c>DangerToolbarButtonStyle</c> for a destructive one and <c>CommandBarDividerStyle</c> between sets.
/// </remarks>
[ContentProperty(Name = nameof(Commands))]
public sealed partial class PageHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    /// <summary>Text or content under the title, such as a status line; null or empty hides it.</summary>
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    /// <summary>The crumbs shown instead of the title, the current page last; empty shows the title.</summary>
    public static readonly DependencyProperty TrailProperty = DependencyProperty.Register(
        nameof(Trail), typeof(IReadOnlyList<string>), typeof(PageHeader), new PropertyMetadata(null));

    /// <summary>The page's actions; the group is hidden while they are null or collapsed.</summary>
    public static readonly DependencyProperty CommandsProperty = DependencyProperty.Register(
        nameof(Commands), typeof(object), typeof(PageHeader), new PropertyMetadata(null, OnCommandsChanged));

    private long _visibilityToken;

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

    private static void OnCommandsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var header = (PageHeader)sender;

        if (e.OldValue is UIElement old)
        {
            old.UnregisterPropertyChangedCallback(VisibilityProperty, header._visibilityToken);
        }

        if (e.NewValue is UIElement commands)
        {
            header._visibilityToken = commands.RegisterPropertyChangedCallback(
                VisibilityProperty, (_, _) => header.ShowCommandGroup());
        }

        header.ShowCommandGroup();
    }

    private void ShowCommandGroup()
    {
        CommandGroup.Visibility = Commands is UIElement { Visibility: Visibility.Visible } or string { Length: > 0 }
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnTrailItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (Trail is { } trail && args.Index < trail.Count - 1)
        {
            TrailClicked?.Invoke(this, args.Index);
        }
    }
}
