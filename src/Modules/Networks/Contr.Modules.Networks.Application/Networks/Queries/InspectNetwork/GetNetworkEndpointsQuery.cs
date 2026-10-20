using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Networks.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Networks.Application.Networks.Queries.InspectNetwork;

/// <summary>The running containers connected to a network; the engine lists no stopped ones.</summary>
public sealed record GetNetworkEndpointsQuery(NetworkId Id) : IRequest<Result<IReadOnlyList<NetworkEndpointDto>>>;
