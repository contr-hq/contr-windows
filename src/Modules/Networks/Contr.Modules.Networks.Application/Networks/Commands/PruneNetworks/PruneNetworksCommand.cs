using Contr.Application.Abstractions.Messaging;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Commands.PruneNetworks;

/// <summary>Deletes the user-defined networks no container is connected to; returns how many it deleted.</summary>
public sealed record PruneNetworksCommand : IRequest<Result<int>>;
