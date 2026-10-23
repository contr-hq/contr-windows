using System;
using Contr.UI.Common.Formatting;
using Contr.UI.Contracts.Services.Daemon;

namespace Contr.App.UI;

/// <summary>The words the activity view and its notifications use for an operation.</summary>
public static class ActivityText
{
    /// <summary>Such as <c>Pull postgres:17</c> or <c>Compose up · shop</c>.</summary>
    public static string Title(OperationWork work)
    {
        ArgumentNullException.ThrowIfNull(work);

        return work switch {
            OperationWork.PullImage { Platform.Length: > 0 } pull => $"Pull {pull.Reference} · {pull.Platform}",
            OperationWork.PullImage pull => $"Pull {pull.Reference}",
            OperationWork.PushImage push => $"Push {push.Reference}",
            OperationWork.ComposeAction compose => $"Compose {compose.Action} · {Target(compose)}",
            OperationWork.ImportImages { Images.Count: 1 } import => $"Import {import.Images[0]} into Kubernetes",
            OperationWork.ImportImages import => $"Import {import.Images.Count} images into Kubernetes",
            OperationWork.CreateBundle => "Diagnostics bundle",
            OperationWork.RunRemediation fix => new CheckFix(new Remediation(fix.ActionId, false)).Label,
            OperationWork.StartEngine => "Start the engine",
            OperationWork.StopEngine => "Stop the engine",
            OperationWork.RestartEngine => "Restart the engine",
            OperationWork.ResetEngine { Scope: EngineResetScope.Factory } => "Factory reset",
            OperationWork.ResetEngine => "Reset the engine",
            _ => "Work in Contr's background service",
        };
    }

    /// <summary>The operation's latest step, or how and when it ended, such as <c>Done · 2m ago</c>.</summary>
    public static string Detail(DaemonOperation operation, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var when = operation.FinishedAt is { } finished ? $" · {HumanFormat.Ago(finished, now)}" : string.Empty;

        return operation.State switch {
            OperationState.Running => operation.Progress?.Message is { Length: > 0 } step ? step : "Starting…",
            OperationState.Succeeded => $"Done{when}",
            OperationState.Cancelled => $"Cancelled{when}",
            _ => operation.Failure?.Error.Message is { Length: > 0 } reason ? reason : $"Failed{when}",
        };
    }

    /// <summary>A notification's first line for an operation that ended, such as <c>Pulled postgres:17</c>.</summary>
    public static string Outcome(DaemonOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.State == OperationState.Cancelled)
        {
            return $"Cancelled: {Title(operation.Work)}";
        }

        if (operation.State != OperationState.Succeeded)
        {
            return $"Failed: {Title(operation.Work)}";
        }

        return operation.Work switch {
            OperationWork.PullImage pull => $"Pulled {pull.Reference}",
            OperationWork.PushImage push => $"Pushed {push.Reference}",
            OperationWork.ComposeAction compose => $"Compose {compose.Action} finished for {Target(compose)}",
            OperationWork.ImportImages => "Imported the images into Kubernetes",
            OperationWork.CreateBundle => "The diagnostics bundle is ready to save",
            OperationWork.RunRemediation fix => new CheckFix(new Remediation(fix.ActionId, false)).Done,
            OperationWork.ResetEngine { Scope: EngineResetScope.Factory } => "Contr deleted the engine and its data",
            OperationWork.ResetEngine => "The engine was reset",
            _ => $"Done: {Title(operation.Work)}",
        };
    }

    /// <summary>Whether ending deserves a notification while the window is hidden; an engine started or stopped
    /// as asked, such as at sign-in, does not.</summary>
    public static bool IsNews(DaemonOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation.State != OperationState.Succeeded
            || operation.Work is not (OperationWork.StartEngine or OperationWork.StopEngine
                or OperationWork.RestartEngine);
    }

    private static string Target(OperationWork.ComposeAction compose)
    {
        return compose.Services.Count == 1 ? compose.Services[0] : compose.Project;
    }
}
