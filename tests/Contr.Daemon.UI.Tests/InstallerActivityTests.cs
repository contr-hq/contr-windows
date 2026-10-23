using System;
using System.Threading;
using Contr.Daemon.UI;
using Contr.Sdk;
using Microsoft.Extensions.Options;
using Xunit;

namespace Contr.Daemon.UI.Tests;

public sealed class InstallerActivityTests
{
    private readonly string _mutexName = $@"Local\contr-test-{Guid.NewGuid():N}";

    [Fact]
    public void IsInstalling_FalseWithoutTheMutex()
    {
        Assert.False(new InstallerActivity(_mutexName).IsInstalling());
    }

    [Fact]
    public void IsInstalling_TrueWhileTheMutexExists()
    {
        using var mutex = new Mutex(initiallyOwned: false, _mutexName);

        Assert.True(new InstallerActivity(_mutexName).IsInstalling());
    }

    [Fact]
    public void Launcher_StartsNoDaemonWhileAnInstallerRuns()
    {
        using var mutex = new Mutex(initiallyOwned: false, _mutexName);
        var launcher = new DaemonLauncher(Options.Create(new DaemonOptions()), new InstallerActivity(_mutexName));

        var refusal = Assert.Throws<DaemonUnavailableException>(launcher.Start);

        Assert.Contains("Windows Installer", refusal.Message, StringComparison.Ordinal);
    }
}
