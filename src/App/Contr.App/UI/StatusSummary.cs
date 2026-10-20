using System;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Daemon;

namespace Contr.App.UI;

/// <summary>How the shell's status bar and the tray describe the daemon and its engine.</summary>
/// <param name="Text">A short label, such as "Engine running".</param>
/// <param name="Kind">The icon drawn before the label.</param>
/// <param name="Detail">A sentence for the tooltip; empty when the label says it all.</param>
public sealed record StatusSummary(string Text, StatusKind Kind, string Detail)
{
    public static StatusSummary From(DaemonStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return status.Connection switch {
            DaemonConnectionState.Connecting =>
                new("Connecting", StatusKind.Busy, "Connecting to the Contr daemon."),
            DaemonConnectionState.Unavailable =>
                new("Daemon unavailable", StatusKind.Critical, status.Problem),
            _ => FromEngine(status),
        };
    }

    private static StatusSummary FromEngine(DaemonStatus status)
    {
        return status.Engine switch {
            EngineState.Running => new("Engine running", StatusKind.Good, string.Empty),
            EngineState.Starting or EngineState.Provisioning =>
                new("Engine starting", StatusKind.Busy, string.Empty),
            EngineState.Updating => new("Engine updating", StatusKind.Busy, string.Empty),
            EngineState.Resetting => new("Engine resetting", StatusKind.Busy, string.Empty),
            EngineState.Stopping => new("Engine stopping", StatusKind.Busy, string.Empty),
            EngineState.Stopped => new("Engine stopped", StatusKind.Neutral, string.Empty),
            EngineState.Uninitialized => new("Engine not set up", StatusKind.Neutral, string.Empty),
            EngineState.Failed => new("Engine failed", StatusKind.Critical, status.Problem),
            _ => new("Connected", StatusKind.Busy, "Waiting for the engine state."),
        };
    }
}
