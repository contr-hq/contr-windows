using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.Services.Platform;

namespace Contr.App.Hosting.Orchestration;

/// <summary>Shows the notification-area icon, which keeps Contr reachable while its window is hidden.</summary>
public sealed class TrayOrchestrator : IStartupOrchestrator
{
    private readonly TrayService _tray;

    public TrayOrchestrator(TrayService tray)
    {
        ArgumentNullException.ThrowIfNull(tray);
        _tray = tray;
    }

    public int Order => StartupOrder.Tray;

    public string Name => nameof(TrayOrchestrator);

    public string Description => "Adding the tray icon...";

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _tray.Show();

        return Task.CompletedTask;
    }
}
