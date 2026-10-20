using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.UI.Common.Lists;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>A card of the containers list: one Compose project, the standalone containers, or the flat list.</summary>
public sealed partial class ContainerGroup : ObservableObject
{
    /// <summary>The key of the card that holds containers outside any Compose project.</summary>
    public const string StandaloneKey = "";

    /// <summary>The key of the single card the flat list shows.</summary>
    public const string FlatKey = "\0flat";

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; }

    [ObservableProperty]
    public partial bool CanStartAll { get; set; }

    [ObservableProperty]
    public partial bool CanStopAll { get; set; }

    public ContainerGroup(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Key = key;
        IsExpanded = true;
        Summary = string.Empty;
    }

    public string Key { get; }

    public bool IsProject => Key is not (StandaloneKey or FlatKey);

    public bool HasHeader => Key != FlatKey;

    public string Title => IsProject ? Key : "Standalone";

    /// <summary>A Segoe Fluent Icons glyph: a list for a project, a package for the standalone card.</summary>
    public string Glyph => IsProject ? "\uE8FD" : "\uE7B8";

    /// <summary>The rows the filter and the stopped toggle let through.</summary>
    public ObservableCollection<ContainerRow> Rows { get; } = [];

    /// <summary>Shows the <paramref name="visible"/> rows; the summary counts <paramref name="all"/>.</summary>
    public void Update(IReadOnlyList<ContainerRow> visible, IReadOnlyList<ContainerRow> all)
    {
        ArgumentNullException.ThrowIfNull(visible);
        ArgumentNullException.ThrowIfNull(all);

        CollectionSync.Sync(Rows, visible, row => row.Id);
        Summary = Summarise(all);
        CanStartAll = all.Any(row => row.CanStart);
        CanStopAll = all.Any(row => row.CanStop);
    }

    private string Summarise(IReadOnlyList<ContainerRow> all)
    {
        var containers = all.Count == 1 ? "1 container" : $"{all.Count} containers";

        if (!IsProject)
        {
            return $"{containers} · not part of a compose project";
        }

        var running = all.Count(row => row.IsRunning);
        var file = all
            .Select(row => row.ComposeFile)
            .FirstOrDefault(file => file.Length > 0);

        return file is null ? $"{containers} · {running} running" : $"{containers} · {running} running · {file}";
    }
}
