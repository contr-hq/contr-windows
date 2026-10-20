using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Application.Volumes.Commands.CreateVolume;

/// <summary>Creates a named volume; an invalid name fails before the engine is asked.</summary>
public sealed record CreateVolumeCommand(string Name) : IRequest<Result>;
