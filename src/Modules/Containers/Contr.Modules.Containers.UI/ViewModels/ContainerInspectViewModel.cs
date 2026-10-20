using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Modules.Containers.Application.Containers;
using Contr.UI.Contracts.Services.Platform;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>The inspect tab: a summary of the container and the engine's full inspection document.</summary>
public sealed partial class ContainerInspectViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true };

    private readonly IClipboardService _clipboard;
    private IReadOnlyList<JsonTreeNode> _document = [];
    private string _rawJson = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<InspectFact> Configuration { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<InspectFact> State { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<InspectFact> Limits { get; set; }

    [ObservableProperty]
    public partial string FindText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    public ContainerInspectViewModel(IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        _clipboard = clipboard;
        Configuration = [];
        State = [];
        Limits = [];
        FindText = string.Empty;
    }

    /// <summary>The JSON tree, narrowed to the members <see cref="FindText"/> names.</summary>
    public ObservableCollection<JsonTreeNode> Nodes { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(Error);

    public void Show(ContainerDetailsDto details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        var facts = InspectFacts.From(details, now);
        Configuration = facts.Configuration;
        State = facts.State;
        Limits = facts.Limits;
        Error = null;

        if (details.RawJson != _rawJson)
        {
            _rawJson = details.RawJson;
            _document = JsonTreeNode.Parse(_rawJson);
            ShowNodes();
        }
    }

    public void CollapseAll()
    {
        foreach (var node in Nodes)
        {
            node.Collapse();
        }
    }

    public void CopyJson()
    {
        try
        {
            using var document = JsonDocument.Parse(_rawJson);
            _clipboard.SetText(JsonSerializer.Serialize(document.RootElement, _indented));
        }
        catch (JsonException)
        {
            _clipboard.SetText(_rawJson);
        }
    }

    partial void OnFindTextChanged(string value)
    {
        ShowNodes();
    }

    private void ShowNodes()
    {
        Nodes.Clear();

        foreach (var node in JsonTreeNode.Find(_document, FindText.Trim()))
        {
            Nodes.Add(node);
        }
    }
}
