using System;
using System.Threading;
using Contr.App.Services.Platform;
using Xunit;

namespace Contr.App.Tests.Platform;

public sealed class QuitSignalTests
{
    private readonly string _name = QuitSignal.NameFor($"contr-test-{Guid.NewGuid():N}");

    [Fact]
    public void Send_ReportsThatNoInstanceListens()
    {
        Assert.False(QuitSignal.Send(_name, TimeSpan.Zero));
    }

    [Fact]
    public void Send_ReachesTheListeningInstance()
    {
        using var received = new ManualResetEventSlim();
        using var signal = new QuitSignal(_name, received.Set);

        Assert.True(QuitSignal.Send(_name, TimeSpan.Zero));
        Assert.True(received.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void NameFor_StaysInTheSession()
    {
        Assert.Equal(@"Local\contr-S-1-5-21-1-quit", QuitSignal.NameFor("contr-S-1-5-21-1"));
    }
}
