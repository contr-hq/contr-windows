using Contr.Daemon.UI;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Contr.Daemon.UI.Tests;

public sealed class DaemonConnectionTests
{
    private readonly ILocalSettingsService _settings = Substitute.For<ILocalSettingsService>();

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
    public void StartsEngineOnConnect_DefaultsToTheOption()
    {
        using var connection = CreateConnection();

        Assert.True(connection.StartsEngineOnConnect);
    }

    [Fact]
    public void StartsEngineOnConnect_PrefersAndSavesTheUsersChoice()
    {
        _settings.Read<bool?>("StartEngineOnConnect").Returns(false);
        using var connection = CreateConnection();

        Assert.False(connection.StartsEngineOnConnect);

        connection.StartsEngineOnConnect = true;

        _settings.Received(1).Save("StartEngineOnConnect", true);
    }

    [Fact]
    public void Launcher_FallsBackToTheDaemonOnPath()
    {
        var launcher = new DaemonLauncher(Options.Create(new DaemonOptions()));

        Assert.Equal("contrd.exe", launcher.Executable);
    }

    private DaemonConnection CreateConnection()
    {
        var options = Options.Create(new DaemonOptions());

        return new DaemonConnection(
            new DaemonLauncher(options), options, _settings, NullLogger<DaemonConnection>.Instance);
    }
}
