using Contr.Daemon.UI;
using Contr.UI.Contracts.Services.Daemon;
using Xunit;
using Wire = Contr.Sdk.Settings.V1;

namespace Contr.Daemon.UI.Tests;

public sealed class DaemonSettingsClientTests
{
    [Fact]
    public void ToSettings_KeepsTheValuesAndTheLocksItKnows()
    {
        var state = new Wire.SettingsState {
            Settings = new Wire.Settings {
                Engine = new Wire.EngineSettings { StartWithDaemon = true, ServeDefaultPipe = false },
            },
            LockedPaths = { "engine.serve_default_pipe", "proxies.http" },
            Etag = "4",
        };

        var settings = DaemonSettingsClient.ToSettings(state);

        Assert.True(settings.StartEngineWithDaemon);
        Assert.False(settings.ServeDefaultPipe);
        Assert.Equal([DaemonSetting.ServeDefaultPipe], settings.Locked);
        Assert.True(settings.IsLocked(DaemonSetting.ServeDefaultPipe));
        Assert.False(settings.IsLocked(DaemonSetting.StartEngineWithDaemon));
    }

    [Fact]
    public void ToSettings_ReadsAnEmptyStateAsTheDefaults()
    {
        var settings = DaemonSettingsClient.ToSettings(new Wire.SettingsState());

        Assert.False(settings.StartEngineWithDaemon);
        Assert.False(settings.ServeDefaultPipe);
        Assert.Empty(settings.Locked);
    }

    [Fact]
    public void ToChange_NamesTheSettingsThatWaitForTheEngine()
    {
        var change = DaemonSettingsClient.ToChange(
            new Wire.SettingsState(), ["engine.serve_default_pipe", "engine.unknown"]);

        Assert.Equal([DaemonSetting.ServeDefaultPipe], change.RestartRequired);
    }
}
