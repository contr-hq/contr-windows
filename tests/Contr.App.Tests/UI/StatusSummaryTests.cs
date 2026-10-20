using Contr.App.UI;
using Contr.UI.Contracts.Services.Daemon;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class StatusSummaryTests
{
    [Theory]
    [InlineData(EngineState.Running, "Engine running", StatusTone.Good)]
    [InlineData(EngineState.Starting, "Engine starting", StatusTone.Busy)]
    [InlineData(EngineState.Provisioning, "Engine starting", StatusTone.Busy)]
    [InlineData(EngineState.Stopping, "Engine stopping", StatusTone.Busy)]
    [InlineData(EngineState.Stopped, "Engine stopped", StatusTone.Neutral)]
    [InlineData(EngineState.Failed, "Engine failed", StatusTone.Problem)]
    [InlineData(EngineState.Unknown, "Connected", StatusTone.Busy)]
    public void From_DescribesTheEngineWhileConnected(EngineState engine, string text, StatusTone tone)
    {
        var summary = StatusSummary.From(new DaemonStatus(DaemonConnectionState.Connected, engine, string.Empty));

        Assert.Equal(text, summary.Text);
        Assert.Equal(tone, summary.Tone);
    }

    [Fact]
    public void From_ShowsWhyTheDaemonIsUnavailable()
    {
        var status = new DaemonStatus(DaemonConnectionState.Unavailable, EngineState.Unknown, "Cannot run contrd.exe");

        var summary = StatusSummary.From(status);

        Assert.Equal("Daemon unavailable", summary.Text);
        Assert.Equal(StatusTone.Problem, summary.Tone);
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
