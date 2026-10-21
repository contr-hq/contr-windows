using System;
using Contr.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>Why the engine is down, the checks the client can make, and the client's log.</summary>
public sealed partial class TroubleshootPage : Page, IHasViewModel
{
    public TroubleshootPage(TroubleshootViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public TroubleshootViewModel ViewModel { get; }

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;

    public static string Summary(string summary)
    {
        return summary.Length > 0 ? $"· {summary}" : string.Empty;
    }

    public static string OrEmpty(string log)
    {
        return log.Length > 0 ? log : "The client has written no log yet.";
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
