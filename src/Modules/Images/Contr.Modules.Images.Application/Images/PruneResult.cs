namespace Contr.Modules.Images.Application.Images;

/// <summary>What a prune deleted.</summary>
public sealed record PruneResult(int Deleted, long ReclaimedBytes);
