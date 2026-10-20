using Contr.UI.Common.Engine;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.UI.Common.Controls;

/// <summary>What an engine-backed page shows while the engine is not running: its state and a way to start it.</summary>
public sealed partial class EngineGate : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(EngineGateViewModel), typeof(EngineGate), new PropertyMetadata(null));

    public EngineGate()
    {
        InitializeComponent();
    }

    public EngineGateViewModel ViewModel
    {
        get => (EngineGateViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static bool Any(bool first, bool second)
    {
        return first || second;
    }

    public static Visibility ShowsEither(bool first, bool second)
    {
        return first || second ? Visibility.Visible : Visibility.Collapsed;
    }
}
