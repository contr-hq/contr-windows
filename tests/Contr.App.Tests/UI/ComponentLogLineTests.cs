using Contr.App.UI;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class ComponentLogLineTests
{
    [Fact]
    public void Parse_ShortensTheTimeLevelAndSource()
    {
        var line = ComponentLogLine.Parse(
            "2026-10-20 19:14:41.843 [ERR] Contr.Daemon.UI.DaemonConnection: The daemon refused\r");

        Assert.Equal(new ComponentLogLine("19:14:41", "ERROR", "DaemonConnection: The daemon refused"), line);
        Assert.Equal(("19:14:41 ", "ERROR ", string.Empty), (line.Stamp, line.ErrorLevel, line.OtherLevel));
    }

    [Fact]
    public void Parse_KeepsAContinuationLineWhole()
    {
        var line = ComponentLogLine.Parse("   at Contr.App.App.OnLaunched()");

        Assert.Equal(string.Empty, line.Level);
        Assert.Equal("   at Contr.App.App.OnLaunched()", line.Text);
        Assert.Equal((string.Empty, string.Empty), (line.Stamp, line.OtherLevel));
    }
}
