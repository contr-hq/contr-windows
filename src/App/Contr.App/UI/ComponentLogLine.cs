using System.Text.RegularExpressions;

namespace Contr.App.UI;

/// <summary>One line of the client's log as Troubleshoot shows it: the time, the level and what happened.</summary>
/// <param name="Time">Such as <c>10:51:58</c>; empty on a continuation line, such as a stack frame.</param>
/// <param name="Level">Such as <c>INFO</c>; empty on a continuation line.</param>
/// <param name="Text">The logging class's own name, then the message.</param>
public sealed partial record ComponentLogLine(string Time, string Level, string Text)
{
    public bool IsError => Level is "ERROR" or "FATAL";

    public bool IsWarning => Level == "WARN";

    public bool IsOther => !IsError && !IsWarning;

    /// <summary>The time and a space, so a continuation line starts at the margin.</summary>
    public string Stamp => Time.Length > 0 ? $"{Time} " : string.Empty;

    /// <summary>The level, when it is an error, and a space; one run per colour.</summary>
    public string ErrorLevel => IsError ? $"{Level} " : string.Empty;

    public string WarningLevel => IsWarning ? $"{Level} " : string.Empty;

    public string OtherLevel => IsOther && Level.Length > 0 ? $"{Level} " : string.Empty;

    /// <summary>Reads a line written by <see cref="Services.Platform.FileLoggerProvider"/>.</summary>
    public static ComponentLogLine Parse(string line)
    {
        var text = line.TrimEnd('\r');
        var match = Entry().Match(text);

        if (!match.Success)
        {
            return new ComponentLogLine(string.Empty, string.Empty, text);
        }

        var category = match.Groups["category"].Value;
        var source = category[(category.LastIndexOf('.') + 1)..];

        return new ComponentLogLine(
            match.Groups["time"].Value,
            LevelName(match.Groups["level"].Value),
            $"{source}: {match.Groups["message"].Value}");
    }

    private static string LevelName(string code)
    {
        return code switch {
            "TRC" => "TRACE",
            "DBG" => "DEBUG",
            "INF" => "INFO",
            "WRN" => "WARN",
            "ERR" => "ERROR",
            "CRT" => "FATAL",
            _ => code,
        };
    }

    [GeneratedRegex(
        @"^\d{4}-\d{2}-\d{2} (?<time>\d{2}:\d{2}:\d{2})\.\d{3} "
            + @"\[(?<level>[A-Z?]{3})\] (?<category>[^:\s]+): (?<message>.*)$")]
    private static partial Regex Entry();
}
