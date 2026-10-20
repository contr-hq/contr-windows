using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Commands.CreateNetwork;

/// <summary>Creates a bridge network; the engine allocates its subnet.</summary>
public sealed record CreateNetworkCommand(string Name) : IRequest<Result>;
