using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Application.Images.Commands.RemoveImage;
using Contr.Modules.Images.Application.Images.Queries.PullImage;
using Contr.Modules.Images.Application.Images.Queries.WatchImages;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Images.UI.ViewModels;

/// <summary>The images list, pulling with per-layer progress, and removal.</summary>
public sealed partial class ImagesViewModel : LiveListViewModel<ImageDto, ImageRow>
{
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly Dictionary<string, PullEvent.Layer> _layers = new(StringComparer.Ordinal);
    private CancellationTokenSource? _pull;

    [ObservableProperty]
    public partial bool IsPulling { get; set; }

    [ObservableProperty]
    public partial string PullTitle { get; set; }

    [ObservableProperty]
    public partial string PullStatus { get; set; }

    [ObservableProperty]
    public partial double PullProgress { get; set; }

    [ObservableProperty]
    public partial bool IsPullIndeterminate { get; set; }

    public ImagesViewModel(ISender sender, IDialogService dialogs, INotificationService notifications)
        : base(
            new LiveCollection<ImageDto, ImageRow>(ImageRow.From, row => row.Id, ImageRow.Compare),
            _retryDelay)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        _sender = sender;
        _dialogs = dialogs;
        _notifications = notifications;
        PullTitle = string.Empty;
        PullStatus = string.Empty;
    }

    /// <summary>Asks which image to pull, then pulls it.</summary>
    public async Task PromptAndPullAsync()
    {
        var reference = await _dialogs.ShowPromptAsync(
            "Pull image", "Image, such as nginx:latest", string.Empty, "Pull");

        if (reference is not null)
        {
            await PullAsync(reference);
        }
    }

    /// <summary>Pulls <paramref name="reference"/>, replacing any pull in progress.</summary>
    public async Task PullAsync(string reference)
    {
        var started = await _sender.Send(new PullImageQuery(reference));

        if (started.IsFailure)
        {
            _notifications.Show(started.Error.Message, InfoBarSeverity.Error, "Cannot pull");

            return;
        }

        CancelPull();
        var pull = new CancellationTokenSource();
        _pull = pull;
        _layers.Clear();
        PullTitle = $"Pulling {reference.Trim()}";
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

    protected override Task<IAsyncEnumerable<ListChange<ImageDto>>> OpenAsync(CancellationToken cancellationToken)
    {
        return _sender.Send(new WatchImagesQuery(), cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPull();
        }

        base.Dispose(disposing);
    }

    /// <summary>Whether the engine reported the layer finished: downloaded and extracted, or present already.</summary>
    private static bool IsDone(PullEvent.Layer layer)
    {
        return layer.Status.StartsWith("Pull complete", StringComparison.Ordinal)
            || layer.Status.StartsWith("Already exists", StringComparison.Ordinal);
    }

    private void Apply(PullEvent pullEvent)
    {
        switch (pullEvent)
        {
            case PullEvent.Layer layer:
                _layers[layer.LayerId] = layer;
                ShowLayerProgress();
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

    private void ShowLayerProgress()
    {
        var done = _layers.Values.Count(IsDone);
        var sized = _layers.Values
            .Where(layer => layer.TotalBytes > 0)
            .ToList();
        var total = sized.Sum(layer => layer.TotalBytes);
        var completed = sized.Sum(
            layer => IsDone(layer) ? layer.TotalBytes : Math.Min(layer.CurrentBytes, layer.TotalBytes));

        IsPullIndeterminate = total == 0;
        PullProgress = total == 0 ? 0 : 100.0 * completed / total;
        PullStatus = $"{done} of {_layers.Count} layers";
    }
}
