using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Daemon;

namespace Contr.App.UI;

/// <summary>One row of Settings' installed versions: a part of Contr and the version that runs.</summary>
/// <param name="Status">Empty where nothing is known about it, such as whether an update exists.</param>
public sealed record InstalledComponent(string Name, string WhatItIs, string Version, string Status, PillKind Kind)
{
    public bool HasStatus => Status.Length > 0;

    /// <summary>The parts the app and the daemon report; the rest stay out rather than show guesses.</summary>
    /// <param name="available">The newer Contr the daemon found, such as <c>0.2.0</c>; empty without one.</param>
    public static IReadOnlyList<InstalledComponent> From(
        string appVersion, DaemonIdentity? daemon, EngineDetails? engine, CliToolsStatus? tools, string available = "")
    {
        ArgumentNullException.ThrowIfNull(appVersion);
        ArgumentNullException.ThrowIfNull(available);

        List<InstalledComponent> rows = [];
        var (update, kind) = available.Length > 0 ? ($"{available} available", PillKind.Busy) : (string.Empty, default);

        if (daemon is null || Release(daemon.Version) == Release(appVersion))
        {
            rows.Add(new("Contr app", "Windows app, tray icon and the contrd service", appVersion, update, kind));
        }
        else
        {
            rows.Add(new("Contr app", "Windows app and tray icon", appVersion, update, kind));
            rows.Add(new("Contr service", "contrd, which runs the engine", daemon.Version, update, kind));
        }

        rows.Add(new(
            "Engine",
            daemon is null ? "Docker Engine" : $"Docker Engine · {daemon.Backend} backend",
            engine?.Version ?? "—",
            string.Empty,
            default));

        var compose = tools?.Tools.FirstOrDefault(tool => tool.Name == "compose");

        if (compose is { InstalledVersion.Length: > 0 })
        {
            rows.Add(new(
                "Compose",
                "Compose v2 plugin for the docker command",
                compose.InstalledVersion,
                string.Empty,
                default));
        }

        if (daemon is not null)
        {
            rows.Add(new(
                "API revision",
                "Contract between the app and the contrd service",
                daemon.ApiRevision.ToString(CultureInfo.InvariantCulture),
                "Compatible",
                PillKind.Good));
        }

        return rows;
    }

    /// <summary>The rows as text for a bug report, one per line, then Windows' version.</summary>
    public static string Details(IEnumerable<InstalledComponent> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var lines = rows
            .Select(row => $"{row.Name}: {row.Version}")
            .Append($"Windows: {Environment.OSVersion.Version}");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>A version without its build metadata, as <c>0.1.0</c> for <c>0.1.0+3f5c264</c>.</summary>
    private static string Release(string version)
    {
        var plus = version.IndexOf('+', StringComparison.Ordinal);

        return plus < 0 ? version : version[..plus];
    }
}
