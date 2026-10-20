using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.UI.Common.Controls;

/// <summary>The <c>‹ 1 / 2 ›</c> in a card's header; the page shows it only while there is a second page.</summary>
public sealed partial class Pager : UserControl
{
    /// <summary>Such as <c>1 / 2</c>.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(Pager), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PreviousCommandProperty = DependencyProperty.Register(
        nameof(PreviousCommand), typeof(ICommand), typeof(Pager), new PropertyMetadata(null));

    public static readonly DependencyProperty NextCommandProperty = DependencyProperty.Register(
        nameof(NextCommand), typeof(ICommand), typeof(Pager), new PropertyMetadata(null));

    public Pager()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? PreviousCommand
    {
        get => (ICommand?)GetValue(PreviousCommandProperty);
        set => SetValue(PreviousCommandProperty, value);
    }

    public ICommand? NextCommand
    {
        get => (ICommand?)GetValue(NextCommandProperty);
        set => SetValue(NextCommandProperty, value);
    }
}
