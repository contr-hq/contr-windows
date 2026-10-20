using System;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>Confirms a factory reset, then runs it and follows its steps.</summary>
public sealed partial class FactoryResetDialog : ContentDialog
{
    public FactoryResetDialog(TroubleshootViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();
    }

    public TroubleshootViewModel ViewModel { get; }

    /// <summary>Only once the user said they understand, and not while a reset runs.</summary>
    public static bool CanDelete(bool? understood, bool isResetting)
    {
        return understood == true && !isResetting;
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        var error = await ViewModel.ResetAsync(EngineResetScope.Factory);

        args.Cancel = error is not null;
        ResetError.Message = error ?? string.Empty;
        ResetError.IsOpen = error is not null;
        ResetError.Visibility = error is not null ? Visibility.Visible : Visibility.Collapsed;
        deferral.Complete();
    }
}
