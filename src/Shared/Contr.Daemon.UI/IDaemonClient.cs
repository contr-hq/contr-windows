using System.Threading;
using System.Threading.Tasks;
using Contr.Sdk;
using Contr.UI.Contracts.Services.Daemon;

namespace Contr.Daemon.UI;

/// <summary>The connected daemon client that module adapters call through.</summary>
public interface IDaemonClient
{
    /// <summary>The connected daemon; <see langword="null"/> while not connected.</summary>
    DaemonIdentity? Identity { get; }

    /// <summary>Returns the connected client, connecting first when no connection is open.</summary>
    /// <exception cref="ContrException">No daemon could be reached, or it refused the handshake.</exception>
    ValueTask<ContrClient> GetAsync(CancellationToken cancellationToken);
}
