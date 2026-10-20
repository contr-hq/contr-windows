using System;
using System.Globalization;
using Contr.Modules.Images.Application.Images;
using Contr.Modules.Images.Domain;
using Contr.UI.Common.Formatting;

namespace Contr.Modules.Images.UI.ViewModels;

/// <summary>One row of the images list.</summary>
/// <param name="OtherTags">Tags beyond the first, such as <c>+2 tags</c>; empty when there are none.</param>
/// <param name="Containers">Containers created from the image; -1 when the engine did not count them.</param>
/// <param name="IsLocalBuild">A tagged image no registry knows: built or loaded on this machine.</param>
public sealed record ImageRow(
    ImageId ImageId,
    string Repository,
    string Tag,
    string OtherTags,
    long SizeBytes,
    string Created,
    long Containers,
    bool IsLocalBuild)
{
    private const string _none = "<none>";

    public string Id => ImageId.Value;

    public string ShortId => ImageId.Short;

    public bool HasOtherTags => OtherTags.Length > 0;

    /// <summary>An image without a tag, left behind by a rebuild or a newer pull.</summary>
    public bool IsDangling => Tag == _none;

    public bool InUse => Containers > 0;

    public string Size => HumanFormat.Bytes(SizeBytes);

    /// <summary>Such as <c>2 containers</c> or <c>Not used</c>.</summary>
    public string UsedBy => Containers switch {
        < 0 => "—",
        0 => "Not used",
        1 => "1 container",
        _ => $"{Containers.ToString(CultureInfo.CurrentCulture)} containers",
    };

    /// <summary>The reference a removal names: the first tag, else the ID.</summary>
    public string DisplayName => IsDangling ? ShortId : $"{Repository}:{Tag}";

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
            image.SizeBytes,
            HumanFormat.Ago(image.CreatedAt, DateTimeOffset.UtcNow),
            image.Containers,
            image.Tags.Count > 0 && !image.HasRegistryDigest);
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
