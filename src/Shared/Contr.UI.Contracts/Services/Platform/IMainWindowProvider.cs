using Microsoft.UI.Xaml;

namespace Contr.UI.Contracts.Services.Platform;

/// <summary>Exposes the application's main window to code that positions or owns other windows.</summary>
public interface IMainWindowProvider : IUiContractService
{
    /// <summary>The main window, or <see langword="null"/> until the shell has been created.</summary>
    Window? MainWindow { get; }
}
