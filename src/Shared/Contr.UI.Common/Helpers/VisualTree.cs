using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Contr.UI.Common.Helpers;

/// <summary>Searches the elements a control's template or a page's XAML created.</summary>
public static class VisualTree
{
    /// <summary>The first <typeparamref name="T"/> under <paramref name="root"/>, depth first.</summary>
    public static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        ArgumentNullException.ThrowIfNull(root);

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is T found)
            {
                return found;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
