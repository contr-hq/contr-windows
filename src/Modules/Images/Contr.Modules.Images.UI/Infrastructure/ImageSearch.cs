using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Images.Application.Images.Queries.WatchImages;
using Contr.UI.Common.Formatting;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Search;

namespace Contr.Modules.Images.UI.Infrastructure;

/// <summary>Finds images by tag for the title bar's search.</summary>
public sealed class ImageSearch : ISearchSource
{
    private const string _pageKey = "Images";
    private const int _limit = 5;

    private readonly ISender _sender;

    public ImageSearch(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var items = await ListSnapshot.FirstAsync(
            token => _sender.Send(new WatchImagesQuery(), token), cancellationToken);

        return [.. items
            .SelectMany(image => image.Tags.Select(tag => (Image: image, Tag: tag)))
            .Where(tagged => tagged.Tag.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tagged => !tagged.Tag.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .ThenBy(tagged => tagged.Tag, StringComparer.OrdinalIgnoreCase)
            .Take(_limit)
            .Select(tagged => new SearchHit(
                tagged.Tag,
                $"Image · {HumanFormat.Bytes(tagged.Image.SizeBytes)}",
                "\uE8B9",
                _pageKey,
                []))];
    }
}
