using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.UI;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class EngineSettingsViewModelTests
{
    private const string _file = "{\n  \"log-driver\": \"json-file\",\n  \"log-opt\": { \"max-size\": \"10m\" }\n}";

    private readonly IDaemonSettings _settings = Substitute.For<IDaemonSettings>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    public EngineSettingsViewModelTests()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 13, "WSL 2") {
            Capabilities = new HashSet<DaemonCapability> { DaemonCapability.EngineConfiguration },
        });
        _daemon.Status.Returns(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Stopped, string.Empty));
    }

    [Theory]
    [InlineData("{}", 0)]
    [InlineData("{\n  \"debug\": tru\n}", 2)]
    [InlineData("[1, 2]", 1)]
    public void Check_FindsTheLineOfWhatIsNotADaemonJson(string text, int line)
    {
        var problem = EngineSettingsViewModel.Check(text);

        Assert.Equal(line, problem?.Line ?? 0);
    }

    [Fact]
    public void Keys_AreFoundAndRenamedWhereTheyAreKeys()
    {
        Assert.Equal(3, EngineSettingsViewModel.LineOf(_file, "log-opt"));
        Assert.Equal(0, EngineSettingsViewModel.LineOf(_file, "json-file"));
        Assert.Contains(
            "\"log-opts\": {", EngineSettingsViewModel.Renamed(_file, "log-opt", "log-opts"), StringComparison.Ordinal);
    }

    [Fact]
    public void Lines_EndWhereTextBoxEndsThem()
    {
        var typed = _file.Replace('\n', '\r');

        Assert.Equal(3, EngineSettingsViewModel.LineOf(typed, "log-opt"));
        var problem = EngineSettingsViewModel.Check("{\r  \"debug\": tru\r}");

        Assert.Equal(2, problem?.Line);
        Assert.DoesNotContain("\n", problem?.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSavedFileComingBackFromTheEditor_IsNotAnEdit()
    {
        var viewModel = Create();
        viewModel.Show(Saved(_file));

        viewModel.Text = _file.Replace('\n', '\r');

        Assert.False(viewModel.HasChanges);
        Assert.Null(viewModel.EditedAt);
        Assert.Equal("1\n2\n3\n4", viewModel.LineNumbers);
    }

    [Fact]
    public void Format_IndentsTheObjectTwoSpacesDeep()
    {
        var formatted = EngineSettingsViewModel.Formatted("{\"debug\":true}");

        Assert.Equal("{\n  \"debug\": true\n}", formatted.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Apply_NamesTheLineOfAnUnknownKeyAndOffersTheSuggestion()
    {
        _settings
            .UpdateDaemonJsonAsync(default!, default)
            .ReturnsForAnyArgs(Refused(new UnknownDaemonKey("log-opt", "log-opts")));
        var viewModel = Create();
        viewModel.Text = _file;

        await viewModel.ApplyAsync();

        Assert.Equal("Line 3: unknown option \"log-opt\".", viewModel.ErrorTitle);
        Assert.Equal("Did you mean \"log-opts\"? The engine won't start with this file.", viewModel.ErrorMessage);
        Assert.Equal("Change to log-opts", viewModel.SuggestionText);
        Assert.Equal("Line 3 · Error · unknown option \"log-opt\"", Assert.Single(viewModel.Problems).Display);
        Assert.False(viewModel.CanApply);
        _notifications.Received(1).Close();
    }

    [Fact]
    public async Task ChangeToSuggestion_RenamesTheKeyAndAppliesAgain()
    {
        var saved = Saved("{\n  \"log-opts\": {}\n}");
        _settings
            .UpdateDaemonJsonAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Refused(new UnknownDaemonKey("log-opt", "log-opts")),
                new DaemonJsonUpdate(Result.Success(new DaemonSettingsChange(saved, new HashSet<DaemonSetting>()))));
        var viewModel = Create();
        viewModel.Text = "{\n  \"log-opt\": {}\n}";
        await viewModel.ApplyAsync();

        await viewModel.ChangeToSuggestionAsync();

        await _settings.Received(1).UpdateDaemonJsonAsync("{\n  \"log-opts\": {}\n}", Arg.Any<CancellationToken>());
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.HasChanges);
        Assert.Equal(saved.DaemonJson, viewModel.Text);
    }

    [Fact]
    public async Task Apply_PointsAtAKeyContrSetsItself()
    {
        _settings
            .UpdateDaemonJsonAsync(default!, default)
            .ReturnsForAnyArgs(new DaemonJsonUpdate(Result.Failure<DaemonSettingsChange>(new Error(
                IDaemonSettings.InvalidValueCode,
                "engine.daemon_json: registry-mirrors is not available: set registry.mirrors instead"))));
        var viewModel = Create();
        viewModel.Text = "{\n  \"debug\": true,\n  \"registry-mirrors\": []\n}";

        await viewModel.ApplyAsync();

        Assert.Equal(
            "Line 3: registry-mirrors is not available: set registry.mirrors instead.", viewModel.ErrorTitle);
        Assert.False(viewModel.HasSuggestion);
    }

    [Fact]
    public async Task Apply_StopsAtTextThatIsNotJson()
    {
        var viewModel = Create();
        viewModel.Text = "{\n  \"debug\": tru\n}";

        await viewModel.ApplyAsync();

        Assert.StartsWith("Line 2: ", viewModel.ErrorTitle, StringComparison.Ordinal);
        await _settings.DidNotReceiveWithAnyArgs().UpdateDaemonJsonAsync(default!, default);
    }

    [Fact]
    public void Editing_ClearsTheErrorAndMarksTheChange()
    {
        var viewModel = Create();
        viewModel.Text = "{\n  \"debug\": tru\n}";
        viewModel.Format();

        Assert.True(viewModel.HasError);

        viewModel.Text = "{\"debug\": true}";

        Assert.False(viewModel.HasError);
        Assert.True(viewModel.CanApply);
        Assert.Equal("· edited now", viewModel.EditedText);
    }

    [Fact]
    public void Locked_KeepsTheFileReadOnly()
    {
        var viewModel = new EngineSettingsViewModel(_settings, _daemon, _notifications);

        viewModel.Show(new DaemonSettings(false, true, new HashSet<DaemonSetting> { DaemonSetting.DaemonJson }));

        Assert.True(viewModel.IsLocked);
        Assert.False(viewModel.CanEdit);
    }

    private static DaemonJsonUpdate Refused(params UnknownDaemonKey[] keys)
    {
        return new DaemonJsonUpdate(Result.Failure<DaemonSettingsChange>(new Error(
            IDaemonSettings.InvalidValueCode, "engine.daemon_json: the engine rejects its configuration: unknown"))) {
            UnknownKeys = keys,
        };
    }

    private static DaemonSettings Saved(string text)
    {
        return new DaemonSettings(false, true, new HashSet<DaemonSetting>()) { DaemonJson = text };
    }

    private EngineSettingsViewModel Create()
    {
        var viewModel = new EngineSettingsViewModel(_settings, _daemon, _notifications);
        viewModel.Show(Saved("{}"));

        return viewModel;
    }
}
