using System;
using Contr.UI.Contracts;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>The Kubernetes page: its layout, with every control off until the daemon runs a cluster.</summary>
public sealed partial class KubernetesPage : Page, IHasViewModel
{
    public KubernetesPage(KubernetesViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();
    }

    public KubernetesViewModel ViewModel { get; }

    Contr.UI.Contracts.ViewModel IHasViewModel.ViewModel => ViewModel;
}
