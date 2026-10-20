using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Volumes.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Application.Volumes.Commands.RemoveVolume;

/// <summary>Deletes a volume and the data in it.</summary>
public sealed record RemoveVolumeCommand(VolumeName Name) : IRequest<Result>;
