using Contr.App.UI;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Daemon;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class StatusSummaryTests
{
    [Theory]
    [InlineData(EngineState.Running, "Engine running", StatusKind.Good)]
    [InlineData(EngineState.Starting, "Engine starting", StatusKind.Busy)]
    [InlineData(EngineState.Provisioning, "Engine starting", StatusKind.Busy)]
    [InlineData(EngineState.Stopping, "Engine stopping", StatusKind.Busy)]
    [InlineData(EngineState.Stopped, "Engine stopped", StatusKind.Neutral)]
    [InlineData(EngineState.Failed, "Engine failed", StatusKind.Critical)]
    [InlineData(EngineState.Unknown, "Connected", StatusKind.Busy)]
    public void From_DescribesTheEngineWhileConnected(EngineState engine, string text, StatusKind kind)
    {
        var summary = StatusSummary.From(new DaemonStatus(DaemonConnectionState.Connected, engine, string.Empty));

        Assert.Equal(text, summary.Text);
        Assert.Equal(kind, summary.Kind);
    }

    [Fact]
    public void From_ShowsWhyTheDaemonIsUnavailable()
    {
        var status = new DaemonStatus(DaemonConnectionState.Unavailable, EngineState.Unknown, "Cannot run contrd.exe");

        var summary = StatusSummary.From(status);

        Assert.Equal("Daemon unavailable", summary.Text);
        Assert.Equal(StatusKind.Critical, summary.Kind);
        Assert.Equal("Cannot run contrd.exe", summary.Detail);
    }

    [Fact]
    public void From_ShowsWhyTheEngineFailed()
    {
        var status = new DaemonStatus(DaemonConnectionState.Connected, EngineState.Failed, "WSL is not installed.");

        Assert.Equal("WSL is not installed.", StatusSummary.From(status).Detail);
    }

    [Fact]
    public void From_ReportsConnecting()
    {
        Assert.Equal("Connecting", StatusSummary.From(DaemonStatus.Connecting).Text);
    }
}
