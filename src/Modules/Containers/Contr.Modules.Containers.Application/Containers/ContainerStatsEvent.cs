using System;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers;

/// <summary>One event of a container's resource usage stream.</summary>
public abstract record ContainerStatsEvent
{
    private ContainerStatsEvent()
    {
    }

    /// <summary>A sample; the engine sends about one per second.</summary>
    public sealed record Sample(ContainerStatsDto Stats) : ContainerStatsEvent;

    /// <summary>The last event of a stream that broke.</summary>
    public sealed record Failed(Error Error) : ContainerStatsEvent;
}

/// <summary>A container's resource usage at one moment, computed the way <c>docker stats</c> computes it.</summary>
/// <param name="CpuPercent">100 per fully used CPU core.</param>
/// <param name="MemoryUsageBytes">Usage without inactive page cache.</param>
/// <param name="MemoryLimitBytes">The container's limit, or the engine's memory when it has none.</param>
/// <param name="NetworkRxBytes">Received since the container started.</param>
/// <param name="NetworkTxBytes">Sent since the container started.</param>
/// <param name="BlockReadBytes">Read since the container started.</param>
/// <param name="BlockWriteBytes">Written since the container started.</param>
public sealed record ContainerStatsDto(
    DateTimeOffset ReadAt,
    double CpuPercent,
    long MemoryUsageBytes,
    long MemoryLimitBytes,
    long NetworkRxBytes,
    long NetworkTxBytes,
    long BlockReadBytes,
    long BlockWriteBytes,
    long Pids);
