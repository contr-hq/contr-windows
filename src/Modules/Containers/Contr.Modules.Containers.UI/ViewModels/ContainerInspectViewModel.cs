using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private IReadOnlyList<string> _environment = [];
    private string _rawJson = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<InspectFact> Configuration { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<InspectFact> State { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<InspectFact> Limits { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<InspectFact> Mounts { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<InspectFact> Networks { get; set; }

    /// <summary>Such as <c>Project shop-api · service api</c>; empty outside Compose.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCompose))]
    public partial string Compose { get; set; }

    /// <summary>Such as <c>6 variables · values hidden</c>.</summary>
    [ObservableProperty]
    public partial string EnvironmentSummary { get; set; }

    /// <summary>The variables, or only their names while the values are hidden.</summary>
    [ObservableProperty]
    public partial string EnvironmentText { get; set; }

    [ObservableProperty]
    public partial bool IsEnvironmentShown { get; set; }

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
        Mounts = [];
        Networks = [];
        Compose = string.Empty;
        EnvironmentSummary = string.Empty;
        EnvironmentText = string.Empty;
        FindText = string.Empty;
    }

    public bool HasCompose => Compose.Length > 0;

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
        Mounts = facts.Mounts;
        Networks = facts.Networks;
        Compose = facts.Compose;
        _environment = facts.Environment;
        ShowEnvironment();
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

    partial void OnIsEnvironmentShownChanged(bool value)
    {
        ShowEnvironment();
    }

    private void ShowEnvironment()
    {
        var count = _environment.Count == 1 ? "1 variable" : $"{_environment.Count} variables";
        EnvironmentSummary = IsEnvironmentShown ? count : $"{count} · values hidden";
        EnvironmentText = string.Join(
            Environment.NewLine,
            IsEnvironmentShown ? _environment : _environment.Select(entry => entry.Split('=', 2)[0]));
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
