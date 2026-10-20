using System.Threading;
using System.Threading.Tasks;
using Contr.App.UI;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class ShellViewModelTests
{
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    public ShellViewModelTests()
    {
        _daemon.Status.Returns(DaemonStatus.Connecting);
    }

    [Fact]
    public void ApplyStatus_OffersStopWhileTheEngineRuns()
    {
        var viewModel = CreateViewModel();

        viewModel.ApplyStatus(Connected(EngineState.Running));

        Assert.Equal("Engine running", viewModel.EngineStatus.Text);
        Assert.True(viewModel.StopEngineCommand.CanExecute(null));
        Assert.True(viewModel.RestartEngineCommand.CanExecute(null));
        Assert.False(viewModel.StartEngineCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyStatus_OffersStartWhileTheEngineIsStopped()
    {
        var viewModel = CreateViewModel();

        viewModel.ApplyStatus(Connected(EngineState.Stopped));

        Assert.True(viewModel.StartEngineCommand.CanExecute(null));
        Assert.False(viewModel.StopEngineCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyStatus_OffersNothingWithoutADaemon()
    {
        var viewModel = CreateViewModel();

        viewModel.ApplyStatus(new DaemonStatus(DaemonConnectionState.Unavailable, EngineState.Stopped, "gone"));

        Assert.False(viewModel.StartEngineCommand.CanExecute(null));
        Assert.False(viewModel.StopEngineCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyStatus_NamesTheConnectedDaemon()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 2, "Fake"));
        var viewModel = CreateViewModel();

        viewModel.ApplyStatus(Connected(EngineState.Running));

        Assert.Equal("Daemon 0.1.0 · Fake backend", viewModel.DaemonText);
    }

    [Fact]
    public async Task StartEngine_ShowsAFailure()
    {
        _daemon
            .StartEngineAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("Daemon.Failed", "no WSL")));
        var viewModel = CreateViewModel();
        viewModel.ApplyStatus(Connected(EngineState.Stopped));

        await viewModel.StartEngineCommand.ExecuteAsync(null);

        _notifications.Received(1).Show("no WSL", InfoBarSeverity.Error, "The engine did not start");
    }

    private static DaemonStatus Connected(EngineState engine)
    {
        return new DaemonStatus(DaemonConnectionState.Connected, engine, string.Empty);
    }

    private ShellViewModel CreateViewModel()
    {
        return new ShellViewModel(_navigation, _daemon, _notifications);
    }
}
