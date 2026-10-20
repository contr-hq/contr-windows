using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Contr.UI.Common.Controls;

/// <summary>
/// The top strip of every page: a breadcrumb on the left, the page's commands (the control's XAML
/// content) on the right. Usage: the "Page skeleton" section of docs/architecture.md.
/// </summary>
[ContentProperty(Name = nameof(Commands))]
public sealed partial class PageHeader : UserControl
{
    /// <summary>The trail's first crumb, the product name on every page; empty hides it.</summary>
    public static readonly DependencyProperty RootProperty = DependencyProperty.Register(
        nameof(Root), typeof(string), typeof(PageHeader), new PropertyMetadata("Contr"));

    /// <summary>The area this page belongs to — the muted middle crumb. Empty hides the crumb.</summary>
    public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
        nameof(Section), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    /// <summary>The page's own name — the bold last crumb.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    /// <summary>The page's actions, shown inside the bordered command group.</summary>
    public static readonly DependencyProperty CommandsProperty = DependencyProperty.Register(
        nameof(Commands), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    public PageHeader()
    {
        InitializeComponent();
    }

    public string Root
    {
        get => (string)GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    public string Section
    {
        get => (string)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Commands
    {
        get => GetValue(CommandsProperty);
        set => SetValue(CommandsProperty, value);
    }

    /// <summary>Shows a crumb and its chevron only when there is something to put in it.</summary>
    public static Visibility WhenSet(string value)
    {
        return string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Shows the command group only when a page contributed commands.</summary>
    public static Visibility WhenPresent(object? value)
    {
        return value is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
