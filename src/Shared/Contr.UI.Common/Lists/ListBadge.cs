using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;

namespace Contr.UI.Common.Lists;

/// <summary>A navigation badge that counts the items of a watched list.</summary>
public static class ListBadge
{
    /// <summary>
    /// The item count while the list streams; an empty string while nothing is listed, before the first snapshot
    /// and while the stream is broken. Each subscription opens its own stream.
    /// </summary>
    /// <param name="open">Opens the stream, such as by sending the module's watch query.</param>
    /// <param name="key">The key a removal names the item by.</param>
    /// <param name="retryDelay">The wait before watching again after the stream ends.</param>
    public static IObservable<string> Count<TItem>(
        Func<CancellationToken, Task<IAsyncEnumerable<ListChange<TItem>>>> open,
        Func<TItem, string> key,
        TimeSpan retryDelay)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(key);

        return Observable
            .Create<string>((observer, token) => FollowAsync(observer, open, key, retryDelay, token))
            .DistinctUntilChanged();
    }

    private static async Task FollowAsync<TItem>(
        IObserver<string> observer,
        Func<CancellationToken, Task<IAsyncEnumerable<ListChange<TItem>>>> open,
        Func<TItem, string> key,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var changes = await open(cancellationToken).ConfigureAwait(false);
                await foreach (var change in changes.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    var known = Apply(keys, change, key);
                    observer.OnNext(known && keys.Count > 0 ? keys.Count.ToString(CultureInfo.CurrentCulture) : "");
                }

                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>Applies one change; false when the stream broke and the count is unknown.</summary>
    private static bool Apply<TItem>(HashSet<string> keys, ListChange<TItem> change, Func<TItem, string> key)
    {
        switch (change)
        {
            case ListChange<TItem>.Snapshot snapshot:
                keys.Clear();
                foreach (var item in snapshot.Items)
                {
                    keys.Add(key(item));
                }

                return true;
            case ListChange<TItem>.Upserted upserted:
                keys.Add(key(upserted.Item));

                return true;
            case ListChange<TItem>.Removed removed:
                keys.Remove(removed.Key);

                return true;
            default:
                return false;
        }
    }
}
