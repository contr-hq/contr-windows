using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.Services.Platform;
using Contr.App.UI.Onboarding;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class OnboardingWslTests
{
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly IDiagnostics _diagnostics = Substitute.For<IDiagnostics>();
    private readonly ICliTools _cliTools = Substitute.For<ICliTools>();
    private readonly IWslDistributions _wsl = Substitute.For<IWslDistributions>();
    private readonly ISignInStartup _signIn = Substitute.For<ISignInStartup>();
    private readonly ILocalSettingsService _settings = Substitute.For<ILocalSettingsService>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();

    public OnboardingWslTests()
    {
        _daemon.Status.Returns(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty));
        _cliTools
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CliToolsStatus>(new Error("Daemon.FeatureUnavailable", "no tools")));
        Prerequisites(DiagnosticStatus.Pass);
    }

    [Fact]
    public async Task CheckStep_GoesToTheEngineWhereNothingIsMissing()
    {
        var viewModel = CreateViewModel();

        await viewModel.NextAsync();

        Assert.True(viewModel.IsEngine);
        Assert.False(viewModel.IsWslMissing);
    }

    [Fact]
    public async Task CheckStep_OffersToInstallWslWhereTheDaemonFoundItMissing()
    {
        Prerequisites(DiagnosticStatus.Fail, new Remediation(Remediation.InstallWslId, true));
        var viewModel = CreateViewModel();

        await viewModel.NextAsync();

        Assert.True(viewModel.IsWsl);
        Assert.Equal("Install WSL", viewModel.NextText);
        Assert.True(viewModel.NextNeedsApproval);
        Assert.True(viewModel.CanPostpone);
    }

    [Fact]
    public async Task WslStep_StaysWithTheReasonWhenThePromptIsDeclined()
    {
        Prerequisites(DiagnosticStatus.Fail, new Remediation(Remediation.InstallWslId, true));
        Install(Result.Failure<RemediationOutcome>(
            new Error(IDiagnostics.ElevationDeclinedCode, "The administrator prompt was declined.")));
        var viewModel = CreateViewModel();
        await viewModel.NextAsync();

        await viewModel.NextAsync();

        Assert.True(viewModel.IsWsl);
        Assert.True(viewModel.HasWslError);
        Assert.StartsWith("Windows didn't get administrator approval", viewModel.WslError, StringComparison.Ordinal);
        Assert.Equal("Install WSL", viewModel.NextText);
    }

    [Fact]
    public async Task WslStep_WaitsForTheRestart_AndTheNextSetupStartsAtTheEngine()
    {
        Prerequisites(DiagnosticStatus.Fail, new Remediation(Remediation.InstallWslId, true));
        Install(Result.Success(new RemediationOutcome(RestartRequired: true, SignOutRequired: false)));
        var viewModel = CreateViewModel();
        await viewModel.NextAsync();

        await viewModel.NextAsync();

        Assert.True(viewModel.IsWsl);
        Assert.True(viewModel.IsWslRestartNeeded);
        Assert.Equal("Restart Windows to finish", viewModel.WslDoneTitle);
        Assert.Equal("Close", viewModel.NextText);
        Assert.False(viewModel.CanGoBack);
        _settings.Received(1).Save(OnboardingViewModel.ResumeAtEngineKey, true);

        _settings.Read<bool>(OnboardingViewModel.ResumeAtEngineKey).Returns(true);
        var resumed = CreateViewModel();
        await resumed.StartAsync();

        Assert.True(resumed.IsEngine);
        _settings.Received(1).Save(OnboardingViewModel.ResumeAtEngineKey, false);
    }

    [Fact]
    public async Task WslStep_GoesOnToTheEngineWhenNoRestartIsNeeded()
    {
        Prerequisites(DiagnosticStatus.Fail, new Remediation(Remediation.InstallWslId, true));
        Install(Result.Success(new RemediationOutcome(RestartRequired: false, SignOutRequired: false)));
        var viewModel = CreateViewModel();
        await viewModel.NextAsync();

        await viewModel.NextAsync();

        Assert.True(viewModel.IsEngine);
        _settings.DidNotReceive().Save(OnboardingViewModel.ResumeAtEngineKey, Arg.Any<bool>());
    }

    [Fact]
    public void ItInstructions_NameThePcTheCommandAndTheBuild()
    {
        var instructions = OnboardingViewModel.ItInstructions("DESKTOP-42", 19045);

        Assert.Contains("DESKTOP-42", instructions, StringComparison.Ordinal);
        Assert.Contains("    wsl --install --no-distribution", instructions, StringComparison.Ordinal);
        Assert.Contains("this PC runs build 19045", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolsStep_ChecksTheDefaultDistroWhileNoneIsOn_AndConnectsIt()
    {
        Offer(Distribution("Debian", isDefault: false), Distribution("Ubuntu-24.04", isDefault: true));
        _wsl
            .SetIntegrationAsync("Ubuntu-24.04", true, Arg.Any<CancellationToken>())
            .Returns(Result.Success(
                Distribution("Ubuntu-24.04", isDefault: true) with { State = WslIntegrationState.Active }));
        var viewModel = CreateViewModel();

        await viewModel.NextAsync();
        await viewModel.NextAsync();

        Assert.True(viewModel.HasDistributions);
        Assert.Equal([true, false], viewModel.Distributions.Select(item => item.IsOn));

        await viewModel.NextAsync();

        await _wsl.Received(1).SetIntegrationAsync("Ubuntu-24.04", true, Arg.Any<CancellationToken>());
        await _wsl.DidNotReceive().SetIntegrationAsync("Debian", Arg.Any<bool>(), Arg.Any<CancellationToken>());
        Assert.Equal("Ubuntu-24.04", viewModel.WslText);
        Assert.Contains(viewModel.Summary, row => row.Name == "WSL distros");
    }

    [Fact]
    public async Task ToolsStep_HidesTheDistrosWithoutTheCapability()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 4, "Fake"));
        var viewModel = CreateViewModel();

        await viewModel.NextAsync();
        await viewModel.NextAsync();

        Assert.False(viewModel.HasDistributions);
        await _wsl.DidNotReceiveWithAnyArgs().ListAsync(default);
    }

    private void Prerequisites(DiagnosticStatus status, params Remediation[] fixes)
    {
        DiagnosticCheck[] checks = [
            new(DiagnosticCheck.PrerequisitesId, "Engine prerequisites", status, string.Empty, fixes),
        ];
        _diagnostics
            .RunChecksAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<DiagnosticCheck>>(checks));
    }

    private void Install(Result<RemediationOutcome> result)
    {
        _diagnostics
            .RunRemediationAsync(
                Remediation.InstallWslId, Arg.Any<IProgress<EngineProgress>>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    private static WslDistribution Distribution(string name, bool isDefault)
    {
        return new WslDistribution(name, 2, isDefault, WslIntegrationState.Disabled, string.Empty);
    }

    private void Offer(params WslDistribution[] distributions)
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 4, "WSL 2") {
            Capabilities = new HashSet<DaemonCapability> { DaemonCapability.WslIntegration },
        });
        _wsl
            .ListAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<WslDistribution>>(distributions));
    }

    private OnboardingViewModel CreateViewModel()
    {
        return new OnboardingViewModel(_daemon, _diagnostics, _cliTools, _wsl, _signIn, _settings, _clipboard);
    }
}
