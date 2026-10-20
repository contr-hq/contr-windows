using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.UI.Common.Controls;

/// <summary>
/// A rounded label: a dot and a word for a state, a lock for a managed setting, or a plain count. The dot always
/// comes with text, so a state never rests on colour alone.
/// </summary>
public sealed partial class PillView : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(PillView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(PillKind), typeof(PillView), new PropertyMetadata(PillKind.Plain));

    public PillView()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public PillKind Kind
    {
        get => (PillKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>The pill for a <see cref="StatusKind"/>, so status pills and labels agree on colour.</summary>
    public static PillKind For(StatusKind kind)
    {
        return kind switch {
            StatusKind.Good => PillKind.Good,
            StatusKind.Busy => PillKind.Busy,
            StatusKind.Caution or StatusKind.Paused => PillKind.Caution,
            StatusKind.Critical => PillKind.Critical,
            _ => PillKind.Hollow,
        };
    }

    /// <summary>Shows a part for <paramref name="shown"/>; x:Bind arguments cannot name enum members.</summary>
    public static Visibility When(PillKind kind, string shown)
    {
        return kind.ToString() == shown ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility Unless(PillKind kind, string first, string second)
    {
        return new[] { first, second }.Contains(kind.ToString()) ? Visibility.Collapsed : Visibility.Visible;
    }
}
