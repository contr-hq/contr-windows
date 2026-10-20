namespace Contr.Modules.Volumes.Application.Volumes;

/// <summary>A container that mounts a volume.</summary>
/// <param name="Destination">Where the volume appears inside the container.</param>
public sealed record VolumeMountDto(string Volume, string Container, string Destination, bool IsRunning);

/// <summary>What a prune deleted.</summary>
public sealed record VolumePruneResult(int Deleted, long ReclaimedBytes);
