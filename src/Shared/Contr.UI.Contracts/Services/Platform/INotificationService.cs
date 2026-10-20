using Microsoft.UI.Xaml.Controls;

namespace Contr.UI.Contracts.Services.Platform;

/// <summary>Shows transient, in-app notifications through the shell's <see cref="InfoBar"/>.</summary>
public interface INotificationService : IUiContractService, ISingletonDependency
{
    /// <summary>Binds the service to the shell's notification bar. Call once, on load.</summary>
    void Initialize(InfoBar infoBar);

    /// <summary>Shows a notification with the given severity.</summary>
    void Show(
        string message,
        InfoBarSeverity severity = InfoBarSeverity.Informational,
        string? title = null);
}
