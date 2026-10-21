using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Contr.SharedKernel;

namespace Contr.UI.Contracts.Services.Daemon;

/// <summary>The daemon's checks of what keeps the engine from working, and the fixes it runs.</summary>
public interface IDiagnostics
{
    /// <summary>Code of a fix the user stopped at the administrator prompt.</summary>
    const string ElevationDeclinedCode = "Diagnostics.ElevationDeclined";

    /// <summary>Runs every check the daemon and its backend make.</summary>
    /// <returns>The checks in the order to show them; a failure with code <c>Daemon.FeatureUnavailable</c> from a
    /// daemon older than API revision 8.</returns>
    Task<Result<IReadOnlyList<DiagnosticCheck>>> RunChecksAsync(CancellationToken cancellationToken);

    /// <summary>Runs a fix that a check offered.</summary>
    /// <param name="actionId">A <see cref="Remediation.ActionId"/>.</param>
    /// <param name="progress">Receives each step the daemon reports.</param>
    /// <param name="cancellationToken">Stops following the fix; the daemon finishes it.</param>
    /// <returns>What the user must do to finish; a failure with code <c>Daemon.NotFound</c> where the backend
    /// offers no such fix, <see cref="ElevationDeclinedCode"/> when the user declined the administrator prompt,
    /// or <c>Daemon.Conflict</c> when it failed otherwise.</returns>
    Task<Result<RemediationOutcome>> RunRemediationAsync(
        string actionId, IProgress<EngineProgress> progress, CancellationToken cancellationToken);

    /// <summary>Writes a zip for support to <paramref name="destination"/>: versions, settings, check results, the
    /// engine's state and recent logs, with credentials and the user's name, home folder, computer name and Windows
    /// SIDs replaced.</summary>
    /// <param name="destination">Receives the zip, which is whole only when the call succeeds.</param>
    /// <param name="progress">Receives each step the daemon reports.</param>
    /// <param name="cancellationToken">Stops the bundle.</param>
    /// <returns>A failure with code <c>Daemon.FeatureUnavailable</c> from a daemon older than API revision 18.
    /// </returns>
    Task<Result> CreateBundleAsync(
        Stream destination, IProgress<EngineProgress> progress, CancellationToken cancellationToken);
}

/// <summary>What one check found.</summary>
public enum DiagnosticStatus
{
    /// <summary>The daemon sent a status this client does not know.</summary>
    Unknown,

    /// <summary>Nothing to fix.</summary>
    Pass,

    /// <summary>The engine works, but something the user expects may not.</summary>
    Warn,

    /// <summary>Keeps the engine, or the part checked, from working.</summary>
    Fail,

    /// <summary>Not checked, such as the engine's API while the engine is stopped.</summary>
    Skip,
}

/// <summary>One check the daemon made.</summary>
/// <param name="Id">Stable across releases, such as <see cref="PrerequisitesId"/>.</param>
/// <param name="Title">What was checked, such as <c>Free disk space</c>.</param>
/// <param name="Status">What the check found.</param>
/// <param name="Detail">What the check found, in one sentence.</param>
/// <param name="Remediations">The fixes the daemon runs, in the order to offer them.</param>
public sealed record DiagnosticCheck(
    string Id, string Title, DiagnosticStatus Status, string Detail, IReadOnlyList<Remediation> Remediations)
{
    /// <summary>Whether the backend has everything it needs to set up and start the engine.</summary>
    public const string PrerequisitesId = "prerequisites";
}

/// <summary>A fix the daemon runs.</summary>
/// <param name="ActionId">Stable across releases, such as <see cref="InstallWslId"/>.</param>
/// <param name="RequiresElevation">Whether Windows asks for administrator approval while it runs.</param>
public sealed record Remediation(string ActionId, bool RequiresElevation)
{
    /// <summary>Installs WSL 2 without a distribution; Windows may need a restart afterwards.</summary>
    public const string InstallWslId = "install-wsl";
}

/// <summary>What the user must do to finish a fix.</summary>
/// <param name="RestartRequired">Windows must restart before the fix takes effect.</param>
/// <param name="SignOutRequired">The user must sign out and back in before the fix takes effect.</param>
public sealed record RemediationOutcome(bool RestartRequired, bool SignOutRequired);
