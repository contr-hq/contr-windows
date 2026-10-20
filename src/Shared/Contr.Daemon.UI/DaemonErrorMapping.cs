using System;
using Contr.Application.Abstractions.Daemon;
using Contr.Sdk;
using Contr.Sdk.Common.V1;
using Contr.SharedKernel;
using Grpc.Core;

namespace Contr.Daemon.UI;

/// <summary>Turns SDK failures into the <see cref="DaemonErrors"/> the application layer branches on.</summary>
public static class DaemonErrorMapping
{
    public static Error ToError(this ContrException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch {
            EngineNotRunningException => DaemonErrors.EngineNotRunning,
            DaemonUnavailableException => DaemonErrors.Unavailable,
            NotFoundException => new Error(DaemonErrors.NotFoundCode, exception.Message),
            ConflictException => new Error(DaemonErrors.ConflictCode, exception.Message),
            PermissionDeniedException => new Error(DaemonErrors.PermissionDeniedCode, exception.Message),
            FeatureUnavailableException => new Error(DaemonErrors.FeatureUnavailableCode, exception.Message),
            VersionMismatchException => new Error(DaemonErrors.VersionMismatchCode, exception.Message),

            // A channel disposed under an open call cancels it; the connection is what failed.
            { InnerException: RpcException { StatusCode: StatusCode.Cancelled or StatusCode.Unavailable } } =>
                DaemonErrors.Unavailable,
            _ => new Error(DaemonErrors.FailedCode, exception.Message),
        };
    }

    public static Error ToError(this RpcException exception)
    {
        var classified = ContrException.From(exception);

        return classified.ToError();
    }

    /// <summary>A user-facing sentence for the reason an engine failed.</summary>
    public static string Describe(ErrorReason reason)
    {
        return reason switch {
            ErrorReason.EngineNotInstalled => "The container engine is not installed.",
            ErrorReason.WslNotInstalled => "WSL is not installed.",
            ErrorReason.VirtualizationDisabled => "Virtualization is turned off in the firmware settings.",
            ErrorReason.SocketPermissionDenied => "Access to the engine socket was denied.",
            _ => "The container engine failed.",
        };
    }
}
