using System.Collections.Generic;
using Contr.Modules.Containers.Application.Containers;

namespace Contr.Modules.Containers.Application.Compose;

/// <summary>A Compose project on the engine: one Compose created containers for, or one the user added.</summary>
/// <param name="WorkingDir">The directory the relative paths in the Compose files start from.</param>
/// <param name="ConfigFiles">The Compose files in the order Compose merges them; empty when none are known.</param>
/// <param name="IsAdded">The user added the project, so it stays listed while it has no containers.</param>
/// <param name="Services">Sorted by name.</param>
/// <param name="ConfigError">Why Compose could not read <paramref name="ConfigFiles"/>; empty when it could.</param>
public sealed record ComposeProjectDto(
    string Name,
    string WorkingDir,
    IReadOnlyList<string> ConfigFiles,
    bool IsAdded,
    IReadOnlyList<ComposeServiceDto> Services,
    string ConfigError);

/// <summary>A service of a Compose project.</summary>
/// <param name="Image">The image of the newest container, else the one the Compose file names.</param>
/// <param name="BuildContext">The directory the service's image builds from; empty for one it only pulls.</param>
/// <param name="Containers">Sorted by replica; empty before the service first runs.</param>
public sealed record ComposeServiceDto(
    string Name, string Image, string BuildContext, IReadOnlyList<ContainerDto> Containers);
