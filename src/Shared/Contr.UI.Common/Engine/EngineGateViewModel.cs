using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Contr.UI.Common.Icons;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.UI.Dispatching;

namespace Contr.UI.Common.Engine;

/// <summary>What an engine-backed page shows in place of its content.</summary>
public enum EngineGateState
{
    /// <summary>The engine runs; the page shows its content.</summary>
    Open,
    Stopped,

    /// <summary>The engine is starting, stopping, updating or resetting.</summary>
    Busy,
    Failed,

    /// <summary>The daemon is not reachable.</summary>
    Disconnected,
}

/// <summary>Follows the engine state for a page over engine objects and offers to start a stopped engine.</summary>
public sealed partial class EngineGateViewModel : ObservableObject
{
    private readonly IDaemonConnection _daemon;
    private readonly DispatcherQueue? _dispatcher;
    private IDisposable? _subscription;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(IsClosed), nameof(IsStopped), nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(IsFailed), nameof(IsDisconnected), nameof(Glyph))]
    [NotifyCanExecuteChangedFor(nameof(StartEngineCommand))]
    public partial EngineGateState State { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartEngineCommand))]
    public partial bool IsStarting { get; set; }

    public EngineGateViewModel(IDaemonConnection daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Title = string.Empty;
        Message = string.Empty;
        Apply(daemon.Status);
    }

    public bool IsOpen => State == EngineGateState.Open;

    public bool IsClosed => State != EngineGateState.Open;

    public bool IsStopped => State == EngineGateState.Stopped;

    public bool IsBusy => State == EngineGateState.Busy;

    public bool IsFailed => State == EngineGateState.Failed;

    public bool IsDisconnected => State == EngineGateState.Disconnected;

    /// <summary>A <see cref="ContrIcons"/> glyph for the state.</summary>
    public string Glyph => State switch {
        EngineGateState.Failed => ContrIcons.Error,
        EngineGateState.Disconnected => ContrIcons.Warning,
        _ => ContrIcons.Stop,
    };

    /// <summary>Whether the client starts the engine whenever it connects; the choice persists.</summary>
    public bool StartsEngineOnConnect
    {
        get => _daemon.StartsEngineOnConnect;
        set {
            if (_daemon.StartsEngineOnConnect != value)
            {
                _daemon.StartsEngineOnConnect = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Follows the engine; call it when the page loads.</summary>
    public void Start()
    {
        _subscription ??= _daemon.StatusChanges.Subscribe(OnStatus);
    }

    /// <summary>Stops following the engine; call it when the page unloads.</summary>
    public void Stop()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    /// <summary>Shows a status; call it on the UI thread.</summary>
    public void Apply(DaemonStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        (State, Title, Message) = Describe(status);
    }

    private static (EngineGateState State, string Title, string Message) Describe(DaemonStatus status)
    {
        if (status.Connection != DaemonConnectionState.Connected)
        {
            return (EngineGateState.Disconnected, "Contr is not connected", status.Problem.Length > 0
                ? status.Problem
                : "Contr is reaching its background service.");
        }

        return status.Engine switch {
            EngineState.Running => (EngineGateState.Open, string.Empty, string.Empty),
            EngineState.Stopped or EngineState.Uninitialized => (
                EngineGateState.Stopped,
                "The engine is stopped",
                "Your containers, images and volumes are safe. Start the engine to see and use them again."),
            EngineState.Failed => (EngineGateState.Failed, "The engine failed", status.Problem),
            EngineState.Stopping => (EngineGateState.Busy, "The engine is stopping", string.Empty),
            EngineState.Updating => (EngineGateState.Busy, "The engine is updating", string.Empty),
            EngineState.Resetting => (EngineGateState.Busy, "The engine is resetting", string.Empty),
            _ => (EngineGateState.Busy, "The engine is starting", string.Empty),
        };
    }

    private bool CanStartEngine()
    {
        return !IsStarting && State is EngineGateState.Stopped or EngineGateState.Failed;
    }

    [RelayCommand(CanExecute = nameof(CanStartEngine))]
    private async Task StartEngineAsync()
    {
        IsStarting = true;
        try
        {
            var result = await _daemon.StartEngineAsync(CancellationToken.None);

            if (result.IsFailure)
            {
                (State, Title, Message) = (EngineGateState.Failed, "The engine did not start", result.Error.Message);
            }
        }
        finally
        {
            IsStarting = false;
        }
    }

    private void OnStatus(DaemonStatus status)
    {
        // StatusChanges emits on a background thread.
        if (_dispatcher is null)
        {
            Apply(status);

            return;
        }

        _dispatcher.TryEnqueue(() => Apply(status));
    }
}
