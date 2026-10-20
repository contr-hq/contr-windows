using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.UI.Contracts.Services.Platform;
using Contr.UI.Contracts.Services.Search;
using Microsoft.Extensions.Logging;

namespace Contr.App.UI;

/// <summary>The title bar's search: asks every module's <see cref="ISearchSource"/> and opens the hit chosen.</summary>
public sealed partial class GlobalSearch
{
    /// <summary>A source that has not answered by then is left out, so one slow call cannot hold the list.</summary>
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(3);

    private readonly IReadOnlyList<ISearchSource> _sources;
    private readonly INavigationService _navigation;
    private readonly ILogger<GlobalSearch> _logger;

    public GlobalSearch(
        IEnumerable<ISearchSource> sources, INavigationService navigation, ILogger<GlobalSearch> logger)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(logger);
        _sources = [.. sources];
        _navigation = navigation;
        _logger = logger;
    }

    /// <summary>Every source's hits for <paramref name="text"/>, in the sources' order.</summary>
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string text, CancellationToken cancellationToken)
    {
        var query = text.Trim();

        if (query.Length == 0)
        {
            return [];
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);

        var answers = await Task.WhenAll(_sources.Select(source => AskAsync(source, query, deadline.Token)));
        cancellationToken.ThrowIfCancellationRequested();

        return [.. answers.SelectMany(hits => hits)];
    }

    /// <summary>Shows the page that holds <paramref name="hit"/>.</summary>
    public void Open(SearchHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);

        _navigation.NavigateTo(hit.PageKey, [.. hit.Arguments]);
    }

    private async Task<IReadOnlyList<SearchHit>> AskAsync(
        ISearchSource source, string query, CancellationToken cancellationToken)
    {
        try
        {
            return await source.SearchAsync(query, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            LogSourceFailed(exception, source.GetType().Name);

            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Search source {Source} failed")]
    private partial void LogSourceFailed(Exception exception, string source);
}
