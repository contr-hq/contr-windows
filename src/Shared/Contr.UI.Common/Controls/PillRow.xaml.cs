using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.UI.Common.Controls;

/// <summary>A page's summary pills, in a row.</summary>
public sealed partial class PillRow : UserControl
{
    public static readonly DependencyProperty PillsProperty = DependencyProperty.Register(
        nameof(Pills), typeof(IReadOnlyList<PillItem>), typeof(PillRow), new PropertyMetadata(null));

    public PillRow()
    {
        InitializeComponent();
    }

    public IReadOnlyList<PillItem>? Pills
    {
        get => (IReadOnlyList<PillItem>?)GetValue(PillsProperty);
        set => SetValue(PillsProperty, value);
    }
}
