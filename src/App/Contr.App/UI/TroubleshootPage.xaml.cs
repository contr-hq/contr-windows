using System;
using Contr.UI.Contracts;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;

namespace Contr.App.UI;

/// <summary>Why the engine is down, the checks the client can make, and the client's log.</summary>
public sealed partial class TroubleshootPage : Page, IHasViewModel
{
    public TroubleshootPage(TroubleshootViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();

        // The newest lines are the ones worth reading, so the log opens scrolled to its end.
        LogScroller.RegisterPropertyChangedCallback(ScrollViewer.ExtentHeightProperty, (_, _) => ScrollLogToEnd());
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public TroubleshootViewModel ViewModel { get; }

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    /// <summary>A failed check's name stands out from the ones that passed.</summary>
    public static FontWeight Weight(bool isFailed)
    {
        return isFailed ? FontWeights.SemiBold : FontWeights.Normal;
    }

    /// <summary>Continuation lines, such as stack frames, recede behind the entries they belong to.</summary>
    public static double LineOpacity(string level)
    {
        return level.Length > 0 ? 1 : 0.6;
    }

    public static Visibility Not(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ScrollLogToEnd()
    {
        LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null, disableAnimation: true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Activate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Deactivate();
    }

    private void OnRunAgain(object sender, RoutedEventArgs e)
    {
        ViewModel.RunAgain();
    }
}
