using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Application.Volumes.Queries.GetVolumeUsage;

/// <summary>Every volume mount of every container, stopped ones included.</summary>
public sealed record GetVolumeUsageQuery : IRequest<Result<IReadOnlyList<VolumeMountDto>>>;
