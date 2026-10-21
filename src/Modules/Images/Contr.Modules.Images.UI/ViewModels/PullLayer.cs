using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Modules.Images.Application.Images;
using Contr.UI.Common.Formatting;

namespace Contr.Modules.Images.UI.ViewModels;

/// <summary>One layer of a pull in progress.</summary>
public sealed partial class PullLayer : ObservableObject
{
    private const int _shortIdLength = 12;

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial bool IsDone { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    public PullLayer(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id.Length > _shortIdLength ? id[.._shortIdLength] : id;
        StatusText = "Waiting";
    }

    public string Id { get; }

    /// <summary>The layer's size; zero while the engine has not reported it.</summary>
    public long TotalBytes { get; private set; }

    public long CurrentBytes { get; private set; }

    /// <summary>Whether the engine reported the step that ends a layer: pulled, or present already.</summary>
    public static bool Finishes(string status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return status.StartsWith("Pull complete", StringComparison.Ordinal)
            || status.StartsWith("Already exists", StringComparison.Ordinal);
    }

    public void Apply(PullEvent.Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        if (layer.TotalBytes > 0)
        {
            TotalBytes = layer.TotalBytes;
            CurrentBytes = Math.Min(layer.CurrentBytes, layer.TotalBytes);
        }

        IsDone = IsDone || Finishes(layer.Status);
        CurrentBytes = IsDone ? TotalBytes : CurrentBytes;
        Progress = IsDone ? 100 : TotalBytes > 0 ? 100.0 * CurrentBytes / TotalBytes : 0;
        StatusText = Describe(layer);
    }

    private string Describe(PullEvent.Layer layer)
    {
        if (layer.Status.StartsWith("Downloading", StringComparison.Ordinal) && TotalBytes > 0)
        {
            return $"Downloading · {HumanFormat.Bytes(CurrentBytes)} of {HumanFormat.Bytes(TotalBytes)}";
        }

        if (layer.Status.StartsWith("Extracting", StringComparison.Ordinal) && TotalBytes > 0)
        {
            return $"Extracting · {Progress:0}%";
        }

        if (layer.Status.StartsWith("Pull complete", StringComparison.Ordinal) && TotalBytes > 0)
        {
            return $"Pull complete · {HumanFormat.Bytes(TotalBytes)}";
        }

        return layer.Status;
    }
}
