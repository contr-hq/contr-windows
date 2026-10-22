using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.SharedKernel;

namespace Contr.UI.Contracts.Services.Daemon;

/// <summary>
/// Whether a newer Contr is published: the daemon reads the releases once a day while
/// <see cref="DaemonSetting.UpdatesCheck"/> is on. Clients install updates their own way.
/// </summary>
public interface IDaemonUpdates
{
    /// <summary>The latest check's result, without checking.</summary>
    Task<Result<UpdateStatus>> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>Checks now.</summary>
    /// <returns>The result; a failure with code <c>Daemon.Conflict</c> while <see cref="DaemonSetting.UpdatesCheck"/>
    /// is off.</returns>
    Task<Result<UpdateStatus>> CheckAsync(CancellationToken cancellationToken);
}

/// <summary>What the daemon's latest update check found.</summary>
/// <param name="CurrentVersion">The running daemon's version, such as <c>0.1.0-alpha.2</c>.</param>
/// <param name="Available">The newest release after <paramref name="CurrentVersion"/>; absent when none is newer or no
/// check has succeeded.</param>
/// <param name="CheckedAt">When the latest check ended; absent before the first.</param>
/// <param name="Error">Why the latest check failed; empty when it succeeded.</param>
public sealed record UpdateStatus(
    string CurrentVersion, AvailableRelease? Available, DateTimeOffset? CheckedAt, string Error);

/// <summary>A published release newer than the running one.</summary>
/// <param name="Version">Such as <c>0.2.0</c> or <c>0.2.1-beta.1</c>.</param>
/// <param name="Url">The release's page; absent when the daemon named none a browser opens.</param>
/// <param name="PublishedAt">When it was published; absent when unknown.</param>
/// <param name="Notes">The release notes, in Markdown.</param>
public sealed record AvailableRelease(string Version, Uri? Url, DateTimeOffset? PublishedAt, string Notes);
