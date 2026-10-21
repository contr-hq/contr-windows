using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.UI.Common.Controls;

/// <summary>Up to four <see cref="StatCard"/>s in equal columns, under a page's header.</summary>
public sealed partial class StatStrip : UserControl
{
    public static readonly DependencyProperty CardsProperty = DependencyProperty.Register(
        nameof(Cards), typeof(IReadOnlyList<StatCard>), typeof(StatStrip), new PropertyMetadata(null));

    public StatStrip()
    {
        InitializeComponent();
    }

    public IReadOnlyList<StatCard>? Cards
    {
        get => (IReadOnlyList<StatCard>?)GetValue(CardsProperty);
        set => SetValue(CardsProperty, value);
    }
}
