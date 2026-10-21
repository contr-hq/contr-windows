using Contr.Daemon.UI;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
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
                    PassToContainers = true,
                },
                Certificates = new Wire.CertificateSettings { TrustSystem = false },
                Registry = new Wire.RegistrySettings { Mirrors = { "https://mirror.corp" } },
            },
            LockedPaths = { "proxy.exclude", "registry.mirrors" },
        };

        var settings = DaemonSettingsClient.ToSettings(state);

        Assert.Equal(
            new ProxySettings(ProxyMode.Manual, "http://proxy.corp:8080", string.Empty, ".corp") {
                PassToContainers = true,
            },
            settings.Proxy);
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
            PassProxyToContainers = true,
            RegistryMirrors = ["https://mirror.corp"],
        });

        Assert.Equal(
            ["proxy.mode", "proxy.http", "proxy.pass_to_containers", "registry.mirrors"], request.UpdateMask.Paths);
        Assert.True(request.Settings.Proxy.PassToContainers);
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

    [Fact]
    public void ToSettings_ReadsDaemonJsonAndAnEmptyOneAsNoSettings()
    {
        var saved = DaemonSettingsClient.ToSettings(new Wire.SettingsState {
            Settings = new Wire.Settings {
                Engine = new Wire.EngineSettings { DaemonJson = "{\n  \"debug\": true\n}" },
            },
        });
        var empty = DaemonSettingsClient.ToSettings(new Wire.SettingsState());

        Assert.Equal("{\n  \"debug\": true\n}", saved.DaemonJson);
        Assert.Equal("{}", empty.DaemonJson);
    }

    [Fact]
    public void ToRequest_SendsDaemonJsonUnderItsPath()
    {
        var request = DaemonSettingsClient.ToRequest(new DaemonSettingsEdit { DaemonJson = "{}" });

        Assert.Equal(["engine.daemon_json"], request.UpdateMask.Paths);
        Assert.Equal("{}", request.Settings.Engine.DaemonJson);
    }

    [Fact]
    public void UnknownKeys_ReadTheRejectionDetail()
    {
        var rejection = new Wire.DaemonJsonRejection {
            UnknownKeys = {
                new Wire.UnknownDaemonKey { Key = "log-opt", Suggestion = "log-opts" },
                new Wire.UnknownDaemonKey { Key = "zzz" },
            },
        };
        var status = new Google.Rpc.Status {
            Code = (int)StatusCode.InvalidArgument,
            Message = "engine.daemon_json: the engine rejects its configuration: unknown keys",
            Details = { Any.Pack(rejection) },
        };

        var keys = DaemonSettingsClient.UnknownKeys(status.ToRpcException());

        Assert.Equal(
            [new UnknownDaemonKey("log-opt", "log-opts"), new UnknownDaemonKey("zzz", string.Empty)], keys);
        Assert.Empty(DaemonSettingsClient.UnknownKeys(new RpcException(new Status(StatusCode.InvalidArgument, "x"))));
    }
}
