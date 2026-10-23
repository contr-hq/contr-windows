using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.Services.Platform;

namespace Contr.App.Hosting.Orchestration;

/// <summary>
/// Shows the notification-area icon, which keeps Contr reachable while its window is hidden, and starts the Windows
/// notifications for work that ends meanwhile.
/// </summary>
public sealed class TrayOrchestrator : IStartupOrchestrator
{
    private readonly TrayService _tray;
    private readonly ActivityToasts _toasts;

    public TrayOrchestrator(TrayService tray, ActivityToasts toasts)
    {
        ArgumentNullException.ThrowIfNull(tray);
        ArgumentNullException.ThrowIfNull(toasts);
        _tray = tray;
        _toasts = toasts;
    }

    public int Order => StartupOrder.Tray;

    public string Name => nameof(TrayOrchestrator);

    public string Description => "Adding the tray icon...";

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _tray.Show();
        _toasts.Start();

        return Task.CompletedTask;
    }
}
