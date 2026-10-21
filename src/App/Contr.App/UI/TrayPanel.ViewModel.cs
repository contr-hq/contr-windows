using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.UI.Common.Controls;
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
        RunningTitle = "Running";
        DockerContext = string.Empty;
        Apply(daemon.Status);
    }

    public ObservableCollection<RunningContainer> Running { get; } = [];

    public bool HasContext => DockerContext.Length > 0;

    public bool HasContainers => _containers is not null;

    /// <summary>Follows the daemon and the containers while the flyout is open.</summary>
    public void Start()
    {
        _statusSubscription ??= _daemon.StatusChanges.Subscribe(status => OnUiThread(() => Apply(status)));

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

        RunningTitle = $"Running · {running.Count.ToString(CultureInfo.CurrentCulture)}";
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
