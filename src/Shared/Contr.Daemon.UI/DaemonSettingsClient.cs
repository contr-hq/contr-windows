using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Daemon;
using Contr.Sdk;
using Contr.Sdk.Settings.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using DaemonJsonRejection = Contr.Sdk.Settings.V1.DaemonJsonRejection;
using ProxyMode = Contr.UI.Contracts.Services.Daemon.ProxyMode;
using ProxySettings = Contr.UI.Contracts.Services.Daemon.ProxySettings;
using UnknownDaemonKey = Contr.UI.Contracts.Services.Daemon.UnknownDaemonKey;
using WireMode = Contr.Sdk.Settings.V1.ProxyMode;
using WireProxy = Contr.Sdk.Settings.V1.ProxySettings;
using WireSettings = Contr.Sdk.Settings.V1.Settings;

namespace Contr.Daemon.UI;

/// <summary><see cref="IDaemonSettings"/> over SettingsService.</summary>
public sealed partial class DaemonSettingsClient : IDaemonSettings
{
    private readonly IDaemonClient _daemon;
    private readonly ILogger<DaemonSettingsClient> _logger;
    private readonly IObservable<DaemonSettings?> _changes;

    public DaemonSettingsClient(
        IDaemonConnection connection, IDaemonClient daemon, ILogger<DaemonSettingsClient> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(logger);
        _daemon = daemon;
        _logger = logger;
        _changes = connection.StatusChanges
            .Select(status => status.Connection == DaemonConnectionState.Connected)
            .DistinctUntilChanged()
            .Select(connected => connected ? Watch() : Observable.Return<DaemonSettings?>(null))
            .Switch()
            .Replay(1)
            .RefCount();
    }

    public IObservable<DaemonSettings?> Changes => _changes;

    public async Task<Result<DaemonSettingsChange>> UpdateAsync(
        DaemonSettingsEdit edit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);

        var request = ToRequest(edit);
        var result = await _daemon
            .CallAsync(
                async (client, token) => await client.Settings
                    .UpdateSettingsAsync(request, cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<DaemonSettingsChange>(Refusal(result.Error))
            : Result.Success(ToChange(result.Value.State, result.Value.RestartRequired));
    }

    public async Task<DaemonJsonUpdate> UpdateDaemonJsonAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        var request = ToRequest(new DaemonSettingsEdit { DaemonJson = text });
        IReadOnlyList<UnknownDaemonKey> unknown = [];
        var result = await _daemon
            .CallAsync(
                async (client, token) => {
                    try
                    {
                        return await client.Settings
                            .UpdateSettingsAsync(request, cancellationToken: token)
                            .ResponseAsync
                            .ConfigureAwait(false);
                    }
                    catch (RpcException exception)
                    {
                        unknown = UnknownKeys(exception);
                        throw;
                    }
                },
                cancellationToken)
            .ConfigureAwait(false);

        return new DaemonJsonUpdate(result.IsFailure
            ? Result.Failure<DaemonSettingsChange>(Refusal(result.Error))
            : Result.Success(ToChange(result.Value.State, result.Value.RestartRequired))) {
            UnknownKeys = unknown,
        };
    }

    /// <summary>The keys a refused <c>daemon.json</c> named that the engine does not know; empty without them.
    /// </summary>
    public static IReadOnlyList<UnknownDaemonKey> UnknownKeys(RpcException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var rejection = exception
            .GetRpcStatus()?
            .Details
            .FirstOrDefault(detail => detail.Is(DaemonJsonRejection.Descriptor))?
            .Unpack<DaemonJsonRejection>();

        return rejection is null
            ? []
            : [.. rejection.UnknownKeys.Select(key => new UnknownDaemonKey(key.Key, key.Suggestion))];
    }

    public async Task<Result<DaemonSettingsChange>> ResetAsync(CancellationToken cancellationToken)
    {
        var result = await _daemon
            .CallAsync(
                async (client, token) => await client.Settings
                    .ResetSettingsAsync(new ResetSettingsRequest(), cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<DaemonSettingsChange>(result.Error)
            : Result.Success(ToChange(result.Value.State, result.Value.RestartRequired));
    }

    /// <remarks>Paths this client does not know are left out.</remarks>
    public static DaemonSettings ToSettings(SettingsState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var engine = state.Settings?.Engine ?? new EngineSettings();
        var proxy = state.Settings?.Proxy;

        return new DaemonSettings(engine.StartWithDaemon, engine.ServeDefaultPipe, Known(state.LockedPaths)) {
            Proxy = proxy is null
                ? ProxySettings.Default
                : new ProxySettings(ToMode(proxy.Mode), proxy.Http, proxy.Https, proxy.Exclude) {
                    PassToContainers = proxy.PassToContainers,
                },
            TrustSystemCertificates = state.Settings?.Certificates?.TrustSystem ?? true,
            RegistryMirrors = state.Settings?.Registry is { } registry ? [.. registry.Mirrors] : [],
            DaemonJson = engine.DaemonJson.Length > 0 ? engine.DaemonJson : "{}",
        };
    }

    public static DaemonSettingsChange ToChange(SettingsState? state, IEnumerable<string> restartRequired)
    {
        return new DaemonSettingsChange(ToSettings(state ?? new SettingsState()), Known(restartRequired));
    }

    /// <summary>The request that changes what <paramref name="edit"/> names, and only that.</summary>
    public static UpdateSettingsRequest ToRequest(DaemonSettingsEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        var settings = new WireSettings {
            Engine = new EngineSettings {
                StartWithDaemon = edit.StartEngineWithDaemon ?? false,
                ServeDefaultPipe = edit.ServeDefaultPipe ?? false,
                DaemonJson = edit.DaemonJson ?? string.Empty,
            },
            Proxy = new WireProxy {
                Mode = edit.ProxyMode is { } mode ? ToWire(mode) : WireMode.Unspecified,
                Http = edit.ProxyHttp ?? string.Empty,
                Https = edit.ProxyHttps ?? string.Empty,
                Exclude = edit.ProxyExclude ?? string.Empty,
                PassToContainers = edit.PassProxyToContainers ?? false,
            },
            Certificates = new CertificateSettings { TrustSystem = edit.TrustSystemCertificates ?? false },
            Registry = new RegistrySettings { Mirrors = { edit.RegistryMirrors ?? [] } },
        };
        var mask = new FieldMask();
        mask.Paths.AddRange(edit
            .Settings()
            .Select(DaemonSettingPaths.Of));

        return new UpdateSettingsRequest { Settings = settings, UpdateMask = mask };
    }

    /// <summary>A refusal that names a setting first gets <see cref="IDaemonSettings.InvalidValueCode"/>.</summary>
    public static Error Refusal(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Code == DaemonErrors.FailedCode && DaemonSettingPaths.Refused(error.Message) is not null
            ? error with { Code = IDaemonSettings.InvalidValueCode }
            : error;
    }

    private static ProxyMode ToMode(WireMode mode)
    {
        return mode switch {
            WireMode.Manual => ProxyMode.Manual,
            WireMode.None => ProxyMode.None,
            _ => ProxyMode.System,
        };
    }

    private static WireMode ToWire(ProxyMode mode)
    {
        return mode switch {
            ProxyMode.Manual => WireMode.Manual,
            ProxyMode.None => WireMode.None,
            _ => WireMode.System,
        };
    }

    private static HashSet<DaemonSetting> Known(IEnumerable<string> paths)
    {
        return [.. paths
            .Select(DaemonSettingPaths.At)
            .OfType<DaemonSetting>()];
    }

    /// <summary>The daemon's settings until the stream ends; then absent, as from a daemon that keeps none.</summary>
    private IObservable<DaemonSettings?> Watch()
    {
        return Observable.Create<DaemonSettings?>(async (observer, cancellationToken) => {
            try
            {
                var client = await _daemon.GetAsync(cancellationToken).ConfigureAwait(false);
                using var call = client.Settings.WatchSettings(
                    new WatchSettingsRequest(), cancellationToken: cancellationToken);
                var responses = call.ResponseStream.ReadAllAsync(cancellationToken);
                await foreach (var response in responses.ConfigureAwait(false))
                {
                    observer.OnNext(ToSettings(response.State ?? new SettingsState()));
                }
            }
            catch (RpcException exception) when (exception.StatusCode != StatusCode.Cancelled)
            {
                LogWatchEnded(exception.Status.Detail);
            }
            catch (ContrException exception)
            {
                LogWatchEnded(exception.Message);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                observer.OnNext(null);
            }
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The settings stream ended: {Reason}")]
    private partial void LogWatchEnded(string reason);
}
