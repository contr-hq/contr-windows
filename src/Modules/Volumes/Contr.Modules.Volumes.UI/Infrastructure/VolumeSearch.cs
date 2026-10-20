using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Volumes.Application.Volumes.Queries.WatchVolumes;
using Contr.UI.Common.Icons;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Search;

namespace Contr.Modules.Volumes.UI.Infrastructure;

/// <summary>Finds volumes by name or Compose project for the title bar's search.</summary>
public sealed class VolumeSearch : ISearchSource
{
    private const string _pageKey = "Volumes";
    private const int _limit = 5;

    private readonly ISender _sender;

    public VolumeSearch(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var items = await ListSnapshot.FirstAsync(
            token => _sender.Send(new WatchVolumesQuery(), token), cancellationToken);

        return [.. items
            .Where(volume => volume.Name.Value.Contains(text, StringComparison.OrdinalIgnoreCase)
                || volume.ComposeProject.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(volume => !volume.Name.Value.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .ThenBy(volume => volume.Name.Value, StringComparer.OrdinalIgnoreCase)
            .Take(_limit)
            .Select(volume => new SearchHit(
                volume.Name.Value,
                volume.ComposeProject.Length > 0 ? $"Volume · {volume.ComposeProject} project" : "Volume",
                ContrIcons.Volumes,
                _pageKey,
                [volume.Name.Value]))];
    }
}
