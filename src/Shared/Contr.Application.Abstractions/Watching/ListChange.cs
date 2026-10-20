using System.Collections.Generic;
using Contr.SharedKernel;

namespace Contr.Application.Abstractions.Watching;

/// <summary>One event of a watched list: a snapshot, a change to one item, or the end of the stream.</summary>
/// <typeparam name="TItem">The listed item.</typeparam>
public abstract record ListChange<TItem>
{
    private ListChange()
    {
    }

    /// <summary>Every item; replaces whatever the receiver holds.</summary>
    /// <param name="Items">The complete list.</param>
    public sealed record Snapshot(IReadOnlyList<TItem> Items) : ListChange<TItem>;

    /// <summary>An item that appeared or changed.</summary>
    /// <param name="Item">The item's current state.</param>
    public sealed record Upserted(TItem Item) : ListChange<TItem>;

    /// <summary>An item that disappeared.</summary>
    /// <param name="Key">The key of the removed item.</param>
    public sealed record Removed(string Key) : ListChange<TItem>;

    /// <summary>The last event of a stream that broke; receivers watch again to recover.</summary>
    /// <param name="Error">Why the stream broke.</param>
    public sealed record Failed(Error Error) : ListChange<TItem>;
}
