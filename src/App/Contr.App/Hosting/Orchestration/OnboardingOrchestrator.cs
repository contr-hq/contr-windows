using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.Services.Platform;
using Contr.App.UI.Onboarding;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Contr.App.Hosting.Orchestration;

/// <summary>Opens the setup on the first run; a launch into the notification area never shows it.</summary>
public sealed class OnboardingOrchestrator : IStartupOrchestrator
{
    private readonly IServiceProvider _services;
    private readonly ILocalSettingsService _settings;

    public OnboardingOrchestrator(IServiceProvider services, ILocalSettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);
        _services = services;
        _settings = settings;
    }

    public int Order => StartupOrder.Onboarding;

    public string Name => nameof(OnboardingOrchestrator);

    public string Description => "Setting up...";

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!SignInStartup.IsBackgroundLaunch() && !_settings.Read<bool>(OnboardingViewModel.SetupDoneKey))
        {
            _services
                .GetRequiredService<OnboardingWindow>()
                .Activate();
        }

        return Task.CompletedTask;
    }
}
