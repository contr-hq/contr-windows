using Contr.SharedKernel;

namespace Contr.Application.Abstractions.Daemon;

/// <summary>Errors every daemon adapter returns; view models branch on their codes.</summary>
public static class DaemonErrors
{
    /// <summary>Code of <see cref="Unavailable"/>.</summary>
    public const string UnavailableCode = "Daemon.Unavailable";

    /// <summary>Code of <see cref="EngineNotRunning"/>.</summary>
    public const string EngineNotRunningCode = "Daemon.EngineNotRunning";

    /// <summary>The object the request named does not exist.</summary>
    public const string NotFoundCode = "Daemon.NotFound";

    /// <summary>The object exists already, or its state forbids the request.</summary>
    public const string ConflictCode = "Daemon.Conflict";

    /// <summary>Access was refused.</summary>
    public const string PermissionDeniedCode = "Daemon.PermissionDenied";

    /// <summary>The daemon or its backend does not offer the request.</summary>
    public const string FeatureUnavailableCode = "Daemon.FeatureUnavailable";

    /// <summary>The daemon speaks an API revision this client cannot use.</summary>
    public const string VersionMismatchCode = "Daemon.VersionMismatch";

    /// <summary>Any other failure the daemon reported.</summary>
    public const string FailedCode = "Daemon.Failed";

    /// <summary>No daemon answered, or the connection broke.</summary>
    public static Error Unavailable { get; } =
        new(UnavailableCode, "Contr is not running. It reconnects when the daemon is back.");

    /// <summary>The daemon answered, but its container engine is stopped or starting.</summary>
    public static Error EngineNotRunning { get; } =
        new(EngineNotRunningCode, "The container engine is not running.");
}
