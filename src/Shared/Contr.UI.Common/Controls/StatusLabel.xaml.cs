using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.UI.Common.Controls;

/// <summary>The icon a <see cref="StatusLabel"/> draws; the label's text always names the state too.</summary>
public enum StatusKind
{
    Neutral,
    Good,
    Caution,
    Critical,
    Paused,
    Busy,
}

/// <summary>A state as an icon, a word and an optional detail such as an age.</summary>
public sealed partial class StatusLabel : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(StatusKind), typeof(StatusLabel), new PropertyMetadata(StatusKind.Neutral));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(StatusLabel), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(StatusLabel), new PropertyMetadata(string.Empty));

    public StatusLabel()
    {
        InitializeComponent();
    }

    public StatusKind Kind
    {
        get => (StatusKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>Shows the icon named <paramref name="shown"/>; x:Bind arguments cannot name enum members.</summary>
    public static Visibility When(StatusKind kind, string shown)
    {
        return kind.ToString() == shown ? Visibility.Visible : Visibility.Collapsed;
    }

    public static bool IsBusy(StatusKind kind)
    {
        return kind == StatusKind.Busy;
    }
}
