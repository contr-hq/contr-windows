using System;
using System.Collections.Generic;
using Contr.Modules.Images.Domain;

namespace Contr.Modules.Images.Application.Images;

/// <summary>An image in the engine's local store.</summary>
/// <param name="Tags">References such as <c>nginx:1.29</c>; empty for a dangling image.</param>
/// <param name="Containers">Containers created from the image; -1 when the engine did not count them.</param>
/// <param name="HasRegistryDigest">Whether a registry knows the image; one without was built or loaded here.</param>
public sealed record ImageDto(
    ImageId Id,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    long SizeBytes,
    long Containers,
    bool HasRegistryDigest = true);
