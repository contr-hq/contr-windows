using System.Threading;
using System.Threading.Tasks;
using Contr.Sdk;

namespace Contr.Daemon.UI;

/// <summary>The connected daemon client that module adapters call through.</summary>
public interface IDaemonClient
{
    /// <summary>Returns the connected client, connecting first when no connection is open.</summary>
    /// <exception cref="ContrException">No daemon could be reached, or it refused the handshake.</exception>
    ValueTask<ContrClient> GetAsync(CancellationToken cancellationToken);
}
