using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Code of a value the daemon refuses, such as a proxy that is not a URL; the message reads
    /// <c>proxy.http: "x" is not a URL</c>, see <see cref="DaemonSettingPaths.Refused"/>.</summary>
    const string InvalidValueCode = "Daemon.InvalidSetting";

    /// <summary>
    /// The current settings, then again after every change, wherever it came from; absent while no daemon is
    /// connected, or where the daemon keeps none (older than API revision 11).
    /// </summary>
    /// <remarks>Emits on a background thread; subscribers share one stream from the daemon.</remarks>
    IObservable<DaemonSettings?> Changes { get; }

    /// <summary>Changes the settings <paramref name="edit"/> names, all or none of them.</summary>
    /// <returns>The settings afterwards; a failure with code <see cref="SettingLockedCode"/> where an administrator
    /// fixes one of them, or <see cref="InvalidValueCode"/> for a value the daemon refuses.</returns>
    Task<Result<DaemonSettingsChange>> UpdateAsync(DaemonSettingsEdit edit, CancellationToken cancellationToken);

    /// <summary>Replaces the engine's <c>daemon.json</c> once the engine accepts it.</summary>
    /// <param name="text">A JSON object; <c>{}</c> holds no settings.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The settings afterwards, or why the daemon refused the file, with the keys the engine does not know.
    /// </returns>
    Task<DaemonJsonUpdate> UpdateDaemonJsonAsync(string text, CancellationToken cancellationToken);

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

    /// <summary>How the engine finds its proxy.</summary>
    ProxyMode,

    /// <summary>In manual mode, the proxy for <c>http://</c> destinations.</summary>
    ProxyHttp,

    /// <summary>In manual mode, the proxy for <c>https://</c> destinations.</summary>
    ProxyHttps,

    /// <summary>In manual mode, the destinations the engine reaches directly, as a <c>NO_PROXY</c> list.</summary>
    ProxyExclude,

    /// <summary>The engine also trusts the root certificates Windows trusts.</summary>
    TrustSystemCertificates,

    /// <summary>The Docker Hub mirrors the engine tries before Docker Hub.</summary>
    RegistryMirrors,

    /// <summary>The engine's own <c>daemon.json</c>, from its next start.</summary>
    DaemonJson,

    /// <summary>New containers and builds also receive the engine's proxy as <c>HTTP_PROXY</c>, <c>HTTPS_PROXY</c>
    /// and <c>NO_PROXY</c>, unless they set the variable themselves; from the engine's next start.</summary>
    PassProxyToContainers,
}

/// <summary>How the engine finds its proxy.</summary>
public enum ProxyMode
{
    /// <summary>Windows' proxy settings, including a configuration script or automatic detection.</summary>
    System,

    /// <summary>The servers the user names.</summary>
    Manual,

    /// <summary>Direct connections.</summary>
    None,
}

/// <summary>How the engine reaches servers outside the machine; the servers apply only in manual mode.</summary>
/// <param name="Mode">How the engine finds its proxy.</param>
/// <param name="Http">See <see cref="DaemonSetting.ProxyHttp"/>; empty for none.</param>
/// <param name="Https">See <see cref="DaemonSetting.ProxyHttps"/>; empty for none.</param>
/// <param name="Exclude">See <see cref="DaemonSetting.ProxyExclude"/>; empty for none.</param>
public sealed record ProxySettings(ProxyMode Mode, string Http, string Https, string Exclude)
{
    /// <summary>See <see cref="DaemonSetting.PassProxyToContainers"/>.</summary>
    public bool PassToContainers { get; init; }

    /// <summary>The daemon's defaults.</summary>
    public static ProxySettings Default { get; } = new(ProxyMode.System, string.Empty, string.Empty, string.Empty);
}

/// <summary>The daemon's settings in effect.</summary>
/// <param name="StartEngineWithDaemon">See <see cref="DaemonSetting.StartEngineWithDaemon"/>.</param>
/// <param name="ServeDefaultPipe">See <see cref="DaemonSetting.ServeDefaultPipe"/>.</param>
/// <param name="Locked">The settings an administrator fixes; the user cannot change them.</param>
public sealed record DaemonSettings(
    bool StartEngineWithDaemon, bool ServeDefaultPipe, IReadOnlySet<DaemonSetting> Locked)
{
    /// <summary>Applies where the daemon reports <see cref="DaemonCapability.EngineConfiguration"/>.</summary>
    public ProxySettings Proxy { get; init; } = ProxySettings.Default;

    /// <summary>See <see cref="DaemonSetting.TrustSystemCertificates"/>.</summary>
    public bool TrustSystemCertificates { get; init; } = true;

    /// <summary>See <see cref="DaemonSetting.RegistryMirrors"/>, in the order the engine tries them.</summary>
    public IReadOnlyList<string> RegistryMirrors { get; init; } = [];

    /// <summary>See <see cref="DaemonSetting.DaemonJson"/>; <c>{}</c> holds no settings.</summary>
    public string DaemonJson { get; init; } = "{}";

    /// <summary>Whether an administrator fixes <paramref name="setting"/>.</summary>
    public bool IsLocked(DaemonSetting setting)
    {
        return Locked.Contains(setting);
    }
}

/// <summary>The settings to change; those left absent stay as they are.</summary>
public sealed record DaemonSettingsEdit
{
    /// <summary>See <see cref="DaemonSetting.StartEngineWithDaemon"/>.</summary>
    public bool? StartEngineWithDaemon { get; init; }

    /// <summary>See <see cref="DaemonSetting.ServeDefaultPipe"/>.</summary>
    public bool? ServeDefaultPipe { get; init; }

    /// <summary>See <see cref="DaemonSetting.ProxyMode"/>.</summary>
    public ProxyMode? ProxyMode { get; init; }

    /// <summary>See <see cref="DaemonSetting.ProxyHttp"/>.</summary>
    public string? ProxyHttp { get; init; }

    /// <summary>See <see cref="DaemonSetting.ProxyHttps"/>.</summary>
    public string? ProxyHttps { get; init; }

    /// <summary>See <see cref="DaemonSetting.ProxyExclude"/>.</summary>
    public string? ProxyExclude { get; init; }

    /// <summary>See <see cref="DaemonSetting.PassProxyToContainers"/>.</summary>
    public bool? PassProxyToContainers { get; init; }

    /// <summary>See <see cref="DaemonSetting.TrustSystemCertificates"/>.</summary>
    public bool? TrustSystemCertificates { get; init; }

    /// <summary>See <see cref="DaemonSetting.RegistryMirrors"/>.</summary>
    public IReadOnlyList<string>? RegistryMirrors { get; init; }

    /// <summary>See <see cref="DaemonSetting.DaemonJson"/>.</summary>
    public string? DaemonJson { get; init; }

    /// <summary>The settings this edit changes.</summary>
    public IEnumerable<DaemonSetting> Settings()
    {
        (bool Named, DaemonSetting Setting)[] fields = [
            (StartEngineWithDaemon is not null, DaemonSetting.StartEngineWithDaemon),
            (ServeDefaultPipe is not null, DaemonSetting.ServeDefaultPipe),
            (ProxyMode is not null, DaemonSetting.ProxyMode),
            (ProxyHttp is not null, DaemonSetting.ProxyHttp),
            (ProxyHttps is not null, DaemonSetting.ProxyHttps),
            (ProxyExclude is not null, DaemonSetting.ProxyExclude),
            (PassProxyToContainers is not null, DaemonSetting.PassProxyToContainers),
            (TrustSystemCertificates is not null, DaemonSetting.TrustSystemCertificates),
            (RegistryMirrors is not null, DaemonSetting.RegistryMirrors),
            (DaemonJson is not null, DaemonSetting.DaemonJson),
        ];

        return fields
            .Where(field => field.Named)
            .Select(field => field.Setting);
    }
}

/// <summary>The settings after a change.</summary>
/// <param name="Settings">The settings in effect afterwards.</param>
/// <param name="RestartRequired">Changed settings the running engine applies only at its next start.</param>
public sealed record DaemonSettingsChange(DaemonSettings Settings, IReadOnlySet<DaemonSetting> RestartRequired);

/// <summary>What became of a <c>daemon.json</c> the user saved.</summary>
/// <param name="Result">The settings afterwards; a failure with code <see cref="IDaemonSettings.InvalidValueCode"/>
/// whose message reads <c>engine.daemon_json: …</c> where the daemon or the engine refused the file.</param>
public sealed record DaemonJsonUpdate(Result<DaemonSettingsChange> Result)
{
    /// <summary>Top-level keys the engine does not know, sorted; empty unless they are why it refused the file.
    /// </summary>
    public IReadOnlyList<UnknownDaemonKey> UnknownKeys { get; init; } = [];
}

/// <summary>A top-level <c>daemon.json</c> key the engine does not know.</summary>
/// <param name="Key">The key as written.</param>
/// <param name="Suggestion">A key the engine takes with the same value, such as <c>log-opts</c> for <c>log-opt</c>;
/// empty when none is close.</param>
public sealed record UnknownDaemonKey(string Key, string Suggestion);

/// <summary>The daemon's names for its settings, which its messages and an administrator's policy use.</summary>
public static class DaemonSettingPaths
{
    private static readonly (DaemonSetting Setting, string Path)[] _paths = [
        (DaemonSetting.StartEngineWithDaemon, "engine.start_with_daemon"),
        (DaemonSetting.ServeDefaultPipe, "engine.serve_default_pipe"),
        (DaemonSetting.ProxyMode, "proxy.mode"),
        (DaemonSetting.ProxyHttp, "proxy.http"),
        (DaemonSetting.ProxyHttps, "proxy.https"),
        (DaemonSetting.ProxyExclude, "proxy.exclude"),
        (DaemonSetting.TrustSystemCertificates, "certificates.trust_system"),
        (DaemonSetting.RegistryMirrors, "registry.mirrors"),
        (DaemonSetting.DaemonJson, "engine.daemon_json"),
        (DaemonSetting.PassProxyToContainers, "proxy.pass_to_containers"),
    ];

    /// <summary>Such as <c>proxy.http</c>.</summary>
    public static string Of(DaemonSetting setting)
    {
        return _paths.First(entry => entry.Setting == setting).Path;
    }

    /// <summary>The setting at <paramref name="path"/>; absent for a path this client does not know.</summary>
    public static DaemonSetting? At(string path)
    {
        return _paths.FirstOrDefault(entry => entry.Path == path) is { Path: not null } entry ? entry.Setting : null;
    }

    /// <summary>The setting a refusal names first, and why the daemon refused it.</summary>
    /// <param name="message">Such as <c>proxy.http: "x" is not a URL</c>.</param>
    /// <returns>The setting and the reason; absent where the message names no setting this client knows.</returns>
    public static (DaemonSetting Setting, string Reason)? Refused(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var colon = message.IndexOf(": ", StringComparison.Ordinal);

        if (colon <= 0 || At(message[..colon]) is not { } setting)
        {
            return null;
        }

        return (setting, message[(colon + 2)..]);
    }
}
