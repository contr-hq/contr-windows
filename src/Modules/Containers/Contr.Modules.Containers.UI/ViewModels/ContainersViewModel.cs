using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
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
using Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Engine;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>The containers list: grouped by Compose project or flat, filtered, with lifecycle actions.</summary>
public sealed partial class ContainersViewModel : LiveListViewModel<ContainerDto, ContainerRow>
{
    /// <summary>The page key of the container page, derived from its type name.</summary>
    private const string _detailPageKey = "ContainerDetail";

    private const string _composePageKey = "Compose";

    private const int _flatView = 1;

    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly INavigationService _navigation;
    private readonly IClipboardService _clipboard;
    private readonly UsageSampler _usage;
    private readonly Dictionary<string, ContainerGroup> _groups = new(StringComparer.Ordinal);
    private ContainerRow? _exited;

    [ObservableProperty]
    public partial string FilterText { get; set; }

    /// <summary>0 groups the list by Compose project; 1 shows it flat.</summary>
    [ObservableProperty]
    public partial int ViewIndex { get; set; }

    [ObservableProperty]
    public partial bool ShowStopped { get; set; }

    /// <summary>Such as <c>5 running</c>, <c>2 stopped</c>; counted over every container, never the filtered.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PillItem> SummaryPills { get; set; }

    /// <summary>Whether the filter and the stopped toggle let any container through.</summary>
    [ObservableProperty]
    public partial bool HasMatches { get; set; }

    [ObservableProperty]
    public partial bool CanPrune { get; set; }

    [ObservableProperty]
    public partial bool IsExitNoticeOpen { get; set; }

    [ObservableProperty]
    public partial string ExitNotice { get; set; }

    public ContainersViewModel(
        ISender sender,
        IDialogService dialogs,
        INotificationService notifications,
        INavigationService navigation,
        IClipboardService clipboard,
        IDaemonConnection daemon)
        : this(sender, dialogs, notifications, navigation, clipboard, daemon, new UsageSampler(sender))
    {
    }

    private ContainersViewModel(
        ISender sender,
        IDialogService dialogs,
        INotificationService notifications,
        INavigationService navigation,
        IClipboardService clipboard,
        IDaemonConnection daemon,
        UsageSampler usage)
        : base(
            new LiveCollection<ContainerDto, ContainerRow>(
                container => ContainerRow.From(container, usage.For(container.Id)),
                row => row.Id,
                ContainerRow.Compare),
            _retryDelay)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(clipboard);
        _sender = sender;
        _dialogs = dialogs;
        _notifications = notifications;
        _navigation = navigation;
        _clipboard = clipboard;
        _usage = usage;
        Engine = new EngineGateViewModel(daemon);
        SummaryPills = [];
        FilterText = string.Empty;
        ShowStopped = true;
        ExitNotice = string.Empty;
        Rows.CollectionChanged += OnRowsChanged;
    }

    public EngineGateViewModel Engine { get; }

    public ObservableCollection<ContainerGroup> Groups { get; } = [];

    /// <summary>Starts following the engine and the list; call it from the page's <c>Loaded</c>.</summary>
    public void Activate()
    {
        Engine.Start();
        StartWatching();
        Regroup();
    }

    /// <summary>Stops every stream the page holds; call it from the page's <c>Unloaded</c>.</summary>
    public void Deactivate()
    {
        StopWatching();
        _usage.StopAll();
        Engine.Stop();
    }

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

    public void OpenDetails(ContainerRow row, ContainerDetailTab tab)
    {
        ArgumentNullException.ThrowIfNull(row);

        _navigation.NavigateTo(_detailPageKey, row.ContainerId, tab);
    }

    public void CopyId(ContainerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        _clipboard.SetText(row.Id);
    }

    public Task StartAllAsync(ContainerGroup group)
    {
        return RunEachAsync(group, row => row.CanStart, StartAsync);
    }

    public Task StopAllAsync(ContainerGroup group)
    {
        return RunEachAsync(group, row => row.CanStop, StopAsync);
    }

    public Task RestartAllAsync(ContainerGroup group)
    {
        return RunEachAsync(group, row => row.CanRestart, RestartAsync);
    }

    /// <summary>Deletes every stopped container after asking once.</summary>
    public void OpenCompose()
    {
        _navigation.NavigateTo(_composePageKey);
    }

    public async Task PruneStoppedAsync()
    {
        var stopped = Rows
            .Where(row => row.IsStopped)
            .ToList();

        if (stopped.Count == 0)
        {
            return;
        }

        var message = stopped.Count == 1
            ? $"Delete {stopped[0].Name}? Its logs and file changes are deleted with it."
            : $"Delete {stopped.Count} stopped containers? Their logs and file changes are deleted with them.";

        if (!await _dialogs.ShowConfirmAsync("Prune stopped containers", message, "Delete", "Cancel"))
        {
            return;
        }

        foreach (var row in stopped)
        {
            await RunAsync(new RemoveContainerCommand(row.ContainerId, row.State), $"Could not delete {row.Name}");
        }
    }

    public void ViewExitedLogs()
    {
        if (_exited is { } row)
        {
            OpenDetails(row, ContainerDetailTab.Logs);
        }
    }

    public async Task RestartExitedAsync()
    {
        if (_exited is { } row)
        {
            IsExitNoticeOpen = false;
            await StartAsync(row);
        }
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
            Rows.CollectionChanged -= OnRowsChanged;
            Deactivate();
            _usage.Dispose();
        }

        base.Dispose(disposing);
    }

    partial void OnFilterTextChanged(string value)
    {
        Regroup();
    }

    partial void OnViewIndexChanged(int value)
    {
        Regroup();
    }

    partial void OnShowStoppedChanged(bool value)
    {
        Regroup();
    }

    private static List<PillItem> Summarize(List<ContainerRow> rows)
    {
        var running = rows.Count(row => row.IsRunning);
        var failed = rows.Count(row => row.HasFailed);
        var stopped = rows.Count(row => row.IsStopped && !row.HasFailed);
        var projects = rows
            .Where(row => row.HasProject)
            .Select(row => row.Project)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var pills = new List<PillItem> { new($"{Count(running)} running", PillKind.Good) };

        if (stopped > 0)
        {
            pills.Add(new PillItem($"{Count(stopped)} stopped", PillKind.Hollow));
        }

        if (failed > 0)
        {
            pills.Add(new PillItem($"{Count(failed)} failed", PillKind.Critical));
        }

        if (projects > 0)
        {
            pills.Add(new PillItem(
                projects == 1 ? "1 compose project" : $"{Count(projects)} compose projects", PillKind.Plain));
        }

        return pills;
    }

    private static string Count(int count)
    {
        return count.ToString(CultureInfo.CurrentCulture);
    }

    private async Task RunEachAsync(
        ContainerGroup group, Func<ContainerRow, bool> applies, Func<ContainerRow, Task> run)
    {
        ArgumentNullException.ThrowIfNull(group);

        var rows = RowsOf(group.Key)
            .Where(applies)
            .ToList();

        foreach (var row in rows)
        {
            await run(row);
        }
    }

    private IEnumerable<ContainerRow> RowsOf(string groupKey)
    {
        return groupKey == ContainerGroup.FlatKey ? Rows : Rows.Where(row => row.Project == groupKey);
    }

    private async Task RunAsync(IRequest<Result> command, string failure)
    {
        var result = await _sender.Send(command);

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, failure);
        }
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e is {
            Action: NotifyCollectionChangedAction.Replace,
            OldItems: [ContainerRow before],
            NewItems: [ContainerRow after],
        })
        {
            NoticeExit(before, after);
        }

        Regroup();
    }

    /// <summary>Raises the exit notice when a running container fails; lowers it when that one runs again.</summary>
    private void NoticeExit(ContainerRow before, ContainerRow after)
    {
        if (before.IsRunning && after.HasFailed)
        {
            _exited = after;
            var where = after.HasProject ? $"{after.Name} in {after.Project}" : after.Name;
            var what = after.ExitCode is { } exitCode ? $"exited with code {exitCode}" : "died";
            ExitNotice = $"{where} {what} at {DateTimeOffset.Now.ToString("t", CultureInfo.CurrentCulture)}.";
            IsExitNoticeOpen = true;

            return;
        }

        if (_exited?.Id == after.Id && after.IsRunning)
        {
            IsExitNoticeOpen = false;
        }
    }

    private void Regroup()
    {
        var filter = FilterText.Trim();
        var all = Rows.ToList();
        var visible = all
            .Where(row => (ShowStopped || !row.IsStopped) && row.Matches(filter))
            .ToList();

        var desired = ViewIndex == _flatView ? FlatGroups(visible, all) : ProjectGroups(visible, all);
        CollectionSync.Sync(Groups, desired, group => group.Key);

        var running = all
            .Where(row => row.State == ContainerState.Running)
            .Select(row => row.ContainerId)
            .ToList();
        _usage.Follow(running);

        var pills = Summarize(all);

        if (!pills.SequenceEqual(SummaryPills))
        {
            SummaryPills = pills;
        }

        HasMatches = visible.Count > 0;
        CanPrune = all.Any(row => row.IsStopped);
    }

    private List<ContainerGroup> FlatGroups(List<ContainerRow> visible, List<ContainerRow> all)
    {
        if (visible.Count == 0)
        {
            return [];
        }

        var group = GroupFor(ContainerGroup.FlatKey);
        group.Update(visible, all);

        return [group];
    }

    /// <summary>Projects by name, then the standalone containers; a card the filter empties is left out.</summary>
    private List<ContainerGroup> ProjectGroups(List<ContainerRow> visible, List<ContainerRow> all)
    {
        var keys = visible
            .Select(row => row.Project)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key.Length == 0)
            .ThenBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = new List<ContainerGroup>(keys.Count);
        foreach (var key in keys)
        {
            var group = GroupFor(key);
            group.Update(
                [.. visible.Where(row => row.Project == key)],
                [.. all.Where(row => row.Project == key)]);
            groups.Add(group);
        }

        return groups;
    }

    private ContainerGroup GroupFor(string key)
    {
        if (!_groups.TryGetValue(key, out var group))
        {
            group = new ContainerGroup(key);
            _groups[key] = group;
        }

        return group;
    }
}
