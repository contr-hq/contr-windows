namespace Contr.UI.Common.Controls;

/// <summary>What a stat card's number means, which decides its colour.</summary>
public enum StatKind
{
    /// <summary>A count with no judgement; takes the accent.</summary>
    Accent,

    Positive,

    /// <summary>Dormant, which is not wrong.</summary>
    Muted,

    Warning,

    Critical,
}

/// <summary>One of the counts along the top of a page.</summary>
/// <param name="Label">Upper case, such as <c>RUNNING</c>.</param>
/// <param name="Detail">One short line under the value; empty draws nothing.</param>
public sealed record StatCard(string Label, string Value, string Detail, string Glyph, StatKind Kind);
