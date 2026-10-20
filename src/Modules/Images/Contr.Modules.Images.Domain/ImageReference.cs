using System.Text.RegularExpressions;
using Contr.SharedKernel;

namespace Contr.Modules.Images.Domain;

/// <summary>
/// A validated image reference, <c>name[:tag][@digest]</c>, following the grammar of the
/// distribution reference specification.
/// </summary>
public sealed partial record ImageReference
{
    private const int _maxNameLength = 255;
    private const string _defaultTag = "latest";
    private const string _domainComponent = "(?:[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])";
    private const string _domain = _domainComponent + @"(?:\." + _domainComponent + ")*(?::[0-9]+)?";
    private const string _pathComponent = "[a-z0-9]+(?:(?:[._]|__|-+)[a-z0-9]+)*";
    private const string _pattern = "^(?<name>(?:" + _domain + "/)?" + _pathComponent + "(?:/" + _pathComponent + ")*)"
        + @"(?::(?<tag>[\w][\w.-]{0,127}))?"
        + "(?:@(?<digest>[a-z0-9]+(?:[+._-][a-z0-9]+)*:[0-9a-fA-F]{32,}))?$";

    private ImageReference(string name, string? tag, string? digest)
    {
        Name = name;
        Tag = tag;
        Digest = digest;
    }

    /// <summary>The repository, with its registry host when one was given.</summary>
    public string Name { get; }

    public string? Tag { get; }

    public string? Digest { get; }

    /// <summary>The reference as the engine receives it; one without tag or digest pulls <c>latest</c>.</summary>
    public string Canonical =>
        Name + (Tag is null ? string.Empty : ":" + Tag) + (Digest is null ? string.Empty : "@" + Digest);

    /// <summary>The reference with the tag the engine resolves when none is given.</summary>
    public string Display => Tag is null && Digest is null ? $"{Name}:{_defaultTag}" : Canonical;

    public static Result<ImageReference> Parse(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return Result.Failure<ImageReference>(ImagesErrors.ReferenceRequired);
        }

        var match = ReferencePattern().Match(trimmed);

        if (!match.Success || match.Groups["name"].Value.Length > _maxNameLength)
        {
            return Result.Failure<ImageReference>(ImagesErrors.ReferenceInvalid(trimmed));
        }

        var tag = match.Groups["tag"].Success ? match.Groups["tag"].Value : null;
        var digest = match.Groups["digest"].Success ? match.Groups["digest"].Value : null;

        return new ImageReference(match.Groups["name"].Value, tag, digest);
    }

    public override string ToString()
    {
        return Canonical;
    }

    [GeneratedRegex(_pattern, RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();
}
