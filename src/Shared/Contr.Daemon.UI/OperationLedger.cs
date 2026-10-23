using System;
using System.Collections.Generic;
using System.Linq;
using Contr.Sdk.Operations.V1;
using Contr.UI.Contracts.Services.Daemon;
using WatchEvent = Contr.Sdk.Operations.V1.WatchOperationsResponse.EventOneofCase;

namespace Contr.Daemon.UI;

/// <summary>
/// The operations <c>WatchOperations</c> reported, and which of them ended since; it outlives a connection, so an
/// operation that ended while the client reconnected still counts as ended.
/// </summary>
/// <remarks>Not thread-safe; one watch at a time applies to it.</remarks>
public sealed class OperationLedger
{
    private readonly Dictionary<string, DaemonOperation> _operations = new(StringComparer.Ordinal);
    private readonly HashSet<string> _running = new(StringComparer.Ordinal);

    /// <summary>Every operation, newest first.</summary>
    public IReadOnlyList<DaemonOperation> Operations => [.. _operations.Values
        .OrderByDescending(operation => operation.StartedAt)
        .ThenByDescending(operation => operation.Id, StringComparer.Ordinal)];

    /// <summary>Applies one event of the watch.</summary>
    /// <returns>The operations that were running and ended with it.</returns>
    public IReadOnlyList<DaemonOperation> Apply(WatchOperationsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        switch (response.EventCase)
        {
            case WatchEvent.Snapshot:
                _operations.Clear();
                List<DaemonOperation> ended = [];
                foreach (var operation in response.Snapshot.Operations.Select(DaemonOperations.ToOperation))
                {
                    _operations[operation.Id] = operation;
                    ended.AddRange(Track(operation));
                }

                // Operations of a daemon that stopped are gone; they never report an end.
                _running.IntersectWith(_operations.Keys);

                return ended;
            case WatchEvent.Upserted:
                var upserted = DaemonOperations.ToOperation(response.Upserted);
                _operations[upserted.Id] = upserted;

                return [.. Track(upserted)];
            case WatchEvent.RemovedId:
                _operations.Remove(response.RemovedId);
                _running.Remove(response.RemovedId);

                return [];
            default:
                return [];
        }
    }

    private IEnumerable<DaemonOperation> Track(DaemonOperation operation)
    {
        if (operation.IsRunning)
        {
            _running.Add(operation.Id);
        }
        else if (_running.Remove(operation.Id))
        {
            yield return operation;
        }
    }
}
