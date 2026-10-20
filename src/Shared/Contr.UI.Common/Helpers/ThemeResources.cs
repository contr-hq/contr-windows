using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Contr.UI.Common.Helpers;

/// <summary>Theme resources as an element's own theme resolves them.</summary>
/// <remarks>
/// <c>Application.Current.Resources[key]</c> answers for the theme the app started in, not the theme a window
/// or page requests.
/// </remarks>
public static class ThemeResources
{
    public static Brush Brush(FrameworkElement element, string key)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var theme = element.ActualTheme == ElementTheme.Light ? "Light" : "Default";
        var resources = Microsoft.UI.Xaml.Application.Current.Resources;

        if (Find(resources, theme, key) is Brush brush)
        {
            return brush;
        }

        foreach (var merged in resources.MergedDictionaries)
        {
            if (Find(merged, theme, key) is Brush mergedBrush)
            {
                return mergedBrush;
            }
        }

        return (Brush)resources[key];
    }

    private static object? Find(ResourceDictionary dictionary, string theme, string key)
    {
        return dictionary.ThemeDictionaries.TryGetValue(theme, out var themed)
            && themed is ResourceDictionary themedDictionary
            && themedDictionary.TryGetValue(key, out var value)
                ? value
                : null;
    }
}
