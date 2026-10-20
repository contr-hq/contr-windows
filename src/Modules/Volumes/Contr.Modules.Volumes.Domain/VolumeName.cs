using System;
using System.Text.RegularExpressions;
using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Domain;

/// <summary>A volume's name, which is also its ID.</summary>
public sealed partial record VolumeName
{
    public VolumeName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    /// <summary>Validates a name a user typed against the engine's rule for named volumes.</summary>
    public static Result<VolumeName> Parse(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return Result.Failure<VolumeName>(VolumesErrors.NameRequired);
        }

        return NamePattern().IsMatch(trimmed)
            ? new VolumeName(trimmed)
            : Result.Failure<VolumeName>(VolumesErrors.NameInvalid(trimmed));
    }

    public override string ToString()
    {
        return Value;
    }

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
