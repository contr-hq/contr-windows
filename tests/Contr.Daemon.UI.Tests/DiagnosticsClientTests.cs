using System.Threading;
using Contr.Daemon.UI;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Grpc.Core;
using Xunit;
using Wire = Contr.Sdk.Diagnostics.V1;
using WireOperations = Contr.Sdk.Operations.V1;
using WireRemediation = Contr.Sdk.Common.V1.Remediation;

namespace Contr.Daemon.UI.Tests;

public sealed class DiagnosticsClientTests
{
    [Theory]
    [InlineData(Wire.CheckStatus.Pass, DiagnosticStatus.Pass)]
    [InlineData(Wire.CheckStatus.Warn, DiagnosticStatus.Warn)]
    [InlineData(Wire.CheckStatus.Fail, DiagnosticStatus.Fail)]
    [InlineData(Wire.CheckStatus.Skip, DiagnosticStatus.Skip)]
    [InlineData(Wire.CheckStatus.Unspecified, DiagnosticStatus.Unknown)]
    [InlineData((Wire.CheckStatus)9, DiagnosticStatus.Unknown)]
    public void ToCheck_MapsTheStatus(Wire.CheckStatus status, DiagnosticStatus expected)
    {
        var check = DiagnosticsClient.ToCheck(new Wire.Check { Id = "disk-space", Status = status });

        Assert.Equal(expected, check.Status);
    }

    [Fact]
    public void ToCheck_KeepsTheRemediationsInOrder()
    {
        var wire = new Wire.Check {
            Id = DiagnosticCheck.PrerequisitesId,
            Title = "Engine prerequisites",
            Status = Wire.CheckStatus.Fail,
            Detail = "WSL is not installed.",
            Remediations = {
                new WireRemediation { ActionId = Remediation.InstallWslId, RequiresElevation = true },
                new WireRemediation { ActionId = "setup-rootless" },
            },
        };

        var check = DiagnosticsClient.ToCheck(wire);

        Assert.Equal("Engine prerequisites", check.Title);
        Assert.Equal("WSL is not installed.", check.Detail);
        Assert.Equal(
            [new Remediation(Remediation.InstallWslId, true), new Remediation("setup-rootless", false)],
            check.Remediations);
    }

    [Theory]
    [InlineData("install-wsl failed: wsl --install exited with 1", "wsl --install exited with 1")]
    [InlineData("no remediation \"install-wsl\"", "no remediation \"install-wsl\"")]
    [InlineData("install-wsl failed: ", "install-wsl failed: ")]
    public void Reason_DropsTheActionId(string message, string expected)
    {
        var error = DiagnosticsClient.Reason(new Error("Daemon.Conflict", message), Remediation.InstallWslId);

        Assert.Equal(new Error("Daemon.Conflict", expected), error);
    }

    [Theory]
    [InlineData("Daemon.NotFound")]
    [InlineData("Daemon.FeatureUnavailable")]
    public void Reason_SaysTheFixIsNotOffered(string code)
    {
        var error = DiagnosticsClient.Reason(new Error(code, "no remediation \"install-wsl\""), "install-wsl");

        Assert.Equal(new Error(code, "Contr's background service doesn't offer this fix."), error);
    }

    [Fact]
    public void Reason_TellsADeclinedPromptApart()
    {
        var declined = new Error("Daemon.Conflict", "install-wsl failed: the administrator prompt was declined");

        var error = DiagnosticsClient.Reason(declined, Remediation.InstallWslId);

        Assert.Equal(IDiagnostics.ElevationDeclinedCode, error.Code);
    }

    [Fact]
    public void Dismisses_ACancelTheCallerDidNotAskFor()
    {
        var cancelled = new RpcException(new Status(StatusCode.Cancelled, "the prompt was dismissed"));
        using var asked = new CancellationTokenSource();
        asked.Cancel();

        Assert.True(DiagnosticsClient.Dismisses(cancelled, CancellationToken.None));
        Assert.False(DiagnosticsClient.Dismisses(cancelled, asked.Token));
        Assert.False(DiagnosticsClient.Dismisses(
            new RpcException(new Status(StatusCode.Internal, "boom")), CancellationToken.None));
    }

    [Fact]
    public void Ended_TellsADismissedPromptFromAFailedFix()
    {
        var dismissed = new WireOperations.Operation {
            State = WireOperations.OperationState.Failed,
            Error = new WireOperations.OperationError { Code = (int)StatusCode.Cancelled, Message = "dismissed" },
        };
        var failed = new WireOperations.Operation {
            State = WireOperations.OperationState.Failed,
            Error = new WireOperations.OperationError {
                Code = (int)StatusCode.Internal,
                Message = "install-wsl failed: wsl --install exited with 1",
            },
        };

        Assert.Equal(
            IDiagnostics.ElevationDeclinedCode,
            DiagnosticsClient.Ended(dismissed, null, Remediation.InstallWslId).Error.Code);
        Assert.Equal(
            "wsl --install exited with 1",
            DiagnosticsClient.Ended(failed, null, Remediation.InstallWslId).Error.Message);
    }

    [Fact]
    public void Ended_KeepsTheOutcomeOfAFixThatSucceeded()
    {
        var succeeded = new WireOperations.Operation { State = WireOperations.OperationState.Succeeded };

        var result = DiagnosticsClient.Ended(succeeded, new RemediationOutcome(true, false), Remediation.InstallWslId);

        Assert.True(result.Value.RestartRequired);
    }
}
