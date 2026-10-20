using System.Threading;
using System.Threading.Tasks;
using Contr.App.Hosting.Orchestration;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.Hosting;

public sealed class DaemonConnectionOrchestratorTests
{
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly ILogger<DaemonConnectionOrchestrator> _logger =
        Substitute.For<ILogger<DaemonConnectionOrchestrator>>();

    [Fact]
    public async Task Execute_ContinuesStartupWhenNoDaemonAnswers()
    {
        _daemon
            .ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("Daemon.Unavailable", "not running")));

        await new DaemonConnectionOrchestrator(_daemon, _logger).ExecuteAsync(CancellationToken.None);

        await _daemon.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_DoesNotCancelASlowConnection()
    {
        var connecting = new TaskCompletionSource<Result>();
        CancellationToken passed = default;
        _daemon
            .ConnectAsync(Arg.Do<CancellationToken>(token => passed = token))
            .Returns(connecting.Task);
        using var startup = new CancellationTokenSource();

        var step = new DaemonConnectionOrchestrator(_daemon, _logger).ExecuteAsync(startup.Token);
        startup.Cancel();
        await step;

        Assert.False(passed.CanBeCanceled);
    }
}
