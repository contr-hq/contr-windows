using System;
using System.Globalization;

namespace Contr.UI.Common.Formatting;

/// <summary>Sizes and ages as the Docker CLI prints them.</summary>
public static class HumanFormat
{
    private static readonly string[] _units = ["B", "kB", "MB", "GB", "TB"];

    /// <summary>Decimal units with up to four significant digits, such as <c>192 MB</c> or <c>12.34 GB</c>.</summary>
    public static string Bytes(long bytes)
    {
        if (bytes < 0)
        {
            return string.Empty;
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1000 && unit < _units.Length - 1)
        {
            value /= 1000;
            unit++;
        }

        var format = unit == 0 ? "0" : value >= 100 ? "0.#" : value >= 10 ? "0.##" : "0.###";

        return $"{value.ToString(format, CultureInfo.CurrentCulture)} {_units[unit]}";
    }

    /// <summary>How long ago <paramref name="moment"/> was, such as <c>3 days ago</c>.</summary>
    public static string Age(DateTimeOffset moment, DateTimeOffset now)
    {
        var elapsed = now - moment;

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "Just now";
        }

        return elapsed switch {
            { TotalHours: < 1 } => Plural((int)elapsed.TotalMinutes, "minute"),
            { TotalDays: < 1 } => Plural((int)elapsed.TotalHours, "hour"),
            { TotalDays: < 14 } => Plural((int)elapsed.TotalDays, "day"),
            { TotalDays: < 60 } => Plural((int)(elapsed.TotalDays / 7), "week"),
            { TotalDays: < 730 } => Plural((int)(elapsed.TotalDays / 30), "month"),
            _ => Plural((int)(elapsed.TotalDays / 365), "year"),
        };
    }

    /// <summary>
    /// Kakehashi's short relative time: <c>now</c>, <c>14m ago</c>, <c>3h ago</c>, <c>3d ago</c>, then the date,
    /// such as <c>Oct 12, 2026</c>.
    /// </summary>
    public static string Ago(DateTimeOffset moment, DateTimeOffset now)
    {
        var elapsed = now - moment;

        return elapsed switch {
            { TotalMinutes: < 1 } => "now",
            { TotalHours: < 1 } => $"{(int)elapsed.TotalMinutes}m ago",
            { TotalDays: < 1 } => $"{(int)elapsed.TotalHours}h ago",
            { TotalDays: < 7 } => $"{(int)elapsed.TotalDays}d ago",
            _ => moment
                .ToLocalTime()
                .ToString("MMM d, yyyy", CultureInfo.CurrentCulture),
        };
    }

    private static string Plural(int count, string unit)
    {
        return count == 1 ? $"1 {unit} ago" : $"{count} {unit}s ago";
    }
}
