using Contr.Daemon.UI;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Contr.Daemon.UI.Tests;

public sealed class DaemonConnectionTests
{
    [Fact]
    public void Status_StartsAsConnecting()
    {
        using var connection = CreateConnection();

        Assert.Equal(DaemonStatus.Connecting, connection.Status);
        Assert.Null(connection.Identity);
    }

    [Fact]
    public void Dispose_CanRunOncePerRegistration()
    {
        var connection = CreateConnection();

        connection.Dispose();
        connection.Dispose();
    }

    [Fact]
    public void Launcher_FallsBackToTheDaemonOnPath()
    {
        var launcher = new DaemonLauncher(Options.Create(new DaemonOptions()));

        Assert.Equal("contrd.exe", launcher.Executable);
    }

    private static DaemonConnection CreateConnection()
    {
        var options = Options.Create(new DaemonOptions());

        return new DaemonConnection(new DaemonLauncher(options), options, NullLogger<DaemonConnection>.Instance);
    }
}
