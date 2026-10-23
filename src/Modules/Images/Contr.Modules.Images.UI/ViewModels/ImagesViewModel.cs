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
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Application.Images.Commands.PruneImages;
using Contr.Modules.Images.Application.Images.Commands.RemoveImage;
using Contr.Modules.Images.Application.Images.Queries.FollowPull;
using Contr.Modules.Images.Application.Images.Queries.PullImage;
using Contr.Modules.Images.Application.Images.Queries.WatchImages;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Engine;
using Contr.UI.Common.Formatting;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Images.UI.ViewModels;

/// <summary>
/// The images list, pulling with per-layer progress, removal and pruning. Where the daemon runs pulls as operations,
/// the progress card follows the newest running pull, whichever page or client started it.
/// </summary>
public sealed partial class ImagesViewModel : LiveListViewModel<ImageDto, ImageRow>
{
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly IClipboardService _clipboard;
    private readonly IActivity _activity;
    private readonly DispatcherQueue? _dispatcher;
    private readonly Dictionary<string, PullLayer> _layers = new(StringComparer.Ordinal);
    private CancellationTokenSource? _pull;
    private IDisposable? _operations;

    /// <summary>Whether the page shows, between <see cref="Activate"/> and <see cref="Deactivate"/>.</summary>
    private bool _isActive;

    /// <summary>The daemon operation of the pull the card follows; absent for an older daemon's pull.</summary>
    private string? _operationId;

    /// <summary>Pulls the card followed to their end, which the daemon may still list as running for a moment.
    /// </summary>
    private readonly HashSet<string> _shown = new(StringComparer.Ordinal);

    [ObservableProperty]
    public partial bool IsPulling { get; set; }

    /// <summary>Such as <c>postgres:17.2-alpine</c>.</summary>
    [ObservableProperty]
    public partial string PullReference { get; set; }

    /// <summary>Such as <c>linux/arm64</c>; empty for the engine's own platform.</summary>
    [ObservableProperty]
    public partial string PullPlatform { get; set; }

    /// <summary>Such as <c>87.4 MB of 141 MB</c>, or the engine's last status line.</summary>
    [ObservableProperty]
    public partial string PullStatus { get; set; }

    [ObservableProperty]
    public partial double PullProgress { get; set; }

    [ObservableProperty]
    public partial bool IsPullIndeterminate { get; set; }

    /// <summary>Counted over every image, tagged or not.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PillItem> SummaryPills { get; set; }

    /// <summary>Such as <c>Remove unused (1.1 GB)</c>.</summary>
    [ObservableProperty]
    public partial string RemoveUnusedText { get; set; }

    [ObservableProperty]
    public partial bool HasUnused { get; set; }

    [ObservableProperty]
    public partial bool HasDangling { get; set; }

    /// <summary>Such as <c>Untagged (3)</c>.</summary>
    [ObservableProperty]
    public partial string DanglingTitle { get; set; }

    [ObservableProperty]
    public partial string DanglingSize { get; set; }

    [ObservableProperty]
    public partial string DanglingUsedBy { get; set; }

    public ImagesViewModel(
        ISender sender,
        IDialogService dialogs,
        INotificationService notifications,
        IClipboardService clipboard,
        IDaemonConnection daemon,
        IDaemonSettings settings,
        IActivity activity)
        : base(
            new LiveCollection<ImageDto, ImageRow>(ImageRow.From, row => row.Id, ImageRow.Compare),
            _retryDelay)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(activity);
        _sender = sender;
        _dialogs = dialogs;
        _notifications = notifications;
        _clipboard = clipboard;
        _activity = activity;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Engine = new EngineGateViewModel(daemon, settings, notifications);
        PullReference = string.Empty;
        PullPlatform = string.Empty;
        PullStatus = string.Empty;
        SummaryPills = [];
        RemoveUnusedText = "Remove unused";
        DanglingTitle = string.Empty;
        DanglingSize = string.Empty;
        DanglingUsedBy = string.Empty;
        Rows.CollectionChanged += OnRowsChanged;
    }

    public EngineGateViewModel Engine { get; }

    /// <summary>The tagged images, five to a page; untagged ones are summed up in one row after the last.</summary>
    public Paging<ImageRow> Page { get; } = new("IMAGES", row => row.Id);

    public ObservableCollection<PullLayer> PullLayers { get; } = [];

    /// <summary>Starts following the engine, the list and the running pulls; call it from the page's <c>Loaded</c>.
    /// </summary>
    public void Activate()
    {
        Engine.Start();
        StartWatching();
        Summarise();
        _isActive = true;
        _operations ??= _activity.Changes.Subscribe(OnOperations);
    }

    /// <summary>Stops following; a pull in progress goes on, and so does its card while the view model lives.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        _operations?.Dispose();
        _operations = null;
        StopWatching();
        Engine.Stop();
    }

    /// <summary>The pull the progress card follows; complete while there is none.</summary>
    public Task Pulling { get; private set; } = Task.CompletedTask;

    /// <summary>Starts pulling <paramref name="reference"/>, replacing any pull in progress.</summary>
    /// <param name="platform">Such as <c>linux/arm64</c>; empty pulls for the engine's own platform.</param>
    /// <returns>Why the engine refused to start; absent once the pull runs.</returns>
    public async Task<string?> StartPullAsync(string reference, string platform = "")
    {
        ArgumentNullException.ThrowIfNull(reference);

        var started = await _sender.Send(new PullImageQuery(reference, platform));

        if (started.IsFailure)
        {
            return started.Error.Message;
        }

        Pulling = FollowAsync(reference, platform, started.Value);

        return null;
    }

    /// <summary>Cancels the pull the card follows; an older daemon's pull stops with the card.</summary>
    public void CancelPull()
    {
        if (_operationId is { } id)
        {
            _ = CancelOperationAsync(id);

            return;
        }

        StopFollowing();
    }

    /// <summary>Asks first; the engine refuses while a container uses the image.</summary>
    public async Task RemoveAsync(ImageRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!await _dialogs.ShowConfirmAsync("Delete image", $"Delete {row.DisplayName}?", "Delete", "Cancel"))
        {
            return;
        }

        var result = await _sender.Send(new RemoveImageCommand(row.ImageId));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, $"Could not delete {row.DisplayName}");
        }
    }

    /// <summary>Deletes every image no container uses, after asking.</summary>
    public Task RemoveUnusedAsync()
    {
        const string message = "Delete every image no container uses? Pulling them again downloads them again.";

        return PruneAsync(all: true, "Remove unused images", message);
    }

    /// <summary>Deletes the untagged images no container uses, after asking.</summary>
    public Task RemoveDanglingAsync()
    {
        return PruneAsync(
            all: false,
            "Remove untagged images",
            "Delete the untagged images no container uses? They are older builds and pulls no tag names any more.");
    }

    public void CopyReference(ImageRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        _clipboard.SetText(row.IsDangling ? row.Id : row.DisplayName);
    }

    public void CopyId(ImageRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        _clipboard.SetText(row.Id);
    }

    protected override Task<IAsyncEnumerable<ListChange<ImageDto>>> OpenAsync(CancellationToken cancellationToken)
    {
        return _sender.Send(new WatchImagesQuery(), cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Rows.CollectionChanged -= OnRowsChanged;
            StopFollowing();
            Deactivate();
        }

        base.Dispose(disposing);
    }

    private async Task FollowAsync(
        string reference, string platform, IAsyncEnumerable<PullEvent> events, string? operationId = null)
    {
        StopFollowing();
        var pull = new CancellationTokenSource();
        _pull = pull;
        _operationId = operationId;
        _layers.Clear();
        PullLayers.Clear();
        PullReference = reference.Trim();
        PullPlatform = platform;
        PullStatus = "Starting";
        PullProgress = 0;
        IsPullIndeterminate = true;
        IsPulling = true;
        try
        {
            await foreach (var pullEvent in events.WithCancellation(pull.Token))
            {
                Apply(pullEvent);
            }
        }
        catch (OperationCanceledException) when (pull.IsCancellationRequested)
        {
            // Cancelled from the progress panel, or replaced by another pull.
        }
        finally
        {
            if (ReferenceEquals(_pull, pull))
            {
                if (_operationId is { } shown && !pull.IsCancellationRequested)
                {
                    _shown.Add(shown);
                }

                IsPulling = false;
                _pull = null;
                _operationId = null;
            }

            pull.Dispose();
        }

        if (_isActive && !IsPulling)
        {
            FollowRunning(_activity.Current);
        }
    }

    private void StopFollowing()
    {
        _pull?.Cancel();
    }

    private async Task CancelOperationAsync(string id)
    {
        var result = await _activity.CancelAsync(id, CancellationToken.None);

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "The pull wasn't cancelled");
        }
    }

    private void OnOperations(IReadOnlyList<DaemonOperation> operations)
    {
        // Changes emits on a background thread.
        if (_dispatcher is null)
        {
            FollowRunning(operations);

            return;
        }

        _dispatcher.TryEnqueue(() => FollowRunning(operations));
    }

    /// <summary>Shows the newest running pull while the card shows none.</summary>
    private void FollowRunning(IReadOnlyList<DaemonOperation> operations)
    {
        if (IsPulling || !_isActive)
        {
            return;
        }

        var running = operations.FirstOrDefault(operation =>
            operation is { IsRunning: true, Work: OperationWork.PullImage } && !_shown.Contains(operation.Id));

        if (running is not { Work: OperationWork.PullImage pull })
        {
            return;
        }

        IsPulling = true;
        Pulling = FollowOperationAsync(running.Id, pull);
    }

    private async Task FollowOperationAsync(string operationId, OperationWork.PullImage pull)
    {
        var events = await _sender.Send(new FollowPullQuery(operationId));
        await FollowAsync(pull.Reference, pull.Platform, events, operationId);
    }

    private List<PillItem> Summarize(bool isUsageKnown, long unusedBytes)
    {
        var images = Rows.Count == 1 ? "1 image" : $"{Count(Rows.Count)} images";
        List<PillItem> pills = [
            new(images, PillKind.Plain),
            new($"{HumanFormat.Bytes(Rows.Sum(row => row.SizeBytes))} on disk", PillKind.Plain),
        ];

        if (isUsageKnown && unusedBytes > 0)
        {
            pills.Add(new($"{HumanFormat.Bytes(unusedBytes)} unused", PillKind.Hollow));
        }

        return pills;
    }

    private static string Count(long count)
    {
        return count.ToString(CultureInfo.CurrentCulture);
    }

    private async Task PruneAsync(bool all, string title, string message)
    {
        if (!await _dialogs.ShowConfirmAsync(title, message, "Delete", "Cancel"))
        {
            return;
        }

        var result = await _sender.Send(new PruneImagesCommand(all));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "Could not remove the images");

            return;
        }

        var pruned = result.Value;
        var deleted = pruned.Deleted == 1 ? "1 image" : $"{Count(pruned.Deleted)} images";
        _notifications.Show(
            $"Deleted {deleted} and freed {HumanFormat.Bytes(pruned.ReclaimedBytes)}.", InfoBarSeverity.Success);
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Summarise();
    }

    private void Summarise()
    {
        var tagged = Rows
            .Where(row => !row.IsDangling)
            .ToList();
        Page.Show(tagged);

        var dangling = Rows
            .Where(row => row.IsDangling)
            .ToList();
        HasDangling = dangling.Count > 0;
        DanglingTitle = $"Untagged ({Count(dangling.Count)})";
        DanglingSize = HumanFormat.Bytes(dangling.Sum(row => row.SizeBytes));
        DanglingUsedBy = dangling.Any(row => row.InUse) ? "Some in use" : "Not used";

        // An engine that does not count an image's containers leaves what is unused unknown.
        var isUsageKnown = Rows.All(row => row.Containers >= 0);
        var unusedBytes = Rows
            .Where(row => row.Containers == 0)
            .Sum(row => row.SizeBytes);
        HasUnused = !isUsageKnown || Rows.Any(row => row.Containers == 0);
        RemoveUnusedText = isUsageKnown && HasUnused
            ? $"Remove unused ({HumanFormat.Bytes(unusedBytes)})"
            : "Remove unused";

        var pills = Summarize(isUsageKnown, unusedBytes);

        if (!pills.SequenceEqual(SummaryPills))
        {
            SummaryPills = pills;
        }
    }

    private void Apply(PullEvent pullEvent)
    {
        switch (pullEvent)
        {
            case PullEvent.Layer layer:
                ShowLayer(layer);
                break;
            case PullEvent.StatusLine status:
                PullStatus = status.Text;
                break;
            case PullEvent.Pulled pulled:
                _notifications.Show($"Pulled {pulled.Reference}.", InfoBarSeverity.Success);
                break;
            case PullEvent.Failed failed:
                _notifications.Show(failed.Error.Message, InfoBarSeverity.Error, "Pull failed");
                break;
            case PullEvent.Started started:
                _operationId = started.OperationId;
                break;
        }
    }

    private void ShowLayer(PullEvent.Layer update)
    {
        if (!_layers.TryGetValue(update.LayerId, out var layer))
        {
            layer = new PullLayer(update.LayerId);
            _layers[update.LayerId] = layer;
            PullLayers.Add(layer);
        }

        layer.Apply(update);

        var total = _layers.Values.Sum(item => item.TotalBytes);
        var current = _layers.Values.Sum(item => item.CurrentBytes);
        var done = _layers.Values.Count(item => item.IsDone);

        var layers = $"{Count(done)} of {Count(_layers.Count)} layers";

        IsPullIndeterminate = total == 0;
        PullProgress = total == 0 ? 0 : 100.0 * current / total;
        PullStatus = total == 0
            ? layers
            : $"{HumanFormat.Bytes(current)} of {HumanFormat.Bytes(total)} · {layers}";
    }
}
