using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Contr.Sdk;
using Contr.Sdk.Common.V1;
using Contr.Sdk.Daemon.V1;
using Contr.Sdk.Engine.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EngineState = Contr.UI.Contracts.Services.Daemon.EngineState;
using WireEngineState = Contr.Sdk.Engine.V1.EngineState;

namespace Contr.Daemon.UI;

/// <summary>
/// Connects to the daemon or starts it, follows the engine state, and reconnects after the
/// connection breaks.
/// </summary>
/// <remarks>
/// Every connection attempt is bounded: on Windows the SDK's pipe connect waits forever for a pipe
/// that does not exist, so an unbounded attempt never learns that no daemon is running.
/// </remarks>
public sealed partial class DaemonConnection : IDaemonConnection, IDaemonClient, IDisposable
{
    private const string _clientName = "contr-windows";
    private const string _startEngineKey = "StartEngineOnConnect";

    private static readonly TimeSpan _attemptTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _startTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _describeRetryDelay = TimeSpan.FromSeconds(2);

    private readonly DaemonLauncher _launcher;
    private readonly ILocalSettingsService _settings;
    private readonly ILogger<DaemonConnection> _logger;
    private readonly BehaviorSubject<DaemonStatus> _status = new(DaemonStatus.Connecting);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private ContrClient? _client;
    private int _retryScheduled;
    private int _disposed;
    private volatile bool _startEngine;

    public DaemonConnection(
        DaemonLauncher launcher,
        IOptions<DaemonOptions> options,
        ILocalSettingsService settings,
        ILogger<DaemonConnection> logger)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);
        _launcher = launcher;
        _settings = settings;
        _logger = logger;
        _startEngine = settings.Read<bool?>(_startEngineKey) ?? options.Value.StartEngine;
    }

    public DaemonStatus Status => _status.Value;

    public DaemonIdentity? Identity { get; private set; }

    public IObservable<DaemonStatus> StatusChanges => _status.AsObservable();

    public bool StartsEngineOnConnect
    {
        get => _startEngine;
        set {
            _startEngine = value;
            _settings.Save(_startEngineKey, value);
        }
    }

    public async Task<Result> ConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await GetAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (ContrException exception)
        {
            return Result.Failure(exception.ToError());
        }
    }

    public async ValueTask<ContrClient> GetAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _client) is { } connected)
        {
            return connected;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _client ?? await OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<Result> StartEngineAsync(CancellationToken cancellationToken)
    {
        return DrainAsync(
            (client, token) => client.Engine.Start(new StartRequest(), cancellationToken: token),
            cancellationToken);
    }

    public Task<Result> StartEngineAsync(IProgress<EngineProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return DrainAsync(
            (client, token) => client.Engine.Start(new StartRequest(), cancellationToken: token),
            cancellationToken,
            response => response.EventCase == StartResponse.EventOneofCase.Progress
                ? new EngineProgress(response.Progress.Message, Fraction(response.Progress))
                : null,
            progress);
    }

    public Task<Result> StopEngineAsync(CancellationToken cancellationToken)
    {
        return DrainAsync(
            (client, token) => client.Engine.Stop(new StopRequest(), cancellationToken: token),
            cancellationToken);
    }

    public Task<Result> RestartEngineAsync(CancellationToken cancellationToken)
    {
        return DrainAsync(
            (client, token) => client.Engine.Restart(new RestartRequest(), cancellationToken: token),
            cancellationToken);
    }

    /// <remarks>
    /// Safe to call more than once: the container disposes this instance once per interface it is
    /// registered under.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _lifetime.Cancel();
        Interlocked.Exchange(ref _client, null)?.Dispose();
        _status.OnCompleted();
        _status.Dispose();
        _gate.Dispose();
        _lifetime.Dispose();
    }

    private static EngineState Map(WireEngineState state)
    {
        return state switch {
            WireEngineState.Uninitialized => EngineState.Uninitialized,
            WireEngineState.Provisioning => EngineState.Provisioning,
            WireEngineState.Stopped => EngineState.Stopped,
            WireEngineState.Starting => EngineState.Starting,
            WireEngineState.Running => EngineState.Running,
            WireEngineState.Stopping => EngineState.Stopping,
            WireEngineState.Updating => EngineState.Updating,
            WireEngineState.Resetting => EngineState.Resetting,
            WireEngineState.Failed => EngineState.Failed,
            _ => EngineState.Unknown,
        };
    }

    private static HashSet<DaemonCapability> Capabilities(IEnumerable<Capability> capabilities)
    {
        var known = new HashSet<DaemonCapability>();
        foreach (var capability in capabilities)
        {
            DaemonCapability? mapped = capability switch {
                Capability.Compose => DaemonCapability.Compose,
                Capability.Kubernetes => DaemonCapability.Kubernetes,
                Capability.ResourceLimits => DaemonCapability.ResourceLimits,
                Capability.WslIntegration => DaemonCapability.WslIntegration,
                Capability.HostPortForwarding => DaemonCapability.HostPortForwarding,
                _ => null,
            };

            if (mapped is { } value)
            {
                known.Add(value);
            }
        }

        return known;
    }

    private static string Describe(BackendKind backend)
    {
        return backend switch {
            BackendKind.Host => "Host",
            BackendKind.Wsl => "WSL 2",
            BackendKind.Qemu => "QEMU",
            BackendKind.Fake => "Fake",
            _ => "Unknown",
        };
    }

    private async Task<ContrClient> OpenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await TryConnectAsync(cancellationToken).ConfigureAwait(false)
                ?? await StartAndConnectAsync(cancellationToken).ConfigureAwait(false);
            Attach(client);

            return client;
        }
        catch (ContrException exception)
        {
            LogUnavailable(exception.Message);
            Publish(new DaemonStatus(DaemonConnectionState.Unavailable, EngineState.Unknown, exception.Message));
            ScheduleRetry();
            throw;
        }
    }

    /// <summary>One bounded attempt; <see langword="null"/> when nothing answers in time.</summary>
    private static async Task<ContrClient?> TryConnectAsync(CancellationToken cancellationToken)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(_attemptTimeout);
        try
        {
            return await ContrClient
                .ConnectAsync(endpoint: null, _clientName, attempt.Token)
                .ConfigureAwait(false);
        }
        catch (DaemonUnavailableException)
        {
            return null;
        }
        catch (ContrException) when (attempt.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (ContrException exception) when (cancellationToken.IsCancellationRequested)
        {
            // The SDK reports a cancelled handshake as a failed call; the caller only stopped waiting.
            throw new OperationCanceledException(exception.Message, exception, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Starts the daemon and waits for it to answer.</summary>
    /// <remarks>
    /// Only for the per-user default endpoint: a daemon started here listens there, so an endpoint
    /// from <c>CONTR_HOST</c> or a discovery file belongs to a daemon somebody else runs.
    /// </remarks>
    private async Task<ContrClient> StartAndConnectAsync(CancellationToken cancellationToken)
    {
        var endpoint = ContrEndpoint.Resolve();

        if (endpoint != ContrEndpoint.DefaultForUser())
        {
            throw new DaemonUnavailableException($"No daemon answers at {endpoint}.", null);
        }

        LogStarting(_launcher.Executable);
        _launcher.Start();

        var deadline = DateTime.UtcNow + _startTimeout;
        while (true)
        {
            await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);

            if (await TryConnectAsync(cancellationToken).ConfigureAwait(false) is { } client)
            {
                return client;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new DaemonUnavailableException(
                    $"{_launcher.Executable} did not answer within {_startTimeout.TotalSeconds:0} seconds.", null);
            }
        }
    }

    private void Attach(ContrClient client)
    {
        var handshake = client.Handshake;
        Identity = new DaemonIdentity(
            client.Endpoint.ToString(),
            handshake.DaemonVersion,
            handshake.ApiRevision,
            Describe(handshake.BackendKind)) {
            Capabilities = Capabilities(handshake.Capabilities),
        };
        Volatile.Write(ref _client, client);
        LogConnected(Identity.Endpoint, Identity.Version, Identity.Backend);
        Publish(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Unknown, string.Empty));

        _ = FollowEngineAsync(client, _lifetime.Token);
    }

    private async Task FollowEngineAsync(ContrClient client, CancellationToken cancellationToken)
    {
        try
        {
            var call = client.Engine.WatchState(new WatchStateRequest(), cancellationToken: cancellationToken);
            var first = true;
            await foreach (var update in ContrClient
                .ReadAllAsync(call, cancellationToken)
                .ConfigureAwait(false))
            {
                var state = Map(update.State);
                var failed = update.State == WireEngineState.Failed;
                var problem = failed ? DaemonErrorMapping.Describe(update.FailureReason) : string.Empty;
                var code = failed && update.FailureReason != ErrorReason.Unspecified
                    ? ReasonCode(update.FailureReason)
                    : string.Empty;
                var details = state == EngineState.Running
                    ? await DescribeEngineAsync(client, cancellationToken).ConfigureAwait(false)
                    : null;
                PublishEngine(state, problem, details, code);

                if (state == EngineState.Running && details is null)
                {
                    _ = DescribeLaterAsync(client, cancellationToken);
                }

                if (first && _startEngine
                    && update.State is WireEngineState.Stopped or WireEngineState.Uninitialized)
                {
                    _ = StartStoppedEngineAsync(cancellationToken);
                }

                first = false;
            }
        }
        catch (ContrException exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogDisconnected(exception.Message);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested
            && exception is ContrException or OperationCanceledException or ObjectDisposedException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _client, null, client) == client)
        {
            Identity = null;
            client.Dispose();
        }

        Publish(new DaemonStatus(
            DaemonConnectionState.Unavailable, EngineState.Unknown, "The connection to the daemon closed."));
        ScheduleRetry();
    }

    /// <summary>The engine's version and Docker context; <see langword="null"/> when the daemon cannot say.</summary>
    private static async Task<EngineDetails?> DescribeEngineAsync(
        ContrClient client, CancellationToken cancellationToken)
    {
        try
        {
            var info = await client.Daemon
                .GetInfoAsync(new GetInfoRequest(), cancellationToken: cancellationToken)
                .ResponseAsync
                .ConfigureAwait(false);

            return info.Engine is { } engine ? new EngineDetails(engine.Version, engine.DockerContext) : null;
        }
        catch (RpcException)
        {
            return null;
        }
    }

    /// <summary>Asks again for an engine that reported running before it could describe itself.</summary>
    private async Task DescribeLaterAsync(ContrClient client, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_describeRetryDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var details = await DescribeEngineAsync(client, cancellationToken).ConfigureAwait(false);
        var current = _status.Value;

        if (details is not null && current is { Engine: EngineState.Running, Details: null })
        {
            Publish(current with { Details = details });
        }
    }

    /// <summary>Reads an engine operation's progress stream to its end; the state arrives through WatchState.</summary>
    private async Task<Result> DrainAsync<TResponse>(
        Func<ContrClient, CancellationToken, AsyncServerStreamingCall<TResponse>> open,
        CancellationToken cancellationToken,
        Func<TResponse, EngineProgress?>? step = null,
        IProgress<EngineProgress>? progress = null)
    {
        return await this
            .RunAsync(
                async (client, token) => {
                    using var call = open(client, token);
                    await foreach (var response in call.ResponseStream.ReadAllAsync(token).ConfigureAwait(false))
                    {
                        LogEngineProgress($"{response}");

                        if (step?.Invoke(response) is { } reported)
                        {
                            progress?.Report(reported);
                        }
                    }
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static double? Fraction(Contr.Sdk.Common.V1.Progress progress)
    {
        return progress.HasFraction ? progress.Fraction : null;
    }

    private async Task StartStoppedEngineAsync(CancellationToken cancellationToken)
    {
        LogStartingEngine();
        var started = await StartEngineAsync(cancellationToken).ConfigureAwait(false);

        if (started.IsFailure)
        {
            LogEngineNotStarted(started.Error.Message);
        }
    }

    private void ScheduleRetry()
    {
        if (Interlocked.Exchange(ref _retryScheduled, 1) == 1)
        {
            return;
        }

        _ = RetryAsync(_lifetime.Token);
    }

    private async Task RetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_retryInterval, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _retryScheduled, 0);
            await GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ContrException)
        {
            // OpenAsync has published the failure and scheduled the next attempt.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The connection is being disposed.
        }
    }

    private void PublishEngine(EngineState state, string problem, EngineDetails? details, string code)
    {
        var current = _status.Value;
        var since = current.Connection == DaemonConnectionState.Connected && current.Engine == state
            ? current.Since
            : DateTimeOffset.Now;
        Publish(new DaemonStatus(DaemonConnectionState.Connected, state, problem) {
            Since = since,
            Details = details,
            ProblemCode = code,
        });
    }

    /// <summary>The reason as the proto names it without its prefix, such as <c>VIRTUALIZATION_DISABLED</c>.</summary>
    private static string ReasonCode(ErrorReason reason)
    {
        var name = reason.ToString();
        var code = new StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            if (index > 0 && char.IsUpper(name[index]))
            {
                code.Append('_');
            }

            code.Append(char.ToUpperInvariant(name[index]));
        }

        return code.ToString();
    }

    private void Publish(DaemonStatus status)
    {
        if (!_lifetime.IsCancellationRequested && _status.Value != status)
        {
            _status.OnNext(status);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting the daemon: {Executable}")]
    private partial void LogStarting(string executable);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to daemon {Version} ({Backend}) at {Endpoint}")]
    private partial void LogConnected(string endpoint, string version, string backend);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Engine operation: {Progress}")]
    private partial void LogEngineProgress(string progress);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting the stopped engine")]
    private partial void LogStartingEngine();

    [LoggerMessage(Level = LogLevel.Warning, Message = "The engine did not start: {Reason}")]
    private partial void LogEngineNotStarted(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The daemon is unavailable: {Reason}")]
    private partial void LogUnavailable(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The daemon connection closed: {Reason}")]
    private partial void LogDisconnected(string reason);
}
