using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Formatting;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Containers;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.UI.Dispatching;

namespace Contr.App.UI;

/// <summary>Backs the tray flyout: the engine's state, the running containers and the engine's actions.</summary>
public sealed partial class TrayViewModel : ViewModel
{
    private readonly IDaemonConnection _daemon;
    private readonly IRunningContainers? _containers;
    private readonly DispatcherQueue? _dispatcher;
    private IDisposable? _statusSubscription;
    private IDisposable? _containersSubscription;
    private IDisposable? _usageSubscription;

    [ObservableProperty]
    public partial StatusKind StatusKind { get; set; }

    /// <summary>Such as <c>Engine running · WSL 2</c>.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; }

    /// <summary>Such as <c>Running · 5</c>.</summary>
    [ObservableProperty]
    public partial string RunningTitle { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContext))]
    public partial string DockerContext { get; set; }

    /// <summary>Such as <c>9%</c>; empty while the engine reports no usage.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUsage))]
    public partial string CpuText { get; set; }

    /// <summary>Such as <c>2.4 / 8 GB</c>.</summary>
    [ObservableProperty]
    public partial string MemoryText { get; set; }

    /// <summary>Such as <c>18.6 GB</c>, or a dash until the engine measured it.</summary>
    [ObservableProperty]
    public partial string DiskText { get; set; }

    [ObservableProperty]
    public partial bool CanStartEngine { get; set; }

    [ObservableProperty]
    public partial bool CanRestartEngine { get; set; }

    /// <param name="containers">The Containers module's list; absent when no module offers one.</param>
    public TrayViewModel(IDaemonConnection daemon, IRunningContainers? containers = null)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
        _containers = containers;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        StatusText = string.Empty;
        RunningTitle = "RUNNING";
        DockerContext = string.Empty;
        CpuText = string.Empty;
        MemoryText = string.Empty;
        DiskText = string.Empty;
        Apply(daemon.Status);
    }

    public ObservableCollection<RunningContainer> Running { get; } = [];

    public bool HasContext => DockerContext.Length > 0;

    public bool HasContainers => _containers is not null;

    public bool HasUsage => CpuText.Length > 0;

    /// <summary>Follows the daemon and the containers while the flyout is open.</summary>
    public void Start()
    {
        _statusSubscription ??= _daemon.StatusChanges.Subscribe(status => OnUiThread(() => Apply(status)));
        _usageSubscription ??= _daemon.UsageChanges.Subscribe(usage => OnUiThread(() => ShowUsage(usage)));

        if (_containers is not null && _containersSubscription is null)
        {
            _containersSubscription = _containers
                .Watch()
                .Subscribe(running => OnUiThread(() => Show(running)));
        }
    }

    public void Stop()
    {
        _statusSubscription?.Dispose();
        _statusSubscription = null;
        _containersSubscription?.Dispose();
        _containersSubscription = null;
        _usageSubscription?.Dispose();
        _usageSubscription = null;
    }

    /// <summary>Shows the engine's usage in the tiles, or hides them when absent; call it on the UI thread.</summary>
    public void ShowUsage(EngineUsage? usage)
    {
        CpuText = usage is null ? string.Empty : $"{usage.CpuShare.ToString("0", CultureInfo.CurrentCulture)}%";
        MemoryText = usage is null
            ? string.Empty
            : HumanFormat
                .MemoryOf(usage.MemoryUsedBytes, usage.MemoryTotalBytes)
                .Replace(" of ", " / ", StringComparison.Ordinal);
        DiskText = usage is { DiskUsedBytes: > 0 } ? HumanFormat.Bytes(usage.DiskUsedBytes) : "—";
    }

    public Task StopContainerAsync(RunningContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        return _containers?.StopAsync(container.Id) ?? Task.CompletedTask;
    }

    public async Task StartEngineAsync()
    {
        await _daemon.StartEngineAsync(CancellationToken.None);
    }

    public async Task RestartEngineAsync()
    {
        await _daemon.RestartEngineAsync(CancellationToken.None);
    }

    /// <summary>Shows a status; call it on the UI thread.</summary>
    public void Apply(DaemonStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var summary = StatusSummary.From(status);
        StatusKind = summary.Kind;
        StatusText = _daemon.Identity is { } identity && status.Connection == DaemonConnectionState.Connected
            ? $"{summary.Text} · {identity.Backend}"
            : summary.Text;
        DockerContext = status.Details?.DockerContext ?? string.Empty;

        var connected = status.Connection == DaemonConnectionState.Connected;
        CanStartEngine = connected
            && status.Engine is EngineState.Stopped or EngineState.Uninitialized or EngineState.Failed;
        CanRestartEngine = connected && status.Engine == EngineState.Running;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
        }
    }

    private void Show(IReadOnlyList<RunningContainer> running)
    {
        Running.Clear();
        foreach (var container in running)
        {
            Running.Add(container);
        }

        RunningTitle = $"RUNNING · {running.Count.ToString(CultureInfo.CurrentCulture)}";
    }

    private void OnUiThread(Action action)
    {
        if (_dispatcher is null)
        {
            action();

            return;
        }

        _dispatcher.TryEnqueue(() => action());
    }
}
