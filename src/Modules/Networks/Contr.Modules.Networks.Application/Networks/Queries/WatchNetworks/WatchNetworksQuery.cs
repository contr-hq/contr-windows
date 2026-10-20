using System.Collections.Generic;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;

namespace Contr.Modules.Networks.Application.Networks.Queries.WatchNetworks;

/// <summary>Every network, then every change, until the request's cancellation token fires.</summary>
public sealed record WatchNetworksQuery : IRequest<IAsyncEnumerable<ListChange<NetworkDto>>>;
