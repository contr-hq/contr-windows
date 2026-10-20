using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.Extensions.Logging;

namespace Contr.App.Hosting.Orchestration;

/// <summary>
/// Connects to the daemon, starting it when none answers, and waits at most five seconds for the
/// handshake. Startup continues either way; the shell's status pill shows how it ended.
/// </summary>
public sealed partial class DaemonConnectionOrchestrator : IStartupOrchestrator
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(5);

    private readonly IDaemonConnection _daemon;
    private readonly ILogger<DaemonConnectionOrchestrator> _logger;

    public DaemonConnectionOrchestrator(
        IDaemonConnection daemon, ILogger<DaemonConnectionOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(logger);
        _daemon = daemon;
        _logger = logger;
    }

    public int Order => StartupOrder.DaemonConnection;

    public string Name => nameof(DaemonConnectionOrchestrator);

    public string Description => "Connecting to the Contr daemon...";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Not cancelled by the budget: a daemon that starts slowly is still worth connecting to.
        var connecting = _daemon.ConnectAsync(CancellationToken.None);
        var finished = await Task.WhenAny(connecting, Task.Delay(_budget, cancellationToken));

        if (finished != connecting)
        {
            LogStillConnecting(_budget.TotalSeconds);

            return;
        }

        var result = await connecting;

        if (result.IsFailure)
        {
            LogNotConnected(result.Error.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The daemon did not answer within {Seconds} s; continuing.")]
    private partial void LogStillConnecting(double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Starting without a daemon: {Reason}")]
    private partial void LogNotConnected(string reason);
}
