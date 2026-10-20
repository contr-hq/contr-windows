using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Contr.SharedKernel;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>
/// Backs the shell chrome: the selected pane item, back navigation, and the status bar with its
/// engine menu.
/// </summary>
public sealed partial class ShellViewModel : ViewModel
{
    private readonly INavigationService _navigationService;
    private readonly IDaemonConnection _daemon;
    private readonly INotificationService _notifications;
    private readonly IDisposable _navigationSubscription;

    [ObservableProperty]
    public partial object? Selected { get; set; }

    [ObservableProperty]
    public partial bool CanGoBack { get; set; }

    [ObservableProperty]
    public partial StatusSummary EngineStatus { get; set; }

    [ObservableProperty]
    public partial string DaemonText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartEngineCommand))]
    public partial bool CanStartEngine { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopEngineCommand), nameof(RestartEngineCommand))]
    public partial bool CanStopEngine { get; set; }

    public ShellViewModel(
        INavigationService navigationService, IDaemonConnection daemon, INotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(notifications);
        _navigationService = navigationService;
        _daemon = daemon;
        _notifications = notifications;
        EngineStatus = StatusSummary.From(daemon.Status);
        DaemonText = string.Empty;
        _navigationSubscription = navigationService.OnNavigated.Subscribe(OnNavigated);
        ApplyStatus(daemon.Status);
    }

    /// <summary>Shows a status; call it on the UI thread.</summary>
    public void ApplyStatus(DaemonStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var connected = status.Connection == DaemonConnectionState.Connected;
        EngineStatus = StatusSummary.From(status);
        CanStartEngine = connected
            && status.Engine is EngineState.Stopped or EngineState.Uninitialized or EngineState.Failed;
        CanStopEngine = connected && status.Engine == EngineState.Running;
        DaemonText = _daemon.Identity is { } daemon
            ? $"Daemon {daemon.Version} · {daemon.Backend} backend"
            : string.Empty;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _navigationSubscription.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartEngine))]
    private Task StartEngineAsync()
    {
        return RunAsync(_daemon.StartEngineAsync, "The engine did not start");
    }

    [RelayCommand(CanExecute = nameof(CanStopEngine))]
    private Task StopEngineAsync()
    {
        return RunAsync(_daemon.StopEngineAsync, "The engine did not stop");
    }

    [RelayCommand(CanExecute = nameof(CanStopEngine))]
    private Task RestartEngineAsync()
    {
        return RunAsync(_daemon.RestartEngineAsync, "The engine did not restart");
    }

    private async Task RunAsync(Func<CancellationToken, Task<Result>> operation, string failure)
    {
        var result = await operation(CancellationToken.None);

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, failure);
        }
    }

    private void OnNavigated(NavigationEvent e)
    {
        CanGoBack = _navigationService.CanGoBack;
    }
}
