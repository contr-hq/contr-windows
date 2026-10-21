using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.UI;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class ProxySettingsViewModelTests
{
    private readonly IDaemonSettings _settings = Substitute.For<IDaemonSettings>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    public ProxySettingsViewModelTests()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 12, "WSL 2") {
            Capabilities = new HashSet<DaemonCapability> { DaemonCapability.EngineConfiguration },
        });
        _daemon.Status.Returns(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty));
    }

    [Fact]
    public void Show_LoadsTheSavedSettingsWithNothingToApply()
    {
        var viewModel = Create();

        viewModel.Show(Saved(ProxyMode.Manual, http: "http://proxy:8080", mirrors: ["https://a", "https://b"]));

        Assert.Equal(1, viewModel.ModeIndex);
        Assert.Equal("http://proxy:8080", viewModel.Http);
        Assert.Equal("https://a", viewModel.Mirror);
        Assert.False(viewModel.HasChanges);
        Assert.True(viewModel.CanEditHttp);
    }

    [Fact]
    public void Edits_NameOnlyWhatChanged()
    {
        var viewModel = Create();
        viewModel.Show(Saved(ProxyMode.Manual, http: "http://proxy:8080", mirrors: ["https://a", "https://b"]));

        viewModel.Http = " http://other:3128 ";
        viewModel.Mirror = "https://c";

        var edit = viewModel.Edit();
        Assert.Equal("http://other:3128", edit.ProxyHttp);
        Assert.Equal(["https://c", "https://b"], edit.RegistryMirrors);
        Assert.Null(edit.ProxyMode);
        Assert.Equal(2, viewModel.ChangeCount);
        Assert.Equal("2 unsaved changes.", viewModel.ChangesText);
    }

    [Fact]
    public void Servers_ApplyOnlyInManualMode()
    {
        var viewModel = Create();
        viewModel.Show(Saved(ProxyMode.System, locked: [DaemonSetting.ProxyExclude]));

        Assert.False(viewModel.CanEditHttp);

        viewModel.ModeIndex = 1;

        Assert.True(viewModel.CanEditHttp);
        Assert.False(viewModel.CanEditExclude);
        Assert.True(viewModel.IsExcludeLocked);
    }

    [Fact]
    public void WithoutTheCapability_NothingCanChange()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 12, "Fake"));
        var viewModel = Create();

        viewModel.Show(Saved(ProxyMode.System));

        Assert.False(viewModel.IsOffered);
        Assert.False(viewModel.CanEdit);
        Assert.False(viewModel.CanEditMode);
    }

    [Fact]
    public void Show_KeepsEditsTheUserHasNotApplied()
    {
        var viewModel = Create();
        viewModel.Show(Saved(ProxyMode.System));
        viewModel.ModeIndex = 2;

        viewModel.Show(Saved(ProxyMode.System, mirrors: ["https://a"]));

        Assert.Equal(2, viewModel.ModeIndex);
        Assert.Equal(ProxyMode.None, viewModel.Edit().ProxyMode);
    }

    [Fact]
    public async Task Apply_RestartsARunningEngineThatWaitsForIt()
    {
        var after = Saved(ProxyMode.None);
        _settings
            .UpdateAsync(default!, default)
            .ReturnsForAnyArgs(Result.Success(new DaemonSettingsChange(
                after, new HashSet<DaemonSetting> { DaemonSetting.ProxyMode })));
        _daemon.RestartEngineAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        var viewModel = Create();
        viewModel.Show(Saved(ProxyMode.System));
        viewModel.ModeIndex = 2;

        await viewModel.ApplyAsync();

        await _daemon.Received(1).RestartEngineAsync(Arg.Any<CancellationToken>());
        _notifications.Received(1).Show(
            "The engine restarted with the new settings.", InfoBarSeverity.Success, Arg.Any<string?>());
        Assert.False(viewModel.HasChanges);
        Assert.False(viewModel.IsApplying);
    }

    [Fact]
    public async Task Apply_NamesTheFieldTheDaemonRefused()
    {
        _settings
            .UpdateAsync(default!, default)
            .ReturnsForAnyArgs(Result.Failure<DaemonSettingsChange>(
                new Error(IDaemonSettings.InvalidValueCode, "proxy.http: \"proxy\" names no server")));
        var viewModel = Create();
        viewModel.Show(Saved(ProxyMode.Manual));
        viewModel.Http = "proxy";

        await viewModel.ApplyAsync();

        _notifications.Received(1).Show(
            "HTTP proxy: \"proxy\" names no server", InfoBarSeverity.Error, "Nothing was saved");
        await _daemon.DidNotReceiveWithAnyArgs().RestartEngineAsync(default);
        Assert.True(viewModel.HasChanges);
    }

    private static DaemonSettings Saved(
        ProxyMode mode, string http = "", string[]? mirrors = null, DaemonSetting[]? locked = null)
    {
        return new DaemonSettings(false, true, new HashSet<DaemonSetting>(locked ?? [])) {
            Proxy = new ProxySettings(mode, http, string.Empty, string.Empty),
            RegistryMirrors = mirrors ?? [],
        };
    }

    private ProxySettingsViewModel Create()
    {
        return new ProxySettingsViewModel(_settings, _daemon, _notifications);
    }
}
