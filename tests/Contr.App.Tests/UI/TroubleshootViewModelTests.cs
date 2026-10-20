using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.UI;
using Contr.SharedKernel;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class TroubleshootViewModelTests
{
    private static readonly DaemonIdentity _identity = new("npipe:////./pipe/x", "0.1.0", 3, "WSL 2");

    [Fact]
    public void Evaluate_PassesARunningEngineWithAContext()
    {
        var status = new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty) {
            Details = new EngineDetails("29.0.1", "contr"),
        };

        var checks = TroubleshootViewModel.Evaluate(status, _identity, 26100);

        Assert.All(checks, check => Assert.Equal(StatusKind.Good, check.Kind));
        Assert.Equal("0.1.0 · WSL 2", checks.Single(check => check.Name == "Contr daemon").Detail);
        Assert.Equal("Windows 11 · build 26100", checks.Single(check => check.Name == "Windows version").Detail);
    }

    [Fact]
    public void Evaluate_FailsAFailedEngineAndSkipsTheContext()
    {
        var status = new DaemonStatus(DaemonConnectionState.Connected, EngineState.Failed, "Virtualization is off.");

        var checks = TroubleshootViewModel.Evaluate(status, _identity, 26100);

        var engine = checks.Single(check => check.Name == "Engine");
        Assert.Equal(StatusKind.Critical, engine.Kind);
        Assert.Equal("Virtualization is off.", engine.Detail);
        Assert.Equal("Skipped", checks.Single(check => check.Name == "Docker context").Outcome);
    }

    [Fact]
    public void Evaluate_FailsAWindowsBuildWithoutWsl2()
    {
        var status = new DaemonStatus(DaemonConnectionState.Unavailable, EngineState.Unknown, "Cannot run contrd.exe");

        var checks = TroubleshootViewModel.Evaluate(status, null, 18363);

        Assert.Equal(StatusKind.Critical, checks.Single(check => check.Name == "Windows version").Kind);
        Assert.Equal("Cannot run contrd.exe", checks.Single(check => check.Name == "Contr daemon").Detail);
        Assert.Equal("Skipped", checks.Single(check => check.Name == "Engine").Outcome);
    }

    [Fact]
    public async Task Reset_ReturnsWhyTheDaemonRefused()
    {
        var daemon = Substitute.For<IDaemonConnection>();
        daemon.Status.Returns(DaemonStatus.Connecting);
        daemon
            .ResetEngineAsync(default, default!, default)
            .ReturnsForAnyArgs(Result.Failure(new Error("Daemon.FeatureUnavailable", "Reset is not available.")));
        var viewModel = new TroubleshootViewModel(
            daemon, Substitute.For<IDialogService>(), Substitute.For<INotificationService>());

        var error = await viewModel.ResetAsync(EngineResetScope.Factory);

        Assert.Equal("Reset is not available.", error);
        Assert.False(viewModel.IsResetting);
        await daemon.Received(1).ResetEngineAsync(
            EngineResetScope.Factory, Arg.Any<IProgress<EngineProgress>>(), Arg.Any<CancellationToken>());
    }
}
