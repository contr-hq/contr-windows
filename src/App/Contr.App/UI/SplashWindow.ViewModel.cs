using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.UI.Contracts;

namespace Contr.App.UI;

/// <summary>Backs the splash window: the running startup step and the app version.</summary>
public sealed partial class SplashViewModel : ViewModel
{
    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string StepText { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; }

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    /// <summary>The application version, e.g. <c>v0.1.0 · build 05609c6</c>.</summary>
    public string VersionText { get; }

    public SplashViewModel()
    {
        StatusText = "Starting...";
        StepText = string.Empty;
        IsIndeterminate = true;
        VersionText = ResolveVersionText();
    }

    /// <summary>Reports the 1-based step, the step count and the step's description.</summary>
    public void ReportProgress(int step, int totalSteps, string statusText)
    {
        StatusText = statusText;
        StepText = $"{step} / {totalSteps}";
        IsIndeterminate = false;
        ProgressValue = totalSteps == 0 ? 0 : 100.0 * step / totalSteps;
    }

    /// <summary>The version, plus the commit the SDK stamps after the <c>+</c> of the informational version.</summary>
    private static string ResolveVersionText()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        string versionText = version is null
            ? string.Empty
            : $"v{version.Major}.{version.Minor}.{version.Build}";

        string? informational = assembly
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        int metadataStart = informational?.IndexOf('+') ?? -1;

        if (metadataStart < 0)
        {
            return versionText;
        }

        string commit = informational![(metadataStart + 1)..];

        if (commit.Length > 7)
        {
            commit = commit[..7];
        }

        return $"{versionText} · build {commit}";
    }
}
