using System;
using System.Text.RegularExpressions;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Domain;

/// <summary>A container's name, unique on the engine.</summary>
public sealed partial record ContainerName
{
    public ContainerName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    /// <summary>Validates a name a user typed against the engine's rule for container names.</summary>
    public static Result<ContainerName> Parse(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return Result.Failure<ContainerName>(ContainersErrors.NameRequired);
        }

        return NamePattern().IsMatch(trimmed)
            ? new ContainerName(trimmed)
            : Result.Failure<ContainerName>(ContainersErrors.NameInvalid(trimmed));
    }

    public override string ToString()
    {
        return Value;
    }

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
