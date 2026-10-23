using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Contr.Sdk;
using Contr.Sdk.Operations.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using OperationState = Contr.UI.Contracts.Services.Daemon.OperationState;

namespace Contr.Daemon.UI;

/// <summary><see cref="IActivity"/> over OperationsService, following <c>WatchOperations</c> while connected.
/// </summary>
public sealed partial class ActivityClient : IActivity, IDisposable
{
    /// <summary>Code of a bundle that could not be written to its file.</summary>
    public const string SaveFailedCode = "Activity.SaveFailed";

    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly IDaemonClient _daemon;
    private readonly ILogger<ActivityClient> _logger;
    private readonly IObservable<IReadOnlyList<DaemonOperation>> _changes;
    private readonly Subject<DaemonOperation> _finished = new();
    private readonly Lock _gate = new();

    private readonly OperationLedger _ledger = new();

    private IReadOnlyList<DaemonOperation> _current = [];

    public ActivityClient(IDaemonConnection connection, IDaemonClient daemon, ILogger<ActivityClient> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(logger);
        _daemon = daemon;
        _logger = logger;
        _changes = connection.StatusChanges
            .Select(status => status.Connection == DaemonConnectionState.Connected
                && connection.Identity?.ApiRevision >= IActivity.OperationsRevision)
            .DistinctUntilChanged()
            .Select(offered => offered ? Watch() : Observable.Return<IReadOnlyList<DaemonOperation>>([]))
            .Switch()
            .Do(operations => Volatile.Write(ref _current, operations))
            .Replay(1)
            .RefCount();
    }

    public IObservable<IReadOnlyList<DaemonOperation>> Changes => _changes;

    public IReadOnlyList<DaemonOperation> Current => Volatile.Read(ref _current);

    /// <remarks>Subscribing also follows the daemon's operations, which report what finished.</remarks>
    public IObservable<DaemonOperation> Finished => Observable.Create<DaemonOperation>(observer =>
        new CompositeDisposable(_finished.Subscribe(observer), _changes.Subscribe(_ => { })));

    public Task<Result<DaemonOperation>> FollowAsync(
        string id, IProgress<EngineProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return _daemon.FollowToEndAsync(id, progress, destination: null, cancellationToken);
    }

    public Task<Result> CancelAsync(string id, CancellationToken cancellationToken)
    {
        return _daemon.CancelOperationAsync(id, cancellationToken);
    }

    public async Task<Result> SaveBundleAsync(
        string id, Stream destination, IProgress<EngineProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(progress);

        try
        {
            var ended = await _daemon
                .FollowToEndAsync(id, progress, destination, cancellationToken)
                .ConfigureAwait(false);

            if (ended.IsFailure)
            {
                return Result.Failure(ended.Error);
            }

            if (ended.Value.State != OperationState.Succeeded)
            {
                return Result.Failure(DaemonOperations.Ended(ended.Value));
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (IOException exception)
        {
            return Result.Failure(new Error(SaveFailedCode, exception.Message));
        }
    }

    public void Dispose()
    {
        _finished.OnCompleted();
        _finished.Dispose();
    }

    /// <summary>The operations until the connection ends; a broken stream opens again after a pause.</summary>
    private IObservable<IReadOnlyList<DaemonOperation>> Watch()
    {
        return Observable.Create<IReadOnlyList<DaemonOperation>>(async (observer, cancellationToken) => {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _daemon.GetAsync(cancellationToken).ConfigureAwait(false);
                    using var call = client.Operations.WatchOperations(
                        new WatchOperationsRequest(), cancellationToken: cancellationToken);
                    var responses = call.ResponseStream.ReadAllAsync(cancellationToken);
                    await foreach (var response in responses.ConfigureAwait(false))
                    {
                        IReadOnlyList<DaemonOperation> ended;
                        IReadOnlyList<DaemonOperation> operations;
                        lock (_gate)
                        {
                            ended = _ledger.Apply(response);
                            operations = _ledger.Operations;
                        }

                        observer.OnNext(operations);

                        foreach (var operation in ended)
                        {
                            _finished.OnNext(operation);
                        }
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
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The operations stream ended: {Reason}")]
    private partial void LogWatchEnded(string reason);
}
