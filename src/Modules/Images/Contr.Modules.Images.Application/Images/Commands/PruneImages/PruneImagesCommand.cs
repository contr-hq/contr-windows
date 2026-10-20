using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Images.Application.Images.Commands.PruneImages;

/// <summary>Deletes images no container uses.</summary>
/// <param name="All">Deletes tagged images too; otherwise only untagged ones.</param>
public sealed record PruneImagesCommand(bool All) : IRequest<Result<PruneResult>>;
