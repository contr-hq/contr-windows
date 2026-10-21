using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Search;

namespace Contr.Modules.Containers.UI.Infrastructure;

/// <summary>Finds containers by name, image or Compose project for the title bar's search.</summary>
public sealed class ContainerSearch : ISearchSource
{
    private const string _detailPageKey = "ContainerDetail";
    private const int _limit = 5;

    private readonly ISender _sender;

    public ContainerSearch(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var containers = await ListSnapshot.FirstAsync(
            token => _sender.Send(new WatchContainersQuery(), token), cancellationToken);

        return [.. containers
            .Where(container => container.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || container.Image.Contains(text, StringComparison.OrdinalIgnoreCase)
                || container.ComposeProject.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(container => !container.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .ThenBy(container => container.Name, StringComparer.OrdinalIgnoreCase)
            .Take(_limit)
            .Select(container => new SearchHit(
                container.Name,
                $"Container · {container.Image}",
                "\uE7B8",
                _detailPageKey,
                [container.Id, ContainerDetailTab.Logs]))];
    }
}
