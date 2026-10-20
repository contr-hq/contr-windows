using System;
using Contr.UI.Contracts.Services.Daemon;

namespace Contr.App.UI;

/// <summary>The colour family a status is drawn in.</summary>
public enum StatusTone
{
    Neutral,
    Busy,
    Good,
    Problem,
}

/// <summary>How the shell's status pill and the tray describe the daemon and its engine.</summary>
/// <param name="Text">A short label, such as "Engine running".</param>
/// <param name="Tone">The colour of the pill's dot.</param>
/// <param name="Detail">A sentence for the tooltip; empty when the label says it all.</param>
public sealed record StatusSummary(string Text, StatusTone Tone, string Detail)
{
    public static StatusSummary From(DaemonStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return status.Connection switch {
            DaemonConnectionState.Connecting =>
                new("Connecting", StatusTone.Busy, "Connecting to the Contr daemon."),
            DaemonConnectionState.Unavailable =>
                new("Daemon unavailable", StatusTone.Problem, status.Problem),
            _ => FromEngine(status),
        };
    }

    private static StatusSummary FromEngine(DaemonStatus status)
    {
        return status.Engine switch {
            EngineState.Running => new("Engine running", StatusTone.Good, string.Empty),
            EngineState.Starting or EngineState.Provisioning =>
                new("Engine starting", StatusTone.Busy, string.Empty),
            EngineState.Updating => new("Engine updating", StatusTone.Busy, string.Empty),
            EngineState.Resetting => new("Engine resetting", StatusTone.Busy, string.Empty),
            EngineState.Stopping => new("Engine stopping", StatusTone.Busy, string.Empty),
            EngineState.Stopped => new("Engine stopped", StatusTone.Neutral, string.Empty),
            EngineState.Uninitialized => new("Engine not set up", StatusTone.Neutral, string.Empty),
            EngineState.Failed => new("Engine failed", StatusTone.Problem, status.Problem),
            _ => new("Connected", StatusTone.Busy, "Waiting for the engine state."),
        };
    }
}
