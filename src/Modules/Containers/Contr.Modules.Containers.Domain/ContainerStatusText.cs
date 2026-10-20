using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Contr.Modules.Containers.Domain;

/// <summary>
/// Reads the engine's status text, such as <c>Up 2 hours (healthy)</c> or <c>Exited (1) 14 minutes ago</c>,
/// which is the only place a container listing carries health and exit code.
/// </summary>
public static partial class ContainerStatusText
{
    public static ContainerHealth Health(string status)
    {
        if (status.Contains("(unhealthy)", StringComparison.Ordinal))
        {
            return ContainerHealth.Unhealthy;
        }

        if (status.Contains("(healthy)", StringComparison.Ordinal))
        {
            return ContainerHealth.Healthy;
        }

        return status.Contains("(health: starting)", StringComparison.Ordinal)
            ? ContainerHealth.Starting
            : ContainerHealth.None;
    }

    /// <summary>The code an exited container returned; <see langword="null"/> for any other status.</summary>
    public static int? ExitCode(string status)
    {
        var match = ExitedPattern().Match(status);

        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// How long the container has been up, or since it exited, shortened such as <c>2 h</c> or <c>14 min</c>;
    /// empty when the status names no duration.
    /// </summary>
    public static string Elapsed(string status)
    {
        var match = ElapsedPattern().Match(status);

        if (!match.Success)
        {
            return string.Empty;
        }

        var duration = match.Groups["duration"].Value;

        return duration switch {
            "Less than a second" => "now",
            "About a minute" => "1 min",
            "About an hour" => "1 h",
            _ => Shorten(duration),
        };
    }

    private static string Shorten(string duration)
    {
        var parts = duration.Split(' ');
        var culture = CultureInfo.InvariantCulture;

        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, culture, out var count))
        {
            return string.Empty;
        }

        var unit = parts[1].TrimEnd('s') switch {
            "second" => "s",
            "minute" => "min",
            "hour" => "h",
            "day" => "d",
            "week" => "wk",
            "month" => "mo",
            "year" => "y",
            _ => null,
        };

        return unit is null ? string.Empty : $"{count.ToString(CultureInfo.CurrentCulture)} {unit}";
    }

    [GeneratedRegex(@"^Exited \((-?\d+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex ExitedPattern();

    [GeneratedRegex(
        @"^(?:Up|(?:Exited|Restarting) \(-?\d+\)) (?<duration>.+?)(?: ago)?(?: \([^)]*\))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ElapsedPattern();
}
