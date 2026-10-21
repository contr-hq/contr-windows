using System;
using System.Threading.Tasks;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.UI.Contracts.Services.Containers;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Contr.Modules.Containers.UI.Views;

/// <summary>Opens the Run dialog over the main window.</summary>
public sealed class ContainerRunner : IContainerRunner
{
    private readonly IServiceProvider _services;
    private readonly IMainWindowProvider _mainWindow;

    public ContainerRunner(IServiceProvider services, IMainWindowProvider mainWindow)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(mainWindow);
        _services = services;
        _mainWindow = mainWindow;
    }

    public async Task RunAsync(string image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (_mainWindow.MainWindow is not { Content: FrameworkElement root } window)
        {
            return;
        }

        var viewModel = _services.GetRequiredService<RunContainerViewModel>();
        viewModel.Image = image;

        // A dialog's popup does not inherit the theme the window content requests.
        var dialog = new RunContainerDialog(viewModel, window) {
            XamlRoot = root.XamlRoot,
            RequestedTheme = root.ActualTheme,
        };

        await dialog.ShowAsync();
    }
}
