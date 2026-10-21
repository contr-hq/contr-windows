using System;
using System.Collections.Generic;
using Contr.Modules.Containers.Domain;

namespace Contr.Modules.Containers.Application.Containers;

/// <summary>What a filesystem mounted into a new container comes from.</summary>
public enum MountSource
{
    /// <summary>A folder on this PC, by its Windows path.</summary>
    Bind,

    /// <summary>A volume, by name; no name creates an anonymous one.</summary>
    Volume,

    /// <summary>Memory, gone when the container stops.</summary>
    Tmpfs,
}

/// <param name="Source">A Windows path for <see cref="MountSource.Bind"/>, a volume name, or empty.</param>
/// <param name="Destination">The absolute path inside the container.</param>
public sealed record MountRequest(MountSource Kind, string Source, string Destination, bool ReadOnly);

/// <summary>A container to create from a local image, as <c>docker run</c> describes one.</summary>
/// <param name="Image">A local image's reference, such as <c>nginx:1.27-alpine</c>.</param>
/// <param name="Name">Absent lets the engine choose a random name.</param>
/// <param name="Environment"><c>VAR=value</c> entries added to the image's.</param>
/// <param name="Command">Replaces the image's command; empty keeps it.</param>
/// <param name="Network">Empty attaches to the engine's default bridge.</param>
/// <param name="AutoRemove">Deletes the container when it exits.</param>
public sealed record ContainerSpec(
    string Image,
    ContainerName? Name,
    IReadOnlyList<PortBinding> Ports,
    IReadOnlyList<MountRequest> Mounts,
    IReadOnlyList<string> Environment,
    IReadOnlyList<string> Command,
    string Network,
    RestartPolicy RestartPolicy,
    bool AutoRemove)
{
    /// <summary>A spec with only <paramref name="image"/> set.</summary>
    public static ContainerSpec For(string image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return new ContainerSpec(image, null, [], [], [], [], string.Empty, RestartPolicy.Never, false);
    }
}

/// <param name="Warnings">What the engine warned about, such as an option it ignored.</param>
public sealed record CreatedContainer(ContainerId Id, IReadOnlyList<string> Warnings);

public sealed record ContainersPruned(int Deleted, long ReclaimedBytes);
