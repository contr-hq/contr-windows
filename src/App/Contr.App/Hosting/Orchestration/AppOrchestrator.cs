using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace Contr.App.Hosting.Orchestration;

/// <summary>Runs the registered <see cref="IStartupOrchestrator"/>s in ascending order.</summary>
public sealed partial class AppOrchestrator
{
    private readonly IReadOnlyList<IStartupOrchestrator> _orchestrators;
    private readonly StartupContext _context;
    private readonly ILogger<AppOrchestrator> _logger;

    public AppOrchestrator(
        IEnumerable<IStartupOrchestrator> orchestrators,
        StartupContext context,
        ILogger<AppOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(orchestrators);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _orchestrators = [.. orchestrators.OrderBy(orchestrator => orchestrator.Order)];
        _context = context;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("App.Startup");

        for (int index = 0; index < _orchestrators.Count; index++)
        {
            var orchestrator = _orchestrators[index];
            cancellationToken.ThrowIfCancellationRequested();
            activity?.AddTag("orchestrator.name", orchestrator.Name);
            LogRunningOrchestrator(orchestrator.Name);

            // The splash exists only from its own step on; earlier steps have nowhere to report.
            _context.Splash?.ViewModel.ReportProgress(
                index + 1, _orchestrators.Count, orchestrator.Description);

            await orchestrator.ExecuteAsync(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Running startup orchestrator {OrchestratorName}.")]
    private partial void LogRunningOrchestrator(string orchestratorName);
}
