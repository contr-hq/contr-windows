using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Contr.Application.Abstractions.Watching;

namespace Contr.UI.Common.Lists;

/// <summary>Sorted rows of a watched list, kept current by applying its <see cref="ListChange{TItem}"/>s.</summary>
/// <typeparam name="TItem">The item the stream carries.</typeparam>
/// <typeparam name="TRow">The immutable row a view shows for one item.</typeparam>
public sealed class LiveCollection<TItem, TRow>
    where TRow : class
{
    private readonly Func<TItem, TRow> _toRow;
    private readonly Func<TRow, string> _key;
    private readonly Comparison<TRow> _order;

    public LiveCollection(Func<TItem, TRow> toRow, Func<TRow, string> key, Comparison<TRow> order)
    {
        ArgumentNullException.ThrowIfNull(toRow);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(order);
        _toRow = toRow;
        _key = key;
        _order = order;
    }

    public ObservableCollection<TRow> Rows { get; } = [];

    /// <summary>Applies a snapshot, upsert or removal; a failure leaves the rows as they are.</summary>
    public void Apply(ListChange<TItem> change)
    {
        switch (change)
        {
            case ListChange<TItem>.Snapshot snapshot:
                Replace(snapshot.Items);
                break;
            case ListChange<TItem>.Upserted upserted:
                Upsert(_toRow(upserted.Item));
                break;
            case ListChange<TItem>.Removed removed:
                Remove(removed.Key);
                break;
        }
    }

    private void Replace(IReadOnlyList<TItem> items)
    {
        var rows = items
            .Select(_toRow)
            .ToList();
        rows.Sort(_order);

        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }
    }

    private void Upsert(TRow row)
    {
        var existing = IndexOf(_key(row));

        if (existing >= 0 && FitsAt(existing, row))
        {
            Rows[existing] = row;

            return;
        }

        if (existing >= 0)
        {
            Rows.RemoveAt(existing);
        }

        var index = 0;
        while (index < Rows.Count && _order(Rows[index], row) <= 0)
        {
            index++;
        }

        Rows.Insert(index, row);
    }

    private void Remove(string key)
    {
        var index = IndexOf(key);

        if (index >= 0)
        {
            Rows.RemoveAt(index);
        }
    }

    private int IndexOf(string key)
    {
        for (var index = 0; index < Rows.Count; index++)
        {
            if (string.Equals(_key(Rows[index]), key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Whether <paramref name="row"/> keeps the order in place of row <paramref name="index"/>.</summary>
    private bool FitsAt(int index, TRow row)
    {
        var afterPrevious = index == 0 || _order(Rows[index - 1], row) <= 0;
        var beforeNext = index == Rows.Count - 1 || _order(row, Rows[index + 1]) <= 0;

        return afterPrevious && beforeNext;
    }
}
