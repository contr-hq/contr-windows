using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Sdk;
using Contr.Sdk.Settings.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using WireSettings = Contr.Sdk.Settings.V1.Settings;

namespace Contr.Daemon.UI;

/// <summary><see cref="IDaemonSettings"/> over SettingsService.</summary>
public sealed partial class DaemonSettingsClient : IDaemonSettings
{
    private const string _startEngineWithDaemonPath = "engine.start_with_daemon";
    private const string _serveDefaultPipePath = "engine.serve_default_pipe";

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

    public async Task<Result<DaemonSettingsChange>> SetAsync(
        DaemonSetting setting, bool value, CancellationToken cancellationToken)
    {
        var request = new UpdateSettingsRequest {
            Settings = new WireSettings {
                Engine = setting switch {
                    DaemonSetting.StartEngineWithDaemon => new EngineSettings { StartWithDaemon = value },
                    _ => new EngineSettings { ServeDefaultPipe = value },
                },
            },
            UpdateMask = new FieldMask { Paths = { PathOf(setting) } },
        };
        var result = await _daemon
            .CallAsync(
                async (client, token) => await client.Settings
                    .UpdateSettingsAsync(request, cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<DaemonSettingsChange>(result.Error)
            : Result.Success(ToChange(result.Value.State, result.Value.RestartRequired));
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

        return new DaemonSettings(engine.StartWithDaemon, engine.ServeDefaultPipe, Known(state.LockedPaths));
    }

    public static DaemonSettingsChange ToChange(SettingsState? state, IEnumerable<string> restartRequired)
    {
        return new DaemonSettingsChange(ToSettings(state ?? new SettingsState()), Known(restartRequired));
    }

    private static HashSet<DaemonSetting> Known(IEnumerable<string> paths)
    {
        return [.. paths
            .Select(SettingOf)
            .OfType<DaemonSetting>()];
    }

    private static DaemonSetting? SettingOf(string path)
    {
        return path switch {
            _startEngineWithDaemonPath => DaemonSetting.StartEngineWithDaemon,
            _serveDefaultPipePath => DaemonSetting.ServeDefaultPipe,
            _ => null,
        };
    }

    private static string PathOf(DaemonSetting setting)
    {
        return setting switch {
            DaemonSetting.StartEngineWithDaemon => _startEngineWithDaemonPath,
            _ => _serveDefaultPipePath,
        };
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
