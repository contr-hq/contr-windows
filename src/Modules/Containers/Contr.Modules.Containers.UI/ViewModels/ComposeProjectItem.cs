using System;
using System.Globalization;
using Contr.UI.Common.Controls;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>One Compose project in the project list: its containers as Compose labelled them.</summary>
/// <param name="Location">The folder Compose ran in, or its file when the folder is unknown.</param>
public sealed record ComposeProjectItem(string Name, string Location, int Running, int Total, bool IsSelected)
{
    public bool HasLocation => Location.Length > 0;

    public StatusKind StatusKind => Running == 0
        ? StatusKind.Neutral
        : Running == Total ? StatusKind.Good : StatusKind.Caution;

    /// <summary>Such as <c>3 of 4</c> running, or <c>Not running</c>.</summary>
    public string StatusText => Running == 0
        ? "Not running"
        : $"{Running.ToString(CultureInfo.CurrentCulture)} of {Total.ToString(CultureInfo.CurrentCulture)}";

    public static int Compare(ComposeProjectItem? left, ComposeProjectItem? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>One line of a project's merged logs, prefixed with its service.</summary>
/// <param name="Color">An index into the page's service colours; each service keeps one.</param>
public sealed record ComposeLogLine(string Service, int Color, string Text);
