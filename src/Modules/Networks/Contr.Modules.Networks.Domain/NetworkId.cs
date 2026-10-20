using System;

namespace Contr.Modules.Networks.Domain;

/// <summary>A network's full engine ID.</summary>
public sealed record NetworkId
{
    private const int _shortLength = 12;

    public NetworkId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    /// <summary>The first twelve characters, as <c>docker network ls</c> prints them.</summary>
    public string Short => Value.Length > _shortLength ? Value[.._shortLength] : Value;

    public override string ToString()
    {
        return Value;
    }
}
