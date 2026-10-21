using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.Services.Platform;
using Contr.App.UI;
using Microsoft.Extensions.DependencyInjection;

namespace Contr.App.Hosting.Orchestration;

/// <summary>Shows the splash window, which reports the steps that follow.</summary>
public sealed class SplashOrchestrator : IStartupOrchestrator
{
    private readonly IServiceProvider _services;
    private readonly StartupContext _context;

    public SplashOrchestrator(IServiceProvider services, StartupContext context)
    {
        _services = services;
        _context = context;
    }

    public int Order => StartupOrder.Splash;

    public string Name => nameof(SplashOrchestrator);

    public string Description => "Starting...";

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (SignInStartup.IsBackgroundLaunch())
        {
            return Task.CompletedTask;
        }

        var splash = _services.GetRequiredService<SplashWindow>();
        _context.Splash = splash;
        splash.Activate();

        return Task.CompletedTask;
    }
}
