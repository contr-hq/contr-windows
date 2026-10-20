using CommunityToolkit.Mvvm.ComponentModel;

namespace Contr.App.UI.Onboarding;

/// <summary>Where the user is relative to a setup step.</summary>
public enum OnboardingStepState
{
    Pending,
    Current,
    Done,
}

/// <summary>One step in the setup's left rail.</summary>
public sealed partial class OnboardingStep : ObservableObject
{
    [ObservableProperty]
    public partial OnboardingStepState State { get; set; }

    /// <summary>What the step found or did, such as <c>Engine started in 41 s</c>; empty until then.</summary>
    [ObservableProperty]
    public partial string Detail { get; set; }

    public OnboardingStep(int number, string title)
    {
        Number = number;
        Title = title;
        Detail = string.Empty;
    }

    public int Number { get; }

    public string Title { get; }
}

/// <summary>One step of the engine's setup as the daemon reported it.</summary>
public sealed partial class EngineSetupStep : ObservableObject
{
    [ObservableProperty]
    public partial bool IsDone { get; set; }

    public EngineSetupStep(string message)
    {
        Message = message;
    }

    public string Message { get; }
}
