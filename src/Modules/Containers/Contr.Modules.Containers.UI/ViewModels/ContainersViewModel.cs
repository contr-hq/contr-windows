using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Commands.RemoveContainer;
using Contr.Modules.Containers.Application.Containers.Commands.RestartContainer;
using Contr.Modules.Containers.Application.Containers.Commands.StartContainer;
using Contr.Modules.Containers.Application.Containers.Commands.StopContainer;
using Contr.Modules.Containers.Application.Containers.Queries.StreamContainerLogs;
using Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>The containers list, its lifecycle actions, and the log panel of one container.</summary>
public sealed partial class ContainersViewModel : LiveListViewModel<ContainerDto, ContainerRow>
{
    private const int _logTailLines = 500;

    /// <summary>The log panel keeps the newest characters up to this count.</summary>
    private const int _logCapacity = 200_000;

    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly StringBuilder _logText = new();
    private CancellationTokenSource? _logs;

    [ObservableProperty]
    public partial bool IsLogsOpen { get; set; }

    [ObservableProperty]
    public partial string LogsTitle { get; set; }

    [ObservableProperty]
    public partial string LogsText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogsError))]
    public partial string? LogsError { get; set; }

    public ContainersViewModel(ISender sender, IDialogService dialogs, INotificationService notifications)
        : base(
            new LiveCollection<ContainerDto, ContainerRow>(ContainerRow.From, row => row.Id, ContainerRow.Compare),
            _retryDelay)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        _sender = sender;
        _dialogs = dialogs;
        _notifications = notifications;
        LogsTitle = string.Empty;
        LogsText = string.Empty;
    }

    public bool HasLogsError => !string.IsNullOrEmpty(LogsError);

    public Task StartAsync(ContainerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return RunAsync(new StartContainerCommand(row.ContainerId, row.State), $"Could not start {row.Name}");
    }

    public Task StopAsync(ContainerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return RunAsync(new StopContainerCommand(row.ContainerId, row.State), $"Could not stop {row.Name}");
    }

    public Task RestartAsync(ContainerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return RunAsync(new RestartContainerCommand(row.ContainerId, row.State), $"Could not restart {row.Name}");
    }

    /// <summary>Asks first; a running container is killed before it is deleted.</summary>
    public async Task RemoveAsync(ContainerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var message = row.State.RemovalNeedsForce()
            ? $"{row.Name} is running. Stop it and delete it?"
            : $"Delete {row.Name}?";

        if (!await _dialogs.ShowConfirmAsync("Delete container", message, "Delete", "Cancel"))
        {
            return;
        }

        await RunAsync(new RemoveContainerCommand(row.ContainerId, row.State), $"Could not delete {row.Name}");
    }

    /// <summary>Opens the log panel on <paramref name="row"/> and follows its output.</summary>
    public void OpenLogs(ContainerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        CloseLogs();
        _logText.Clear();
        LogsText = string.Empty;
        LogsError = null;
        LogsTitle = row.Name;
        IsLogsOpen = true;

        _logs = new CancellationTokenSource();
        _ = FollowLogsAsync(row.ContainerId, _logs.Token);
    }

    public void CloseLogs()
    {
        _logs?.Cancel();
        _logs?.Dispose();
        _logs = null;
        IsLogsOpen = false;
    }

    protected override Task<IAsyncEnumerable<ListChange<ContainerDto>>> OpenAsync(
        CancellationToken cancellationToken)
    {
        return _sender.Send(new WatchContainersQuery(), cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseLogs();
        }

        base.Dispose(disposing);
    }

    private async Task RunAsync(IRequest<Result> command, string failure)
    {
        var result = await _sender.Send(command);

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, failure);
        }
    }

    private async Task FollowLogsAsync(ContainerId id, CancellationToken cancellationToken)
    {
        try
        {
            var events = await _sender.Send(new StreamContainerLogsQuery(id, _logTailLines), cancellationToken);
            await foreach (var logEvent in events.WithCancellation(cancellationToken))
            {
                switch (logEvent)
                {
                    case ContainerLogEvent.Output output:
                        AppendLog(output.Text);
                        break;
                    case ContainerLogEvent.Failed failed:
                        LogsError = failed.Error.Message;
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The panel closed or moved to another container.
        }
    }

    private void AppendLog(string text)
    {
        _logText.Append(text);

        if (_logText.Length > _logCapacity)
        {
            _logText.Remove(0, _logText.Length - _logCapacity);
        }

        LogsText = _logText.ToString();
    }
}
