using System;
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

    /// <summary>
    /// Asks for one line of text and has <paramref name="apply"/> apply it before closing. Apply returns why it
    /// failed, or <see langword="null"/>; a failure shows at the top of the dialog, which stays open.
    /// </summary>
    /// <returns>Whether the text was applied.</returns>
    Task<bool> ShowPromptAsync(
        string title, string label, string initialValue, string primaryText, Func<string, Task<string?>> apply);
}
