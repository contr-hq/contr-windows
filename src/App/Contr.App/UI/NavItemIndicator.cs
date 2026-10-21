using Contr.UI.Common.Helpers;
using Contr.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace Contr.App.UI;

/// <summary>The end of a navigation item: a spinner or a coloured dot for its page's subject.</summary>
public sealed partial class NavItemIndicator : Grid
{
    private readonly ProgressRing _spinner = new() {
        Width = 16,
        Height = 16,
        MinWidth = 0,
        MinHeight = 0,
        Visibility = Visibility.Collapsed,
    };

    private readonly Ellipse _dot = new() { Width = 8, Height = 8, Visibility = Visibility.Collapsed };

    public NavItemIndicator()
    {
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_spinner);
        Children.Add(_dot);
    }

    public void Show(NavigationItemState state)
    {
        var isBusy = state == NavigationItemState.Busy;
        _spinner.IsActive = isBusy;
        _spinner.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        _dot.Visibility = state is NavigationItemState.Good or NavigationItemState.Critical
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (_dot.Visibility == Visibility.Visible)
        {
            _dot.Fill = ThemeResources.Brush(
                this,
                state == NavigationItemState.Good ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush");
        }

        AutomationProperties.SetName(this, state switch {
            NavigationItemState.Busy => "Busy",
            NavigationItemState.Good => "Running",
            NavigationItemState.Critical => "Failed",
            _ => string.Empty,
        });
    }
}
