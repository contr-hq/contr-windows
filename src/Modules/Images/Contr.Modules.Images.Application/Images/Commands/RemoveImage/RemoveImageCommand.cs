using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Images.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Images.Application.Images.Commands.RemoveImage;

/// <summary>Deletes an image; the engine refuses while a container uses it.</summary>
public sealed record RemoveImageCommand(ImageId Id) : IRequest<Result>;
