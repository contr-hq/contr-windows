using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;

namespace Contr.UI.Common.Lists;

/// <summary>A watched list read once, for callers that need the items now rather than as they change.</summary>
public static class ListSnapshot
{
    /// <summary>The first complete list the watch sends; empty when the stream fails or ends before one.</summary>
    /// <param name="open">Opens the stream, such as by sending the module's watch query.</param>
    /// <param name="cancellationToken">Ends the wait; the stream is closed either way.</param>
    public static async Task<IReadOnlyList<TItem>> FirstAsync<TItem>(
        Func<CancellationToken, Task<IAsyncEnumerable<ListChange<TItem>>>> open,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(open);

        using var stream = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var changes = await open(stream.Token).ConfigureAwait(false);
            await foreach (var change in changes.WithCancellation(stream.Token).ConfigureAwait(false))
            {
                switch (change)
                {
                    case ListChange<TItem>.Snapshot snapshot:
                        return snapshot.Items;
                    case ListChange<TItem>.Failed:
                        return [];
                }
            }

            return [];
        }
        finally
        {
            await stream.CancelAsync().ConfigureAwait(false);
        }
    }
}
