using System;
using System.Collections.Specialized;
using System.Linq;
using Contr.Application.Abstractions.Watching;
using Contr.SharedKernel;
using Contr.UI.Common.Lists;
using Xunit;

namespace Contr.App.Tests.Common;

public sealed class LiveCollectionTests
{
    private readonly LiveCollection<Item, Item> _list = new(
        item => item,
        item => item.Key,
        (left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));

    [Fact]
    public void Snapshot_ReplacesTheRowsInOrder()
    {
        _list.Apply(new ListChange<Item>.Upserted(new Item("stale", "z")));

        _list.Apply(new ListChange<Item>.Snapshot([new Item("2", "b"), new Item("1", "a")]));

        Assert.Equal(["a", "b"], Names());
    }

    [Fact]
    public void Upserted_InsertsANewItemInOrder()
    {
        _list.Apply(new ListChange<Item>.Snapshot([new Item("1", "a"), new Item("3", "c")]));

        _list.Apply(new ListChange<Item>.Upserted(new Item("2", "b")));

        Assert.Equal(["a", "b", "c"], Names());
    }

    [Fact]
    public void Upserted_ReplacesAnItemInPlace()
    {
        _list.Apply(new ListChange<Item>.Snapshot([new Item("1", "a"), new Item("2", "b")]));
        var replaced = 0;
        _list.Rows.CollectionChanged += (_, e) => {
            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                replaced++;
            }
        };

        _list.Apply(new ListChange<Item>.Upserted(new Item("2", "b", "changed")));

        Assert.Equal(1, replaced);
        Assert.Equal("changed", _list.Rows[1].Detail);
    }

    [Fact]
    public void Upserted_MovesAnItemWhoseOrderChanged()
    {
        _list.Apply(new ListChange<Item>.Snapshot([new Item("1", "a"), new Item("2", "b")]));

        _list.Apply(new ListChange<Item>.Upserted(new Item("1", "c")));

        Assert.Equal(["b", "c"], Names());
    }

    [Fact]
    public void Removed_DropsTheItemWithThatKey()
    {
        _list.Apply(new ListChange<Item>.Snapshot([new Item("1", "a"), new Item("2", "b")]));

        _list.Apply(new ListChange<Item>.Removed("1"));
        _list.Apply(new ListChange<Item>.Removed("unknown"));

        Assert.Equal(["b"], Names());
    }

    [Fact]
    public void Failed_LeavesTheRowsAsTheyAre()
    {
        _list.Apply(new ListChange<Item>.Snapshot([new Item("1", "a")]));

        _list.Apply(new ListChange<Item>.Failed(new Error("Daemon.Unavailable", "gone")));

        Assert.Equal(["a"], Names());
    }

    private string[] Names()
    {
        return [.. _list.Rows.Select(row => row.Name)];
    }

    private sealed record Item(string Key, string Name, string Detail = "");
}
