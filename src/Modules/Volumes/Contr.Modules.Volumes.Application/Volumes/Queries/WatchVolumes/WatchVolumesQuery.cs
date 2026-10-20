using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;

namespace Contr.Modules.Volumes.Application.Volumes.Queries.WatchVolumes;

/// <summary>Every volume, then every change, until the request's cancellation token fires.</summary>
public sealed record WatchVolumesQuery : IRequest<IAsyncEnumerable<ListChange<VolumeDto>>>;
