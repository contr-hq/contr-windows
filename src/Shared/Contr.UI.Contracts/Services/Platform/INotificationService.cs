using Microsoft.UI.Xaml.Controls;

namespace Contr.UI.Contracts.Services.Platform;

/// <summary>Shows transient, in-app notifications in an <see cref="InfoBar"/> under the page's breadcrumb.</summary>
public interface INotificationService : IUiContractService, ISingletonDependency
{
    /// <summary>Binds the service to the bar notifications show in; the shell rebinds it for each page.</summary>
    void Initialize(InfoBar infoBar);

    /// <summary>Shows a notification with the given severity.</summary>
    void Show(
        string message,
        InfoBarSeverity severity = InfoBarSeverity.Informational,
        string? title = null);

    /// <summary>Closes the notification showing, if any.</summary>
    void Close();
}
