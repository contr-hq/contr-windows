using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>Splits and quotes command lines the way a POSIX shell reads them, for <c>docker run</c>.</summary>
public static class CommandLine
{
    /// <summary>Words separated by spaces; single or double quotes keep spaces inside a word.</summary>
    public static IReadOnlyList<string> Split(string? text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var inWord = false;
        char? quote = null;

        foreach (var character in text ?? string.Empty)
        {
            if (quote is { } open)
            {
                if (character == open)
                {
                    quote = null;
                }
                else
                {
                    word.Append(character);
                }
            }
            else if (character is '"' or '\'')
            {
                quote = character;
                inWord = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (inWord)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    inWord = false;
                }
            }
            else
            {
                word.Append(character);
                inWord = true;
            }
        }

        if (inWord)
        {
            words.Add(word.ToString());
        }

        return words;
    }

    /// <summary>The word as a shell takes it back: quoted when it holds anything a shell would read.</summary>
    public static string Quote(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        return word.Length > 0 && word.All(IsPlain)
            ? word
            : $"'{word.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }

    private static bool IsPlain(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || "-_./:=,@%+\\".Contains(character, StringComparison.Ordinal);
    }
}
