using System;

namespace Contr.Modules.Containers.Domain;

/// <summary>A container's full engine ID.</summary>
public sealed record ContainerId
{
    private const int _shortLength = 12;

    public ContainerId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    /// <summary>The first twelve characters, as <c>docker ps</c> prints them.</summary>
    public string Short => Value.Length > _shortLength ? Value[.._shortLength] : Value;

    public override string ToString()
    {
        return Value;
    }
}
