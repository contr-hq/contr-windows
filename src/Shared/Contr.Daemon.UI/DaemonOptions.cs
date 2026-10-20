namespace Contr.Daemon.UI;

/// <summary>How the app starts the daemon when none answers, bound from the <c>Daemon</c> section.</summary>
public sealed class DaemonOptions
{
    public const string SectionName = "Daemon";

    /// <summary>Path of <c>contrd.exe</c>; empty runs the one beside the app, else the one on PATH.</summary>
    public string Path { get; set; } = string.Empty;

    public string[] Arguments { get; set; } = [];

    /// <summary>Whether a new connection starts an engine it finds stopped or never provisioned.</summary>
    public bool StartEngine { get; set; } = true;
}
