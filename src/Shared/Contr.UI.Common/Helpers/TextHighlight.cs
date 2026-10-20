using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Contr.UI.Common.Helpers;

/// <summary>Highlights every occurrence of a query in a <see cref="TextBlock"/>, ignoring case.</summary>
public static class TextHighlight
{
    public static readonly DependencyProperty QueryProperty = DependencyProperty.RegisterAttached(
        "Query", typeof(string), typeof(TextHighlight), new PropertyMetadata(string.Empty, OnQueryChanged));

    /// <summary>The token of the text-change callback, so a recycled text block registers once.</summary>
    private static readonly DependencyProperty _textCallbackProperty = DependencyProperty.RegisterAttached(
        "TextCallback", typeof(long), typeof(TextHighlight), new PropertyMetadata(0L));

    public static string GetQuery(TextBlock textBlock)
    {
        ArgumentNullException.ThrowIfNull(textBlock);

        return (string)textBlock.GetValue(QueryProperty);
    }

    public static void SetQuery(TextBlock textBlock, string value)
    {
        ArgumentNullException.ThrowIfNull(textBlock);

        textBlock.SetValue(QueryProperty, value);
    }

    private static void OnQueryChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        if ((long)textBlock.GetValue(_textCallbackProperty) == 0)
        {
            var token = textBlock.RegisterPropertyChangedCallback(TextBlock.TextProperty, OnTextChanged);
            textBlock.SetValue(_textCallbackProperty, token);
        }

        Apply(textBlock);
    }

    private static void OnTextChanged(DependencyObject sender, DependencyProperty property)
    {
        Apply((TextBlock)sender);
    }

    private static void Apply(TextBlock textBlock)
    {
        textBlock.TextHighlighters.Clear();
        var query = GetQuery(textBlock).Trim();
        var text = textBlock.Text;

        if (query.Length == 0 || text.Length == 0)
        {
            return;
        }

        var highlighter = new TextHighlighter {
            Background = ThemeResources.Brush(textBlock, "SystemFillColorCautionBrush"),
            Foreground = ThemeResources.Brush(textBlock, "TextOnAccentFillColorPrimaryBrush"),
        };

        var start = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        while (start >= 0)
        {
            highlighter.Ranges.Add(new TextRange { StartIndex = start, Length = query.Length });
            start = text.IndexOf(query, start + query.Length, StringComparison.OrdinalIgnoreCase);
        }

        if (highlighter.Ranges.Count > 0)
        {
            textBlock.TextHighlighters.Add(highlighter);
        }
    }
}
