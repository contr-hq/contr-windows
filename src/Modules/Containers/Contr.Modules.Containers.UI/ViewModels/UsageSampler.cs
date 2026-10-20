using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Queries.StreamContainerStats;
using Contr.Modules.Containers.Domain;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>Keeps one stats stream open per running container and shows its samples in the container's usage.</summary>
/// <remarks>Use it on the UI thread: samples resume on the captured context.</remarks>
internal sealed class UsageSampler : IDisposable
{
    private readonly ISender _sender;
    private readonly Dictionary<string, ContainerUsage> _usage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CancellationTokenSource> _streams = new(StringComparer.Ordinal);

    public UsageSampler(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
    }

    public ContainerUsage For(ContainerId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!_usage.TryGetValue(id.Value, out var usage))
        {
            usage = new ContainerUsage();
            _usage[id.Value] = usage;
        }

        return usage;
    }

    /// <summary>Samples exactly the <paramref name="running"/> containers; the others show no usage.</summary>
    public void Follow(IReadOnlyCollection<ContainerId> running)
    {
        ArgumentNullException.ThrowIfNull(running);

        var wanted = running
            .Select(id => id.Value)
            .ToHashSet(StringComparer.Ordinal);

        var gone = _streams.Keys
            .Where(id => !wanted.Contains(id))
            .ToList();

        foreach (var id in gone)
        {
            Close(id);
        }

        foreach (var id in running.Where(id => !_streams.ContainsKey(id.Value)))
        {
            var stream = new CancellationTokenSource();
            _streams[id.Value] = stream;
            _ = SampleAsync(id, For(id), stream.Token);
        }
    }

    public void StopAll()
    {
        foreach (var id in _streams.Keys.ToList())
        {
            Close(id);
        }
    }

    public void Dispose()
    {
        StopAll();
    }

    private void Close(string id)
    {
        if (_streams.Remove(id, out var stream))
        {
            stream.Cancel();
            stream.Dispose();
        }

        if (_usage.TryGetValue(id, out var usage))
        {
            usage.Clear();
        }
    }

    private async Task SampleAsync(ContainerId id, ContainerUsage usage, CancellationToken cancellationToken)
    {
        try
        {
            var events = await _sender.Send(new StreamContainerStatsQuery(id), cancellationToken);
            await foreach (var sample in events.WithCancellation(cancellationToken))
            {
                if (sample is ContainerStatsEvent.Sample { Stats: var stats })
                {
                    usage.Show(stats);
                }
                else
                {
                    usage.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The container stopped or the page closed.
        }
    }
}
