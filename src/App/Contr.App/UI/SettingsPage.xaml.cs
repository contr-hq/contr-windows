using System;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>Appearance, the connected daemon, and where the logs are.</summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        ViewModel = viewModel;

        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }
}
