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
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.Application.Volumes.Commands.CreateVolume;
using Contr.Modules.Volumes.Application.Volumes.Commands.PruneVolumes;
using Contr.Modules.Volumes.Application.Volumes.Commands.RemoveVolume;
using Contr.Modules.Volumes.Application.Volumes.Queries.GetVolumeUsage;
using Contr.Modules.Volumes.Application.Volumes.Queries.WatchVolumes;
using Contr.UI.Common.Engine;
using Contr.UI.Common.Formatting;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Volumes.UI.ViewModels;

/// <summary>The volumes list with the containers that use each one, a details panel, creation and pruning.</summary>
/// <remarks>Use it on the UI thread: the list and the usage resume on the captured context.</remarks>
public sealed partial class VolumesViewModel : LiveListViewModel<VolumeDto, VolumeRow>
{
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly IClipboardService _clipboard;
    private ILookup<string, VolumeMountDto>? _usage;
    private bool _isRefreshing;
    private bool _refreshAgain;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial VolumeItem? Selected { get; set; }

    /// <summary>Such as <c>6 volumes · 1 not used by any container</c>.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; set; }

    [ObservableProperty]
    public partial bool HasUnused { get; set; }

    public VolumesViewModel(
        ISender sender,
        IDialogService dialogs,
        INotificationService notifications,
        IClipboardService clipboard,
        IDaemonConnection daemon)
        : base(
            new LiveCollection<VolumeDto, VolumeRow>(VolumeRow.From, row => row.Name, VolumeRow.Compare),
            _retryDelay)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(clipboard);
        _sender = sender;
        _dialogs = dialogs;
        _notifications = notifications;
        _clipboard = clipboard;
        Engine = new EngineGateViewModel(daemon);
        Subtitle = string.Empty;
        Rows.CollectionChanged += OnRowsChanged;
    }

    public EngineGateViewModel Engine { get; }

    public ObservableCollection<VolumeItem> Items { get; } = [];

    public bool HasSelection => Selected is not null;

    /// <summary>Starts following the engine and the list; call it from the page's <c>Loaded</c>.</summary>
    public void Activate()
    {
        Engine.Start();
        StartWatching();
        _ = RefreshUsageAsync();
    }

    public void Deactivate()
    {
        StopWatching();
        Engine.Stop();
    }

    public void Select(VolumeItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Show(item.Name);
    }

    /// <summary>Asks for a name, then creates the volume.</summary>
    public async Task PromptAndCreateAsync()
    {
        var name = await _dialogs.ShowPromptAsync("Create volume", "Name", string.Empty, "Create");

        if (name is not null)
        {
            await CreateAsync(name);
        }
    }

    public async Task CreateAsync(string name)
    {
        var result = await _sender.Send(new CreateVolumeCommand(name));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "Could not create the volume");
        }
    }

    /// <summary>Asks first; deleting a volume deletes its data.</summary>
    public async Task RemoveAsync(VolumeItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var confirmed = await _dialogs.ShowConfirmAsync(
            "Delete volume", $"Delete {item.Name} and the data in it?", "Delete", "Cancel");

        if (!confirmed)
        {
            return;
        }

        var result = await _sender.Send(new RemoveVolumeCommand(item.Row.VolumeName));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, $"Could not delete {item.Name}");
        }
    }

    /// <summary>Deletes every volume no container uses, named ones included, after asking.</summary>
    public async Task RemoveUnusedAsync()
    {
        var confirmed = await _dialogs.ShowConfirmAsync(
            "Remove unused volumes",
            "Delete every volume no container uses, and the data in them?",
            "Delete",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        var result = await _sender.Send(new PruneVolumesCommand(All: true));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "Could not remove the volumes");

            return;
        }

        var pruned = result.Value;
        var deleted = pruned.Deleted == 1 ? "1 volume" : $"{Count(pruned.Deleted)} volumes";
        _notifications.Show(
            $"Deleted {deleted} and freed {HumanFormat.Bytes(pruned.ReclaimedBytes)}.", InfoBarSeverity.Success);
    }

    public void CopyName(VolumeItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _clipboard.SetText(item.Name);
    }

    /// <summary>Reads which containers mount each volume; overlapping calls fold into one more pass.</summary>
    public async Task RefreshUsageAsync()
    {
        if (_isRefreshing)
        {
            _refreshAgain = true;

            return;
        }

        _isRefreshing = true;
        try
        {
            do
            {
                _refreshAgain = false;
                var result = await _sender.Send(new GetVolumeUsageQuery());
                _usage = result.IsSuccess
                    ? result.Value.ToLookup(mount => mount.Volume, StringComparer.Ordinal)
                    : null;
                Show(Selected?.Name);
            }
            while (_refreshAgain);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    protected override Task<IAsyncEnumerable<ListChange<VolumeDto>>> OpenAsync(CancellationToken cancellationToken)
    {
        return _sender.Send(new WatchVolumesQuery(), cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Rows.CollectionChanged -= OnRowsChanged;
            Deactivate();
        }

        base.Dispose(disposing);
    }

    private static string Count(int count)
    {
        return count.ToString(CultureInfo.CurrentCulture);
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Show(Selected?.Name);
        _ = RefreshUsageAsync();
    }

    /// <summary>Rebuilds the items with <paramref name="selectedName"/> selected, or the first volume.</summary>
    private void Show(string? selectedName)
    {
        var selected = Rows.Any(row => row.Name == selectedName) ? selectedName : Rows.FirstOrDefault()?.Name;
        var items = Rows
            .Select(row => new VolumeItem(row, Mounts(row.Name), row.Name == selected))
            .ToList();

        CollectionSync.Sync(Items, items, item => item.Name);
        Selected = items.FirstOrDefault(item => item.IsSelected);

        var unused = items.Count(item => item.IsUnused);
        HasUnused = _usage is null || unused > 0;
        var volumes = items.Count == 1 ? "1 volume" : $"{Count(items.Count)} volumes";
        Subtitle = items.Count == 0
            ? string.Empty
            : _usage is null
                ? volumes
                : $"{volumes} · {Count(unused)} not used by any container";
    }

    private IReadOnlyList<VolumeMountDto>? Mounts(string name)
    {
        return _usage is null ? null : [.. _usage[name]];
    }
}
