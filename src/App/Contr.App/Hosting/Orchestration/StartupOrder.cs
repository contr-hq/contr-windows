namespace Contr.App.Hosting.Orchestration;

/// <summary>The order of every startup step, spelled once.</summary>
/// <remarks>
/// Each number is pinned by what must run before and after it; the gaps leave room for new steps.
/// </remarks>
internal static class StartupOrder
{
    /// <summary>Before every window: accent brushes resolve once, when the first window draws.</summary>
    public const int Accent = 5;

    /// <summary>The splash covers every step below it.</summary>
    public const int Splash = 10;

    /// <summary>Before the shell, so its first pages find a connection; a failure does not stop startup.</summary>
    public const int DaemonConnection = 15;

    /// <summary>Creates the main window's content, which the steps below reach into.</summary>
    public const int Shell = 20;

    /// <summary>After the shell: the theme is applied to main-window content.</summary>
    public const int Theme = 30;

    /// <summary>After the shell, whose window the tray icon shows and hides.</summary>
    public const int Tray = 35;

    /// <summary>After the main window is ready, so the splash stays up until then.</summary>
    public const int Activation = 40;

    /// <summary>Last: the first run's setup opens over the activated main window.</summary>
    public const int Onboarding = 45;
}
