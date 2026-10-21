using System;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Daemon;

namespace Contr.App.UI;

/// <summary>One row of Settings' command-line tools table, from what the daemon reports.</summary>
/// <param name="Name">How a terminal calls the tool, such as <c>docker compose</c>.</param>
/// <param name="Version">The installed version; a dash while not installed.</param>
/// <param name="Status">Such as <c>Ready</c> or <c>Second on PATH</c>.</param>
public sealed record CliToolRow(string Name, string Description, string Version, string Status, PillKind Kind)
{
    public bool NeedsAttention => Kind != PillKind.Good;

    /// <summary>Where a tool sorts in the table: docker, its plugins, then the rest.</summary>
    public static int Rank(CliTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        return tool.Name switch {
            "docker" => 0,
            "compose" => 1,
            "buildx" => 2,
            _ => 3,
        };
    }

    public static CliToolRow From(CliTool tool, CliToolsStatus tools)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(tools);

        var (name, description) = tool.Name switch {
            "docker" => ("docker", "Command-line client"),
            "compose" => ("docker compose", "Compose v2 plugin"),
            "buildx" => ("docker buildx", "BuildKit builder plugin"),
            "credential-wincred" => (
                "docker-credential-wincred", "Keeps registry sign-ins in Windows Credential Manager"),
            _ => (tool.Name, string.Empty),
        };
        var isDocker = tool.Name == "docker";
        var (status, kind) = tool switch {
            { InstalledVersion.Length: 0 } => ("Not installed", PillKind.Caution),
            { BundledVersion.Length: > 0 } when tool.BundledVersion != tool.InstalledVersion =>
                ($"{tool.BundledVersion} bundled", PillKind.Caution),
            _ when isDocker && tools.ShadowedBy.Length > 0 => ("Second on PATH", PillKind.Caution),
            _ when isDocker && !tools.IsOnPath => ("Not on PATH", PillKind.Caution),
            _ => ("Ready", PillKind.Good),
        };

        return new CliToolRow(
            name, description, tool.InstalledVersion.Length > 0 ? tool.InstalledVersion : "—", status, kind);
    }
}
