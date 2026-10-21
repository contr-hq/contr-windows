using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.UI.Common.Controls;

/// <summary>One count along the top of a page: a coloured icon square, a label, a value and a detail line.</summary>
/// <remarks>A control rather than a shared template, so pages in module assemblies can use it.</remarks>
public sealed partial class StatCardView : UserControl
{
    /// <summary>The card to draw; null draws an empty card.</summary>
    public static readonly DependencyProperty CardProperty = DependencyProperty.Register(
        nameof(Card), typeof(StatCard), typeof(StatCardView), new PropertyMetadata(null));

    public StatCardView()
    {
        InitializeComponent();
    }

    public StatCard? Card
    {
        get => (StatCard?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    /// <summary>Shows the square for <paramref name="kind"/>; x:Bind arguments cannot name enum members.</summary>
    public static Visibility When(StatCard? card, string kind)
    {
        return card?.Kind.ToString() == kind ? Visibility.Visible : Visibility.Collapsed;
    }

    public static string GlyphOf(StatCard? card)
    {
        return card?.Glyph ?? string.Empty;
    }

    public static string LabelOf(StatCard? card)
    {
        return card?.Label ?? string.Empty;
    }

    public static string ValueOf(StatCard? card)
    {
        return card?.Value ?? string.Empty;
    }

    public static string DetailOf(StatCard? card)
    {
        return card?.Detail ?? string.Empty;
    }

    public static Visibility HasDetail(StatCard? card)
    {
        return string.IsNullOrEmpty(card?.Detail) ? Visibility.Collapsed : Visibility.Visible;
    }
}
