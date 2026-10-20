using System;

namespace Contr.Modules.Images.Domain;

/// <summary>An image's content-addressed ID, such as <c>sha256:5e1a…</c>.</summary>
public sealed record ImageId
{
    private const string _algorithmSeparator = ":";
    private const int _shortLength = 12;

    public ImageId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    /// <summary>The first twelve hex digits, as <c>docker images</c> prints them.</summary>
    public string Short
    {
        get {
            var separator = Value.IndexOf(_algorithmSeparator, StringComparison.Ordinal);
            var hex = separator >= 0 ? Value[(separator + 1)..] : Value;

            return hex.Length > _shortLength ? hex[.._shortLength] : hex;
        }
    }

    public override string ToString()
    {
        return Value;
    }
}
