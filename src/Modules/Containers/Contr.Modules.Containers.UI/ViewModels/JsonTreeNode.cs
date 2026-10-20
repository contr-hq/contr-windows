using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>What a JSON value is, which decides its colour.</summary>
public enum JsonTokenKind
{
    String,
    Number,

    /// <summary><c>true</c>, <c>false</c> or <c>null</c>.</summary>
    Literal,

    /// <summary>An object or an array, shown as a summary such as <c>{ 8 keys }</c>.</summary>
    Container,
}

/// <summary>One member of the inspect tab's JSON tree.</summary>
public sealed partial class JsonTreeNode : ObservableObject
{
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    private JsonTreeNode(string key, string value, JsonTokenKind kind, IReadOnlyList<JsonTreeNode> children)
    {
        Key = key;
        Value = value;
        Kind = kind;
        Children = [.. children];
    }

    /// <summary>The member's name in quotes; empty for an array element.</summary>
    public string Key { get; }

    public string Value { get; }

    public JsonTokenKind Kind { get; }

    public bool HasKey => Key.Length > 0;

    public ObservableCollection<JsonTreeNode> Children { get; }

    /// <summary>The document's top-level members, or none when <paramref name="json"/> is not JSON.</summary>
    public static IReadOnlyList<JsonTreeNode> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);

            return Members(document.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The members whose name contains <paramref name="key"/>, with the members that lead to them, expanded;
    /// an empty key returns <paramref name="nodes"/> themselves.
    /// </summary>
    public static IReadOnlyList<JsonTreeNode> Find(IReadOnlyList<JsonTreeNode> nodes, string key)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length == 0)
        {
            return nodes;
        }

        return [.. nodes
            .Select(node => node.Matching(key))
            .OfType<JsonTreeNode>()];
    }

    public void Collapse()
    {
        IsExpanded = false;

        foreach (var child in Children)
        {
            child.Collapse();
        }
    }

    private static List<JsonTreeNode> Members(JsonElement element)
    {
        return element.ValueKind switch {
            JsonValueKind.Object => [.. element
                .EnumerateObject()
                .Select(property => Create($"\"{property.Name}\"", property.Value))],
            JsonValueKind.Array => [.. element
                .EnumerateArray()
                .Select(item => Create(string.Empty, item))],
            _ => [],
        };
    }

    private static JsonTreeNode Create(string key, JsonElement value)
    {
        return value.ValueKind switch {
            JsonValueKind.Object => Container(key, value, Count(Members(value).Count, "key")),
            JsonValueKind.Array => Container(key, value, Count(value.GetArrayLength(), "item")),
            JsonValueKind.String => new(key, value.GetRawText(), JsonTokenKind.String, []),
            JsonValueKind.Number => new(key, value.GetRawText(), JsonTokenKind.Number, []),
            _ => new(key, value.GetRawText(), JsonTokenKind.Literal, []),
        };
    }

    private static JsonTreeNode Container(string key, JsonElement value, string summary)
    {
        return new JsonTreeNode(key, summary, JsonTokenKind.Container, Members(value));
    }

    private static string Count(int count, string noun)
    {
        var open = noun == "key" ? "{" : "[";
        var close = noun == "key" ? "}" : "]";
        var counted = count == 1 ? $"1 {noun}" : $"{count.ToString(CultureInfo.CurrentCulture)} {noun}s";

        return $"{open} {counted} {close}";
    }

    private JsonTreeNode? Matching(string key)
    {
        var children = Children
            .Select(child => child.Matching(key))
            .OfType<JsonTreeNode>()
            .ToList();
        var matches = Key.Contains(key, StringComparison.OrdinalIgnoreCase);

        if (!matches && children.Count == 0)
        {
            return null;
        }

        var copy = new JsonTreeNode(Key, Value, Kind, matches ? [.. Children] : children);
        copy.IsExpanded = children.Count > 0;

        return copy;
    }
}
