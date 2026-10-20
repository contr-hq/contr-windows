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
using Contr.Modules.Images.Application.Images.Queries.PullImage;
using Contr.Modules.Images.Application.Images.Queries.WatchImages;
using Contr.UI.Common.Engine;
using Contr.UI.Common.Formatting;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Images.UI.ViewModels;

/// <summary>The images list, pulling with per-layer progress, removal and pruning.</summary>
public sealed partial class ImagesViewModel : LiveListViewModel<ImageDto, ImageRow>
{
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly IClipboardService _clipboard;
    private readonly Dictionary<string, PullLayer> _layers = new(StringComparer.Ordinal);
    private CancellationTokenSource? _pull;

    [ObservableProperty]
    public partial bool IsPulling { get; set; }

    /// <summary>Such as <c>Pulling postgres:17.2-alpine</c>.</summary>
    [ObservableProperty]
    public partial string PullTitle { get; set; }

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

    /// <summary>Such as <c>9 images · 2.2 GB on disk · 1.1 GB unused</c>.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; set; }

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
        IDaemonConnection daemon)
        : base(
            new LiveCollection<ImageDto, ImageRow>(ImageRow.From, row => row.Id, ImageRow.Compare),
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
        PullTitle = string.Empty;
        PullPlatform = string.Empty;
        PullStatus = string.Empty;
        Subtitle = string.Empty;
        RemoveUnusedText = "Remove unused";
        DanglingTitle = string.Empty;
        DanglingSize = string.Empty;
        DanglingUsedBy = string.Empty;
        Rows.CollectionChanged += OnRowsChanged;
    }

    public EngineGateViewModel Engine { get; }

    /// <summary>The tagged images; untagged ones are summed up in one row.</summary>
    public ObservableCollection<ImageRow> TaggedRows { get; } = [];

    public ObservableCollection<PullLayer> PullLayers { get; } = [];

    public bool HasPullPlatform => PullPlatform.Length > 0;

    /// <summary>Starts following the engine and the list; call it from the page's <c>Loaded</c>.</summary>
    public void Activate()
    {
        Engine.Start();
        StartWatching();
        Summarise();
    }

    /// <summary>Stops following; a pull in progress goes on.</summary>
    public void Deactivate()
    {
        StopWatching();
        Engine.Stop();
    }

    /// <summary>Pulls <paramref name="reference"/>, replacing any pull in progress.</summary>
    /// <param name="platform">Such as <c>linux/arm64</c>; empty pulls for the engine's own platform.</param>
    public async Task PullAsync(string reference, string platform = "")
    {
        ArgumentNullException.ThrowIfNull(reference);

        var started = await _sender.Send(new PullImageQuery(reference, platform));

        if (started.IsFailure)
        {
            _notifications.Show(started.Error.Message, InfoBarSeverity.Error, "Cannot pull");

            return;
        }

        CancelPull();
        var pull = new CancellationTokenSource();
        _pull = pull;
        _layers.Clear();
        PullLayers.Clear();
        PullTitle = $"Pulling {reference.Trim()}";
        PullPlatform = platform;
        OnPropertyChanged(nameof(HasPullPlatform));
        PullStatus = "Starting";
        PullProgress = 0;
        IsPullIndeterminate = true;
        IsPulling = true;
        try
        {
            await foreach (var pullEvent in started.Value.WithCancellation(pull.Token))
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
                IsPulling = false;
                _pull = null;
            }

            pull.Dispose();
        }
    }

    public void CancelPull()
    {
        _pull?.Cancel();
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
            CancelPull();
            Deactivate();
        }

        base.Dispose(disposing);
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
        CollectionSync.Sync(TaggedRows, tagged, row => row.Id);

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

        var images = Rows.Count == 1 ? "1 image" : $"{Count(Rows.Count)} images";
        var summary = $"{images} · {HumanFormat.Bytes(Rows.Sum(row => row.SizeBytes))} on disk";
        Subtitle = Rows.Count == 0
            ? string.Empty
            : isUsageKnown ? $"{summary} · {HumanFormat.Bytes(unusedBytes)} unused" : summary;
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
