using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Contr.UI.Contracts.Services.Search;

/// <summary>Answers the title bar's search from what one module lists, such as its containers.</summary>
/// <remarks>Every registered source is asked; the shell shows their hits in registration order.</remarks>
public interface ISearchSource
{
    /// <summary>The items whose name contains <paramref name="text"/>, best first.</summary>
    /// <remarks>Returns none while the engine is down.</remarks>
    /// <param name="text">What the user typed, trimmed and never empty.</param>
    /// <param name="cancellationToken">Cancelled when the user types again.</param>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string text, CancellationToken cancellationToken);
}

/// <summary>One suggestion under the title bar's search box, and where choosing it goes.</summary>
/// <param name="Title">The item's name.</param>
/// <param name="Detail">What it is, such as <c>Container · nginx:1.29</c>.</param>
/// <param name="Glyph">A Contr Icons glyph for the kind of item, such as <c>ContrIcons.Containers</c>.</param>
/// <param name="PageKey">The page that shows the item.</param>
/// <param name="Arguments">The arguments that page needs to show this item.</param>
public sealed record SearchHit(
    string Title, string Detail, string Glyph, string PageKey, IReadOnlyList<object> Arguments);
