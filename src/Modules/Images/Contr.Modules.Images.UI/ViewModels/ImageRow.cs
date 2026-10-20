using System;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Domain;
using Contr.UI.Common.Formatting;

namespace Contr.Modules.Images.UI.ViewModels;

/// <summary>One row of the images list.</summary>
/// <param name="OtherTags">Tags beyond the first, such as <c>+2 tags</c>; empty when there are none.</param>
public sealed record ImageRow(
    ImageId ImageId,
    string Repository,
    string Tag,
    string OtherTags,
    string Size,
    string Created,
    bool InUse)
{
    private const string _none = "<none>";

    public string Id => ImageId.Value;

    public string ShortId => ImageId.Short;

    public bool HasOtherTags => OtherTags.Length > 0;

    /// <summary>The reference a removal names: the first tag, else the ID.</summary>
    public string DisplayName => Tag == _none ? ShortId : $"{Repository}:{Tag}";

    public static ImageRow From(ImageDto image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var (repository, tag) = image.Tags.Count > 0 ? SplitTag(image.Tags[0]) : (_none, _none);
        var otherTags = image.Tags.Count switch {
            <= 1 => string.Empty,
            2 => "+1 tag",
            _ => $"+{image.Tags.Count - 1} tags",
        };

        return new ImageRow(
            image.Id,
            repository,
            tag,
            otherTags,
            HumanFormat.Bytes(image.SizeBytes),
            HumanFormat.Age(image.CreatedAt, DateTimeOffset.UtcNow),
            image.Containers > 0);
    }

    public static int Compare(ImageRow? left, ImageRow? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var byRepository = string.Compare(left.Repository, right.Repository, StringComparison.OrdinalIgnoreCase);

        return byRepository != 0 ? byRepository : string.Compare(left.Tag, right.Tag, StringComparison.Ordinal);
    }

    /// <summary>Splits <c>registry:5000/name:tag</c> at the colon after the last slash.</summary>
    public static (string Repository, string Tag) SplitTag(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var colon = reference.LastIndexOf(':');
        var slash = reference.LastIndexOf('/');

        return colon > slash ? (reference[..colon], reference[(colon + 1)..]) : (reference, _none);
    }
}
