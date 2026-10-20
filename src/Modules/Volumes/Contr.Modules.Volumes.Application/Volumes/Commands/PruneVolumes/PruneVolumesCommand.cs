using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Application.Volumes.Commands.PruneVolumes;

/// <summary>Deletes volumes no container uses.</summary>
/// <param name="All">Deletes named volumes too; otherwise only anonymous ones.</param>
public sealed record PruneVolumesCommand(bool All) : IRequest<Result<VolumePruneResult>>;
