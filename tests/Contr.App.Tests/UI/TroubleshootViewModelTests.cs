using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.UI;
using Contr.SharedKernel;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
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
            daemon,
            Substitute.For<IDiagnostics>(),
            Substitute.For<IKubernetesCluster>(),
            Substitute.For<IDialogService>(),
            Substitute.For<INotificationService>());

        var error = await viewModel.ResetAsync(EngineResetScope.Factory);

        Assert.Equal("Reset is not available.", error);
        Assert.False(viewModel.IsResetting);
        await daemon.Received(1).ResetEngineAsync(
            EngineResetScope.Factory, Arg.Any<IProgress<EngineProgress>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Combine_ShowsTheDaemonsChecksAfterTheWindowsBuild()
    {
        DiagnosticCheck[] checks = [
            Check(DiagnosticCheck.PrerequisitesId, DiagnosticStatus.Fail, new Remediation("install-wsl", true)),
            Check("disk-space", DiagnosticStatus.Warn),
            Check("engine-api", DiagnosticStatus.Skip),
            Check("guest-images", DiagnosticStatus.Unknown),
        ];

        var rows = TroubleshootViewModel.Combine(26100, checks);

        Assert.Equal(
            ["Windows version", "prerequisites", "disk-space", "engine-api", "guest-images"],
            rows.Select(row => row.Name));
        Assert.Equal(["Passed", "Failed", "Warning", "Skipped", "Unknown"], rows.Select(row => row.Outcome));
        Assert.Equal("Install WSL", Assert.Single(rows[1].Fixes).Label);
        Assert.True(rows[1].Fixes[0].RequiresElevation);
    }

    [Fact]
    public void Checks_OfferTheEnginesFixOnTheProblemBar()
    {
        var install = new Remediation("install-wsl", true);
        var daemon = Daemon(new DaemonStatus(
            DaemonConnectionState.Connected, EngineState.Failed, "WSL is not installed.") {
            ProblemCode = "WSL_NOT_INSTALLED",
        });
        var diagnostics = Diagnostics(
            Check(DiagnosticCheck.PrerequisitesId, DiagnosticStatus.Fail, install),
            Check("disk-space", DiagnosticStatus.Pass));
        var viewModel = Create(daemon, diagnostics, Substitute.For<INotificationService>());

        viewModel.RunAgain();

        Assert.Equal(new CheckFix(install), viewModel.ProblemFix);
        Assert.Equal("Install WSL", viewModel.ProblemFixLabel);
        Assert.All(viewModel.Checks, row => Assert.Empty(row.Fixes));
        Assert.Equal([new PillItem("1 problem", PillKind.Critical)], viewModel.CheckPills);
    }

    [Fact]
    public void Checks_FallBackToTheClientsOwnWhereTheDaemonMakesNone()
    {
        var daemon = Daemon(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Stopped, string.Empty));
        var diagnostics = Substitute.For<IDiagnostics>();
        diagnostics
            .RunChecksAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<DiagnosticCheck>>(
                new Error("Daemon.FeatureUnavailable", "unknown service contr.diagnostics.v1.DiagnosticsService")));
        var viewModel = Create(daemon, diagnostics, Substitute.For<INotificationService>());

        viewModel.RunAgain();

        Assert.Contains(viewModel.Checks, row => row.Name == "Contr daemon");
        Assert.Null(viewModel.ProblemFix);
    }

    [Fact]
    public async Task Fix_SaysWhatFinishingTakes_AndChecksAgain()
    {
        var daemon = Daemon(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Stopped, string.Empty));
        var diagnostics = Diagnostics(Check(DiagnosticCheck.PrerequisitesId, DiagnosticStatus.Pass));
        diagnostics
            .RunRemediationAsync("install-wsl", Arg.Any<IProgress<EngineProgress>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new RemediationOutcome(RestartRequired: true, SignOutRequired: false)));
        var notifications = Substitute.For<INotificationService>();
        var viewModel = Create(daemon, diagnostics, notifications);

        await viewModel.FixAsync(new CheckFix(new Remediation("install-wsl", true)));

        notifications.Received(1).Show(
            "WSL is installed. Save your work, then restart when it suits you.",
            InfoBarSeverity.Informational,
            "Restart Windows to finish");
        Assert.False(viewModel.IsFixing);
        await diagnostics.Received(1).RunChecksAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fix_TellsADeclinedPromptApart()
    {
        var daemon = Daemon(new DaemonStatus(DaemonConnectionState.Connected, EngineState.Stopped, string.Empty));
        var diagnostics = Diagnostics();
        diagnostics
            .RunRemediationAsync(default!, default!, default)
            .ReturnsForAnyArgs(Result.Failure<RemediationOutcome>(
                new Error(IDiagnostics.ElevationDeclinedCode, "The administrator prompt was declined.")));
        var notifications = Substitute.For<INotificationService>();
        var viewModel = Create(daemon, diagnostics, notifications);

        await viewModel.FixAsync(new CheckFix(new Remediation("install-wsl", true)));

        notifications.Received(1).Show(
            Arg.Is<string>(message =>
                message != null && message.Contains("administrator approval", StringComparison.Ordinal)),
            InfoBarSeverity.Error,
            "WSL wasn't installed");
    }

    [Fact]
    public async Task Fix_FromTheProblemBar_KeepsTheOutcomeOnTheBar()
    {
        var install = new Remediation("install-wsl", true);
        var daemon = Daemon(new DaemonStatus(
            DaemonConnectionState.Connected, EngineState.Failed, "WSL is not installed.") {
            ProblemCode = "WSL_NOT_INSTALLED",
        });
        var diagnostics = Diagnostics(Check(DiagnosticCheck.PrerequisitesId, DiagnosticStatus.Fail, install));
        diagnostics
            .RunRemediationAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                Result.Failure<RemediationOutcome>(new Error(IDiagnostics.ElevationDeclinedCode, "declined")),
                Result.Success(new RemediationOutcome(RestartRequired: true, SignOutRequired: false)));
        var notifications = Substitute.For<INotificationService>();
        var viewModel = Create(daemon, diagnostics, notifications);
        viewModel.RunAgain();

        await viewModel.FixAsync(viewModel.ProblemFix!);

        Assert.StartsWith("WSL wasn't installed. Windows didn't get", viewModel.FixError, StringComparison.Ordinal);
        Assert.Equal(InfoBarSeverity.Error, viewModel.ProblemSeverity);

        await viewModel.FixAsync(viewModel.ProblemFix!);

        Assert.False(viewModel.HasFixError);
        Assert.Equal(InfoBarSeverity.Informational, viewModel.ProblemSeverity);
        Assert.Equal("Restart Windows to finish", viewModel.ProblemTitle);
        Assert.Null(viewModel.ProblemFix);
        Assert.False(viewModel.HasProblemCode);
        notifications.DidNotReceiveWithAnyArgs().Show(default!, default, default);
    }

    [Fact]
    public async Task CreateBundle_WritesTheZipAndSaysWhere()
    {
        var diagnostics = Diagnostics();
        diagnostics
            .CreateBundleAsync(default!, default!, default)
            .ReturnsForAnyArgs(Result.Success());
        var notifications = Substitute.For<INotificationService>();
        var viewModel = Create(Daemon(DaemonStatus.Connecting), diagnostics, notifications);
        using var stream = new MemoryStream();

        var created = await viewModel.CreateBundleAsync(stream, @"C:\Users\dev\Downloads\contr-diagnostics.zip");

        Assert.True(created);
        Assert.False(viewModel.IsCreatingBundle);
        notifications.Received(1).Show(
            @"Saved the diagnostics bundle to C:\Users\dev\Downloads\contr-diagnostics.zip.",
            InfoBarSeverity.Success,
            Arg.Any<string?>());
    }

    [Fact]
    public async Task CreateBundle_SaysWhyTheDaemonFailed()
    {
        var diagnostics = Diagnostics();
        diagnostics
            .CreateBundleAsync(default!, default!, default)
            .ReturnsForAnyArgs(Result.Failure(new Error("Daemon.Failed", "the log is locked")));
        var notifications = Substitute.For<INotificationService>();
        var viewModel = Create(Daemon(DaemonStatus.Connecting), diagnostics, notifications);
        using var stream = new MemoryStream();

        var created = await viewModel.CreateBundleAsync(stream, "bundle.zip");

        Assert.False(created);
        notifications.Received(1).Show(
            "the log is locked", InfoBarSeverity.Error, "The diagnostics bundle wasn't created");
    }

    [Fact]
    public void Bundles_AreOfferedFromApiRevision18AndNamedByTheDay()
    {
        var daemon = Daemon(DaemonStatus.Connecting);
        daemon.StatusChanges.Returns(Observable.Return(DaemonStatus.Connecting));
        daemon.Identity.Returns(_identity with { ApiRevision = 18 });
        var viewModel = Create(daemon, Diagnostics(), Substitute.For<INotificationService>());

        viewModel.Activate();

        Assert.True(viewModel.CanCreateBundle);
        Assert.Equal(
            "contr-diagnostics-2026-10-22",
            TroubleshootViewModel.BundleName(new DateTimeOffset(2026, 10, 22, 23, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task ResetKubernetes_ResetsTheClusterOnceConfirmed()
    {
        var cluster = Substitute.For<IKubernetesCluster>();
        cluster
            .ResetAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var dialogs = Substitute.For<IDialogService>();
        dialogs
            .ShowConfirmAsync(default!, default!, default!, default!)
            .ReturnsForAnyArgs(true);
        var notifications = Substitute.For<INotificationService>();
        var viewModel = Create(Daemon(DaemonStatus.Connecting), Diagnostics(), notifications, cluster, dialogs);

        await viewModel.ResetKubernetesAsync();

        await cluster.Received(1).ResetAsync(Arg.Any<CancellationToken>());
        notifications.Received(1).Show("The cluster was reset.", InfoBarSeverity.Success, Arg.Any<string?>());
    }

    private static DiagnosticCheck Check(string id, DiagnosticStatus status, params Remediation[] fixes)
    {
        return new DiagnosticCheck(id, id, status, $"{id} detail", fixes);
    }

    private static IDaemonConnection Daemon(DaemonStatus status)
    {
        var daemon = Substitute.For<IDaemonConnection>();
        daemon.Status.Returns(status);

        return daemon;
    }

    private static IDiagnostics Diagnostics(params DiagnosticCheck[] checks)
    {
        var diagnostics = Substitute.For<IDiagnostics>();
        diagnostics
            .RunChecksAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<DiagnosticCheck>>(checks));

        return diagnostics;
    }

    private static TroubleshootViewModel Create(
        IDaemonConnection daemon,
        IDiagnostics diagnostics,
        INotificationService notifications,
        IKubernetesCluster? cluster = null,
        IDialogService? dialogs = null)
    {
        return new TroubleshootViewModel(
            daemon,
            diagnostics,
            cluster ?? Substitute.For<IKubernetesCluster>(),
            dialogs ?? Substitute.For<IDialogService>(),
            notifications);
    }
}
