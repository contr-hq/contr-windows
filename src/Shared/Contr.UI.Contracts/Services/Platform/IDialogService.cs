using System.Threading.Tasks;

namespace Contr.UI.Contracts.Services.Platform;

/// <summary>Shows modal content dialogs anchored to the main window.</summary>
public interface IDialogService : IUiContractService, ISingletonDependency
{
    /// <summary>Shows an informational dialog with a single dismiss button.</summary>
    Task ShowMessageAsync(string title, string message, string closeText = "OK");

    /// <summary>Asks before a destructive action; the close button is the default.</summary>
    /// <returns><see langword="true"/> if the user chose the primary action.</returns>
    Task<bool> ShowConfirmAsync(
        string title, string message, string primaryText = "Yes", string closeText = "No");

    /// <summary>Asks for one line of text.</summary>
    /// <returns>
    /// What was typed, or <see langword="null"/> when the dialog was dismissed, so "typed nothing"
    /// and "cancelled" stay distinguishable.
    /// </returns>
    Task<string?> ShowPromptAsync(
        string title, string label, string initialValue = "", string primaryText = "OK");
}
