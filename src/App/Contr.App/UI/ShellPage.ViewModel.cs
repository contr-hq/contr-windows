using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Contr.SharedKernel;
using Contr.UI.Common.Formatting;
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

    /// <summary>The engine backend, such as <c>WSL 2</c>; empty until the daemon answered.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackend))]
    public partial string BackendText { get; set; }

    [ObservableProperty]
    public partial string DaemonVersionText { get; set; }

    /// <summary>Such as <c>Stopped at 18:02</c>; empty unless the engine is stopped.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSince))]
    public partial string SinceText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEngineVersion))]
    public partial string EngineVersionText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContext))]
    public partial string ContextText { get; set; }

    /// <summary>Such as <c>CPU 9%</c>; empty while the engine reports no usage.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUsage))]
    public partial string CpuText { get; set; }

    /// <summary>Such as <c>Memory 2.4 of 8 GB</c>.</summary>
    [ObservableProperty]
    public partial string MemoryText { get; set; }

    /// <summary>Such as <c>Disk 18.6 GB</c>; empty until the engine measured it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDisk))]
    public partial string DiskText { get; set; }

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
        BackendText = string.Empty;
        DaemonVersionText = string.Empty;
        SinceText = string.Empty;
        EngineVersionText = string.Empty;
        ContextText = string.Empty;
        CpuText = string.Empty;
        MemoryText = string.Empty;
        DiskText = string.Empty;
        _navigationSubscription = navigationService.OnNavigated.Subscribe(OnNavigated);
        ApplyStatus(daemon.Status);
    }

    public bool HasBackend => BackendText.Length > 0;

    public bool HasSince => SinceText.Length > 0;

    public bool HasEngineVersion => EngineVersionText.Length > 0;

    public bool HasContext => ContextText.Length > 0;

    public bool HasUsage => CpuText.Length > 0;

    public bool HasDisk => DiskText.Length > 0;

    /// <summary>Shows the engine's usage, or hides it when absent; call it on the UI thread.</summary>
    public void ApplyUsage(EngineUsage? usage)
    {
        CpuText = usage is null ? string.Empty : $"CPU {usage.CpuShare.ToString("0", CultureInfo.CurrentCulture)}%";
        MemoryText = usage is null
            ? string.Empty
            : $"Memory {HumanFormat.MemoryOf(usage.MemoryUsedBytes, usage.MemoryTotalBytes)}";
        DiskText = usage is { DiskUsedBytes: > 0 } ? $"Disk {HumanFormat.Bytes(usage.DiskUsedBytes)}" : string.Empty;
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
        var daemon = connected ? _daemon.Identity : null;
        BackendText = daemon is null ? string.Empty : $"{daemon.Backend} backend";
        DaemonVersionText = daemon is null ? string.Empty : $"Daemon {daemon.Version}";
        SinceText = status is { Engine: EngineState.Stopped, Since: { } since }
            ? $"Stopped at {ShortTime(since)}"
            : string.Empty;
        EngineVersionText = status.Details is { } engine ? $"Engine {engine.Version}" : string.Empty;
        ContextText = status.Details is { DockerContext.Length: > 0 } context
            ? $"Context {context.DockerContext}"
            : string.Empty;
    }

    /// <summary>Such as <c>18:02</c>, in local time.</summary>
    public static string ShortTime(DateTimeOffset moment)
    {
        var local = moment.ToLocalTime();

        return local.ToString("t", CultureInfo.CurrentCulture);
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
