using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Commands.KillContainer;
using Contr.Modules.Containers.Application.Containers.Commands.PauseContainer;
using Contr.Modules.Containers.Application.Containers.Commands.RemoveContainer;
using Contr.Modules.Containers.Application.Containers.Commands.RenameContainer;
using Contr.Modules.Containers.Application.Containers.Commands.RestartContainer;
using Contr.Modules.Containers.Application.Containers.Commands.StartContainer;
using Contr.Modules.Containers.Application.Containers.Commands.StopContainer;
using Contr.Modules.Containers.Application.Containers.Commands.UnpauseContainer;
using Contr.Modules.Containers.Application.Containers.Queries.InspectContainer;
using Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Formatting;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>One container's page: its state and actions, and the logs, inspect and stats tabs.</summary>
/// <remarks>Use it on the UI thread: the streams resume on the captured context.</remarks>
public sealed partial class ContainerDetailViewModel : ViewModel
{
    private const string _listPageKey = "Containers";

    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly INavigationService _navigation;
    private readonly IClipboardService _clipboard;
    private readonly ContainerUsage _noUsage = new();
    private ContainerId? _id;
    private ContainerDetailsDto? _details;
    private CancellationTokenSource? _watch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Trail), nameof(StatusKind), nameof(StatusLine), nameof(Image))]
    [NotifyPropertyChangedFor(nameof(CanStart), nameof(CanStop), nameof(CanRestart), nameof(CanRemove))]
    [NotifyPropertyChangedFor(nameof(ShortId), nameof(FirstPort), nameof(HasPort), nameof(Ports))]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(PillKind), nameof(StatusText), nameof(Elapsed))]
    [NotifyPropertyChangedFor(nameof(CanPause), nameof(CanUnpause), nameof(CanKill), nameof(CanRename))]
    public partial ContainerRow? Row { get; set; }

    [ObservableProperty]
    public partial ContainerDetailTab Tab { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial bool IsGone { get; set; }

    public ContainerDetailViewModel(
        ISender sender,
        IDialogService dialogs,
        INotificationService notifications,
        INavigationService navigation,
        IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(navigation);
        _sender = sender;
        _dialogs = dialogs;
        _notifications = notifications;
        _navigation = navigation;
        _clipboard = clipboard;
        Logs = new ContainerLogsViewModel(sender, clipboard);
        Terminal = new ContainerTerminalViewModel(sender);
        Inspect = new ContainerInspectViewModel(clipboard);
        Stats = new ContainerStatsViewModel(sender);
    }

    public ContainerLogsViewModel Logs { get; }

    public ContainerInspectViewModel Inspect { get; }

    public ContainerStatsViewModel Stats { get; }

    public ContainerTerminalViewModel Terminal { get; }

    public string ShortId => Row?.ShortId ?? string.Empty;

    public bool IsRunning => !IsGone && Row?.State == ContainerState.Running;

    public IReadOnlyList<PortLink> Ports => Row?.Ports ?? [];

    /// <summary>The first published port with an http address, for the header's open button.</summary>
    public PortLink? FirstPort => Row?.Ports.FirstOrDefault(port => port.IsLink);

    public bool HasPort => FirstPort is not null;

    /// <summary>Containers, then the Compose project when there is one, then the container.</summary>
    public IReadOnlyList<string> Trail => Row is not { } row
        ? ["Containers"]
        : row.HasProject ? ["Containers", row.Project, row.Name] : ["Containers", row.Name];

    public StatusKind StatusKind => Row?.StatusKind ?? StatusKind.Neutral;

    /// <summary>Such as <c>Exited with code 1, 14 minutes ago</c> or <c>Running for 2 h · healthy</c>.</summary>
    public string StatusLine => Row switch {
        null => string.Empty,
        { State: ContainerState.Exited, ExitCode: { } code } => $"Exited with code {code}{Since(_details?.FinishedAt)}",
        { State: ContainerState.Running } row => RunningLine(row),
        { } row => row.StatusText,
    };

    public string Image => Row?.Image ?? string.Empty;

    public PillKind PillKind => Row?.PillKind ?? PillKind.Hollow;

    public string StatusText => Row?.StatusText ?? string.Empty;

    /// <summary>Such as <c>up 2h</c>.</summary>
    public string Elapsed => Row?.Elapsed ?? string.Empty;

    public bool CanStart => !IsGone && Row?.CanStart == true;

    public bool CanStop => !IsGone && Row?.CanStop == true;

    public bool CanRestart => !IsGone && Row?.CanRestart == true;

    public bool CanRemove => !IsGone && Row?.CanRemove == true;

    public bool CanPause => !IsGone && Row?.CanPause == true;

    public bool CanUnpause => !IsGone && Row?.CanUnpause == true;

    public bool CanKill => !IsGone && Row?.CanKill == true;

    public bool CanRename => !IsGone && Row is not null;

    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>Takes the container's ID and, optionally, the tab to open.</summary>
    public override void OnNavigatedTo(params ReadOnlySpan<object> args)
    {
        foreach (var arg in args)
        {
            switch (arg)
            {
                case ContainerId id:
                    _id = id;
                    break;
                case ContainerDetailTab tab:
                    Tab = tab;
                    break;
            }
        }
    }

    /// <summary>Starts every stream the page shows; call it from the page's <c>Loaded</c>.</summary>
    public void Activate()
    {
        if (_id is not { } id || _watch is not null)
        {
            return;
        }

        _watch = new CancellationTokenSource();
        _ = WatchAsync(id, _watch.Token);
        _ = LoadAsync(id, openLogs: true);
    }

    /// <summary>Stops every stream and ends the terminal session; call it from the page's <c>Unloaded</c>.</summary>
    public void Deactivate()
    {
        _watch?.Cancel();
        _watch?.Dispose();
        _watch = null;
        Logs.Close();
        Stats.Stop();
        _ = Terminal.DisconnectAsync();
    }

    /// <summary>Starts a shell in the running container unless one is connected.</summary>
    public Task ConnectTerminalAsync()
    {
        return IsRunning && !Terminal.IsConnected && Row is { } row
            ? Terminal.ConnectAsync(row.ContainerId)
            : Task.CompletedTask;
    }

    public void CopyText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > 0)
        {
            _clipboard.SetText(text);
        }
    }

    public void CopyId()
    {
        if (Row is { } row)
        {
            _clipboard.SetText(row.Id);
        }
    }

    public void OpenList()
    {
        _navigation.NavigateTo(_listPageKey);
    }

    public Task StartAsync()
    {
        return Row is { } row
            ? RunAsync(new StartContainerCommand(row.ContainerId, row.State), $"Could not start {row.Name}")
            : Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Row is { } row
            ? RunAsync(new StopContainerCommand(row.ContainerId, row.State), $"Could not stop {row.Name}")
            : Task.CompletedTask;
    }

    public Task RestartAsync()
    {
        return Row is { } row
            ? RunAsync(new RestartContainerCommand(row.ContainerId, row.State), $"Could not restart {row.Name}")
            : Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        return Row is { } row
            ? RunAsync(new PauseContainerCommand(row.ContainerId, row.State), $"Could not pause {row.Name}")
            : Task.CompletedTask;
    }

    public Task UnpauseAsync()
    {
        return Row is { } row
            ? RunAsync(new UnpauseContainerCommand(row.ContainerId, row.State), $"Could not resume {row.Name}")
            : Task.CompletedTask;
    }

    public Task KillAsync()
    {
        return Row is { } row
            ? RunAsync(new KillContainerCommand(row.ContainerId, row.State), $"Could not kill {row.Name}")
            : Task.CompletedTask;
    }

    /// <summary>Asks for the new name; the dialog stays open while the engine refuses it.</summary>
    public Task RenameAsync()
    {
        if (Row is not { } row)
        {
            return Task.CompletedTask;
        }

        return _dialogs.ShowPromptAsync("Rename container", "Name", row.Name, "Rename", async name => {
            var result = await _sender.Send(new RenameContainerCommand(row.ContainerId, name));

            return result.IsSuccess ? null : result.Error.Message;
        });
    }

    /// <summary>Asks first, then returns to the list once the container is gone.</summary>
    public async Task RemoveAsync()
    {
        if (Row is not { } row)
        {
            return;
        }

        var message = row.State.RemovalNeedsForce()
            ? $"{row.Name} is running. Stop it and delete it?"
            : $"Delete {row.Name}?";

        if (!await _dialogs.ShowConfirmAsync("Delete container", message, "Delete", "Cancel"))
        {
            return;
        }

        if (await RunAsync(new RemoveContainerCommand(row.ContainerId, row.State), $"Could not delete {row.Name}"))
        {
            OpenList();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Deactivate();
            Logs.Dispose();
            Stats.Dispose();
        }
    }

    partial void OnTabChanged(ContainerDetailTab value)
    {
        FollowStats();
    }

    partial void OnRowChanged(ContainerRow? oldValue, ContainerRow? newValue)
    {
        if (oldValue is null || newValue is null || oldValue.State == newValue.State || _id is not { } id)
        {
            return;
        }

        if (oldValue.IsRunning && !newValue.IsRunning)
        {
            var exit = newValue.ExitCode is { } code ? $"container exited with code {code}" : "container stopped";
            Logs.Note(exit);
            _ = Terminal.DisconnectAsync();
        }

        _ = LoadAsync(id, openLogs: !oldValue.IsRunning && newValue.IsRunning);
        FollowStats();
    }

    private static string RunningLine(ContainerRow row)
    {
        var elapsed = ContainerStatusText.Elapsed(row.Status);
        var running = elapsed.Length > 0 ? $"Running for {elapsed}" : "Running";

        return ContainerStatusText.Health(row.Status) switch {
            ContainerHealth.Healthy => $"{running} · healthy",
            ContainerHealth.Unhealthy => $"{running} · unhealthy",
            ContainerHealth.Starting => $"{running} · health check starting",
            _ => running,
        };
    }

    private static string Since(DateTimeOffset? finishedAt)
    {
        if (finishedAt is not { } finished)
        {
            return string.Empty;
        }

        var age = HumanFormat.Age(finished, DateTimeOffset.Now);

        return $", {char.ToLowerInvariant(age[0])}{age[1..]}";
    }

    private void FollowStats()
    {
        var showsUsage = Tab is ContainerDetailTab.Stats or ContainerDetailTab.Exec;

        if (showsUsage && Row is { State: ContainerState.Running } row && _watch is not null)
        {
            Stats.Start(row.ContainerId);
        }
        else
        {
            Stats.Stop();
        }
    }

    private async Task LoadAsync(ContainerId id, bool openLogs)
    {
        var result = await _sender.Send(new InspectContainerQuery(id));

        if (result.IsFailure)
        {
            Inspect.Error = result.Error.Message;

            if (openLogs)
            {
                Logs.Open(id, null);
            }

            return;
        }

        _details = result.Value;
        Inspect.Show(_details, DateTimeOffset.Now);
        OnPropertyChanged(nameof(StatusLine));

        if (openLogs)
        {
            Logs.Open(id, _details.StartedAt);
        }
    }

    private async Task WatchAsync(ContainerId id, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var changes = await _sender.Send(new WatchContainersQuery(), cancellationToken);
                await foreach (var change in changes.WithCancellation(cancellationToken))
                {
                    Apply(id, change);
                }

                await Task.Delay(_retryDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private void Apply(ContainerId id, ListChange<ContainerDto> change)
    {
        switch (change)
        {
            case ListChange<ContainerDto>.Snapshot snapshot:
                Error = null;
                Show(snapshot.Items.FirstOrDefault(container => container.Id == id));
                break;
            case ListChange<ContainerDto>.Upserted upserted when upserted.Item.Id == id:
                Show(upserted.Item);
                break;
            case ListChange<ContainerDto>.Removed removed when removed.Key == id.Value:
                Show(null);
                break;
            case ListChange<ContainerDto>.Failed failed:
                Error = failed.Error.Message;
                break;
        }
    }

    private void Show(ContainerDto? container)
    {
        IsGone = container is null;

        if (container is not null)
        {
            Row = ContainerRow.From(container, _noUsage);
        }

        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRestart));
        OnPropertyChanged(nameof(CanRemove));

        FollowStats();
    }

    private async Task<bool> RunAsync(IRequest<Result> command, string failure)
    {
        var result = await _sender.Send(command);

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, failure);
        }

        return result.IsSuccess;
    }
}
