using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Application.Abstractions;

/// <summary>The engine's volumes, as the module's adapter reaches them through the daemon.</summary>
/// <remarks>Streams never throw for daemon failures; they end with a failed item instead.</remarks>
public interface IVolumesGateway
{
    /// <summary>The list, then every change to it, until cancelled or broken.</summary>
    IAsyncEnumerable<ListChange<VolumeDto>> WatchAsync(CancellationToken cancellationToken);

    /// <summary>Creates a volume with the default driver; one that exists already is left unchanged.</summary>
    Task<Result> CreateAsync(VolumeName name, CancellationToken cancellationToken);

    /// <summary>Deletes a volume; the engine refuses while a container uses it.</summary>
    Task<Result> RemoveAsync(VolumeName name, CancellationToken cancellationToken);
}
