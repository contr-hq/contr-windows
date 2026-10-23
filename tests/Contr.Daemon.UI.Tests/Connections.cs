using System.Collections.Generic;
using System.Reactive.Linq;
using Contr.UI.Contracts.Services.Daemon;
using NSubstitute;

namespace Contr.Daemon.UI.Tests;

internal static class Connections
{
    /// <summary>A connection that stays connected to a daemon offering <paramref name="capabilities"/>.</summary>
    public static IDaemonConnection Connected(params DaemonCapability[] capabilities)
    {
        var connection = Substitute.For<IDaemonConnection>();
        connection.Identity.Returns(new DaemonIdentity("npipe:////./pipe/contr", "0.1.0", 20, "Fake") {
            Capabilities = new HashSet<DaemonCapability>(capabilities),
        });
        connection.StatusChanges.Returns(Observable
            .Never<DaemonStatus>()
            .StartWith(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty)));

        return connection;
    }
}
