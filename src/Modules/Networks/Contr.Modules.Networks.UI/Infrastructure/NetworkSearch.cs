using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Application.Networks.Queries.WatchNetworks;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Search;

namespace Contr.Modules.Networks.UI.Infrastructure;

/// <summary>Finds networks by name or Compose project for the title bar's search.</summary>
public sealed class NetworkSearch : ISearchSource
{
    private const string _pageKey = "Networks";
    private const int _limit = 5;

    private readonly ISender _sender;

    public NetworkSearch(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var items = await ListSnapshot.FirstAsync(
            token => _sender.Send(new WatchNetworksQuery(), token), cancellationToken);

        return [.. items
            .Where(network => network.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || network.ComposeProject.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(network => !network.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .ThenBy(network => network.Name, StringComparer.OrdinalIgnoreCase)
            .Take(_limit)
            .Select(network => new SearchHit(
                network.Name,
                $"Network · {network.Driver}",
                "\uE968",
                _pageKey,
                [network.Id.Value]))];
    }
}
