using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.SharedKernel;

namespace Contr.UI.Contracts.Services.Daemon;

/// <summary>The daemon's settings for this user: defaults, the user's own values, and values an administrator fixes.
/// </summary>
public interface IDaemonSettings
{
    /// <summary>Code of a change to a setting an administrator fixes.</summary>
    const string SettingLockedCode = "Daemon.SettingLocked";

    /// <summary>
    /// The current settings, then again after every change, wherever it came from; absent while no daemon is
    /// connected, or where the daemon keeps none (older than API revision 11).
    /// </summary>
    /// <remarks>Emits on a background thread; subscribers share one stream from the daemon.</remarks>
    IObservable<DaemonSettings?> Changes { get; }

    /// <summary>Changes one setting.</summary>
    /// <returns>The settings afterwards; a failure with code <see cref="SettingLockedCode"/> where an administrator
    /// fixes the setting.</returns>
    Task<Result<DaemonSettingsChange>> SetAsync(
        DaemonSetting setting, bool value, CancellationToken cancellationToken);

    /// <summary>Returns every setting the user changed to its default; settings an administrator fixes stay.</summary>
    Task<Result<DaemonSettingsChange>> ResetAsync(CancellationToken cancellationToken);
}

/// <summary>A setting the daemon keeps.</summary>
public enum DaemonSetting
{
    /// <summary>The daemon starts the engine whenever the daemon starts.</summary>
    StartEngineWithDaemon,

    /// <summary>The engine also serves <c>\\.\pipe\docker_engine</c> while no other program does, from its next
    /// start.</summary>
    ServeDefaultPipe,
}

/// <summary>The daemon's settings in effect.</summary>
/// <param name="StartEngineWithDaemon">See <see cref="DaemonSetting.StartEngineWithDaemon"/>.</param>
/// <param name="ServeDefaultPipe">See <see cref="DaemonSetting.ServeDefaultPipe"/>.</param>
/// <param name="Locked">The settings an administrator fixes; the user cannot change them.</param>
public sealed record DaemonSettings(
    bool StartEngineWithDaemon, bool ServeDefaultPipe, IReadOnlySet<DaemonSetting> Locked)
{
    /// <summary>Whether an administrator fixes <paramref name="setting"/>.</summary>
    public bool IsLocked(DaemonSetting setting)
    {
        return Locked.Contains(setting);
    }

    /// <summary>The value in effect for <paramref name="setting"/>.</summary>
    public bool ValueOf(DaemonSetting setting)
    {
        return setting switch {
            DaemonSetting.StartEngineWithDaemon => StartEngineWithDaemon,
            DaemonSetting.ServeDefaultPipe => ServeDefaultPipe,
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, null),
        };
    }
}

/// <summary>The settings after a change.</summary>
/// <param name="Settings">The settings in effect afterwards.</param>
/// <param name="RestartRequired">Changed settings the running engine applies only at its next start.</param>
public sealed record DaemonSettingsChange(DaemonSettings Settings, IReadOnlySet<DaemonSetting> RestartRequired);
