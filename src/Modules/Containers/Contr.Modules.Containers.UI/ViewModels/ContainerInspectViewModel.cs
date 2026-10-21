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

    [ObservableProperty]
    public partial IReadOnlyList<InspectFact> Setup { get; set; }

    /// <summary>Such as <c>6 variables</c>.</summary>
    [ObservableProperty]
    public partial string EnvironmentSummary { get; set; }

    /// <summary>The variables once shown; empty while hidden.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEnvironmentText))]
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
        Setup = [];
        EnvironmentSummary = string.Empty;
        EnvironmentText = string.Empty;
        FindText = string.Empty;
    }

    public bool HasEnvironmentText => EnvironmentText.Length > 0;

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
        Setup = facts.Setup;
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
        EnvironmentSummary = _environment.Count == 1 ? "1 variable" : $"{_environment.Count} variables";
        EnvironmentText = IsEnvironmentShown ? string.Join(Environment.NewLine, _environment) : string.Empty;
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
