using System.Threading;
using System.Threading.Tasks;
using Contr.App.Services.Platform;

namespace Contr.App.Hosting.Orchestration;

/// <summary>Activates the main window unless Contr started in the notification area; dismisses the splash.</summary>
public sealed class ActivationOrchestrator : IStartupOrchestrator
{
    private readonly StartupContext _context;

    public ActivationOrchestrator(StartupContext context)
    {
        _context = context;
    }

    public int Order => StartupOrder.Activation;

    public string Name => nameof(ActivationOrchestrator);

    public string Description => "Ready";

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!SignInStartup.IsBackgroundLaunch())
        {
            _context.MainWindow?.Activate();
        }

        _context.Splash?.Close();
        _context.Splash = null;

        return Task.CompletedTask;
    }
}
