namespace Contr.UI.Common.Controls;

/// <summary>What a <see cref="Pill"/> shows before its text: a coloured dot, a hollow one, a lock or nothing.</summary>
public enum PillKind
{
    /// <summary>Text only, such as a count.</summary>
    Plain,

    /// <summary>A green dot: running, passed.</summary>
    Good,

    /// <summary>An accent dot: starting, restarting, in progress.</summary>
    Busy,

    /// <summary>A yellow dot: paused, unhealthy.</summary>
    Caution,

    /// <summary>A hollow dot: stopped, exited cleanly, created, not started.</summary>
    Hollow,

    /// <summary>A red dot on a critical background: needs attention.</summary>
    Critical,

    /// <summary>A success background without a dot, such as a verified signature.</summary>
    Success,

    /// <summary>A lock: set by the user's organization.</summary>
    Locked,
}

/// <summary>One pill of a page's summary row, such as <c>5 running</c>.</summary>
public sealed record PillItem(string Text, PillKind Kind);
