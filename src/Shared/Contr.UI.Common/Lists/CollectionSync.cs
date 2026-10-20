using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Contr.UI.Common.Lists;

/// <summary>Brings a bound collection to a new content with moves and in-place replacements, so views keep their items.</summary>
public static class CollectionSync
{
    /// <summary>
    /// Makes <paramref name="target"/> equal <paramref name="desired"/>; an item with the same key and a different
    /// instance replaces the old one in place.
    /// </summary>
    public static void Sync<T>(ObservableCollection<T> target, IReadOnlyList<T> desired, Func<T, string> key)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(key);

        for (var index = 0; index < desired.Count; index++)
        {
            var item = desired[index];
            var existing = IndexOf(target, key(item), index, key);

            if (existing < 0)
            {
                target.Insert(index, item);

                continue;
            }

            if (existing != index)
            {
                target.Move(existing, index);
            }

            if (!ReferenceEquals(target[index], item))
            {
                target[index] = item;
            }
        }

        while (target.Count > desired.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static int IndexOf<T>(ObservableCollection<T> items, string wanted, int start, Func<T, string> key)
    {
        for (var index = start; index < items.Count; index++)
        {
            if (string.Equals(key(items[index]), wanted, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
