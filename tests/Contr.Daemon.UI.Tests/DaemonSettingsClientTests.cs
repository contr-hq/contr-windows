using Contr.Daemon.UI;
using Contr.SharedKernel;
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

    [Fact]
    public void ToSettings_ReadsTheProxyCertificatesAndMirrors()
    {
        var state = new Wire.SettingsState {
            Settings = new Wire.Settings {
                Proxy = new Wire.ProxySettings {
                    Mode = Wire.ProxyMode.Manual,
                    Http = "http://proxy.corp:8080",
                    Exclude = ".corp",
                },
                Certificates = new Wire.CertificateSettings { TrustSystem = false },
                Registry = new Wire.RegistrySettings { Mirrors = { "https://mirror.corp" } },
            },
            LockedPaths = { "proxy.exclude", "registry.mirrors" },
        };

        var settings = DaemonSettingsClient.ToSettings(state);

        Assert.Equal(
            new ProxySettings(ProxyMode.Manual, "http://proxy.corp:8080", string.Empty, ".corp"), settings.Proxy);
        Assert.False(settings.TrustSystemCertificates);
        Assert.Equal(["https://mirror.corp"], settings.RegistryMirrors);
        Assert.True(settings.IsLocked(DaemonSetting.ProxyExclude));
        Assert.True(settings.IsLocked(DaemonSetting.RegistryMirrors));
    }

    [Fact]
    public void ToSettings_ReadsAMissingProxyAsTheDefaults()
    {
        var settings = DaemonSettingsClient.ToSettings(new Wire.SettingsState { Settings = new Wire.Settings() });

        Assert.Equal(ProxySettings.Default, settings.Proxy);
        Assert.True(settings.TrustSystemCertificates);
        Assert.Empty(settings.RegistryMirrors);
    }

    [Fact]
    public void ToRequest_NamesOnlyTheEditedSettings()
    {
        var request = DaemonSettingsClient.ToRequest(new DaemonSettingsEdit {
            ProxyMode = ProxyMode.None,
            ProxyHttp = string.Empty,
            RegistryMirrors = ["https://mirror.corp"],
        });

        Assert.Equal(["proxy.mode", "proxy.http", "registry.mirrors"], request.UpdateMask.Paths);
        Assert.Equal(Wire.ProxyMode.None, request.Settings.Proxy.Mode);
        Assert.Equal(string.Empty, request.Settings.Proxy.Http);
        Assert.Equal(["https://mirror.corp"], request.Settings.Registry.Mirrors);
    }

    [Theory]
    [InlineData("Daemon.Failed", "proxy.http: \"x\" is not a URL", "Daemon.InvalidSetting")]
    [InlineData("Daemon.Failed", "proxy.socks: \"x\" is not a URL", "Daemon.Failed")]
    [InlineData("Daemon.SettingLocked", "proxy.http: locked", "Daemon.SettingLocked")]
    public void Refusal_TellsAnInvalidValueApart(string code, string message, string expected)
    {
        Assert.Equal(expected, DaemonSettingsClient.Refusal(new Error(code, message)).Code);
    }

    [Fact]
    public void Refused_NamesTheSettingAndTheReason()
    {
        Assert.Equal(
            (DaemonSetting.RegistryMirrors, "\"ftp://m\" does not use http, https"),
            DaemonSettingPaths.Refused("registry.mirrors: \"ftp://m\" does not use http, https"));
        Assert.Null(DaemonSettingPaths.Refused("the etag is not one the daemon gave"));
    }
}
