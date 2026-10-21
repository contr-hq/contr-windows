using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.App.Services.Platform;
using Contr.Interoperability;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Formatting;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;

namespace Contr.App.UI.Onboarding;

/// <summary>The first-run setup: check this PC, WSL, the engine, the tools, and done.</summary>
public sealed partial class OnboardingViewModel : ViewModel
{
    /// <summary>The setting that records a finished setup, so it shows once.</summary>
    public const string SetupDoneKey = "SetupDone";

    /// <summary>The command Microsoft documents for installing WSL without a Linux distribution.</summary>
    public const string InstallWslCommand = "wsl --install --no-distribution";

    private const string _wslMissing = "WSL_NOT_INSTALLED";
    private const long _lowDiskBytes = 10_000_000_000;

    private readonly IDaemonConnection _daemon;
    private readonly ICliTools _cliTools;
    private readonly ISignInStartup _signIn;
    private readonly ILocalSettingsService _settings;
    private readonly IClipboardService _clipboard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepLabel), nameof(IsChecking), nameof(IsWsl), nameof(IsEngine))]
    [NotifyPropertyChangedFor(nameof(IsTools), nameof(IsReady), nameof(CanGoBack), nameof(NextText))]
    public partial int StepIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsWslMissing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEngineError))]
    public partial string EngineError { get; set; }

    [ObservableProperty]
    public partial double EngineProgress { get; set; }

    [ObservableProperty]
    public partial bool IsEngineProgressIndeterminate { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    public partial bool IsEngineRunning { get; set; }

    /// <summary>Such as <c>docker 28.3.2 · compose 2.39.1</c>; empty while unknown.</summary>
    [ObservableProperty]
    public partial string ToolsText { get; set; }

    /// <summary>Why the tools may not be what a shell runs, such as another <c>docker</c> earlier on PATH.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToolsNote))]
    public partial string ToolsNote { get; set; }

    [ObservableProperty]
    public partial bool CanInstallTools { get; set; }

    [ObservableProperty]
    public partial bool InstallTools { get; set; }

    [ObservableProperty]
    public partial string ContextText { get; set; }

    [ObservableProperty]
    public partial bool StartAtSignIn { get; set; }

    public OnboardingViewModel(
        IDaemonConnection daemon,
        ICliTools cliTools,
        ISignInStartup signIn,
        ILocalSettingsService settings,
        IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(cliTools);
        ArgumentNullException.ThrowIfNull(signIn);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clipboard);
        _daemon = daemon;
        _cliTools = cliTools;
        _signIn = signIn;
        _settings = settings;
        _clipboard = clipboard;
        EngineError = string.Empty;
        ToolsText = string.Empty;
        ToolsNote = string.Empty;
        ContextText = string.Empty;
        StartAtSignIn = true;
        Steps = [
            new OnboardingStep(1, "Check this PC"),
            new OnboardingStep(2, "Install WSL"),
            new OnboardingStep(3, "Set up the engine"),
            new OnboardingStep(4, "Tools and integration"),
            new OnboardingStep(5, "Ready"),
        ];
    }

    /// <summary>Raised when the user finishes; the window closes.</summary>
    public event EventHandler? Finished;

    public ObservableCollection<OnboardingStep> Steps { get; }

    public ObservableCollection<CheckRow> Checks { get; } = [];

    public ObservableCollection<EngineSetupStep> EngineSteps { get; } = [];

    public ObservableCollection<string> EngineLog { get; } = [];

    public ObservableCollection<CheckRow> Summary { get; } = [];

    public string StepLabel => $"Step {StepIndex + 1} of {Steps.Count}";

    public bool IsChecking => StepIndex == 0;

    public bool IsWsl => StepIndex == 1;

    public bool IsEngine => StepIndex == 2;

    public bool IsTools => StepIndex == 3;

    public bool IsReady => StepIndex == 4;

    public bool CanGoBack => StepIndex is > 0 and < 4;

    public bool CanGoNext => !IsBusy && (StepIndex != 2 || IsEngineRunning);

    public string NextText => StepIndex switch {
        3 => "Finish setup",
        4 => "Open Contr",
        _ => "Next",
    };

    public bool HasEngineError => EngineError.Length > 0;

    public bool HasToolsNote => ToolsNote.Length > 0;

    /// <summary>Shows the first step; call it once the window is up.</summary>
    public void Start()
    {
        Show(0);
        RunChecks();
    }

    public async Task NextAsync()
    {
        switch (StepIndex)
        {
            case 0:
                Show(1);
                Steps[1].Detail = IsWslMissing ? "Needed" : "Checked while the engine starts";
                await NextAsync();
                break;
            case 1:
                Show(2);
                await StartEngineAsync();
                break;
            case 2:
                Show(3);
                await LoadToolsAsync();
                break;
            case 3:
                await ApplyToolsAsync();
                Show(4);
                Summarise();
                break;
            default:
                _settings.Save(SetupDoneKey, true);
                Finished?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public void Back()
    {
        if (CanGoBack)
        {
            Show(StepIndex - 1);
        }
    }

    public async Task RetryEngineAsync()
    {
        await StartEngineAsync();
    }

    public void CopyWslCommand()
    {
        _clipboard.SetText(InstallWslCommand);
    }

    /// <summary>The checks the client makes itself: the Windows build, virtualization and free disk space.</summary>
    public static CheckRow[] CheckThisPc(int windowsBuild, bool? virtualization, long freeBytes, string drive)
    {
        var free = $"{HumanFormat.Bytes(freeBytes)} free on {drive}";

        return [
            TroubleshootViewModel.WindowsCheck(windowsBuild),
            virtualization switch {
                true => new CheckRow(StatusKind.Good, "Passed", "Virtualization", "Turned on"),
                false => new CheckRow(StatusKind.Critical, "Failed", "Virtualization", "Turned off in the firmware"),
                null => new CheckRow(StatusKind.Neutral, "Unknown", "Virtualization", "The engine's start checks it"),
            },
            freeBytes >= _lowDiskBytes
                ? new CheckRow(StatusKind.Good, "Passed", "Free disk space", free)
                : new CheckRow(StatusKind.Caution, "Warning", "Free disk space", $"{free} · images need room"),
        ];
    }

    private void Show(int index)
    {
        StepIndex = index;

        for (var step = 0; step < Steps.Count; step++)
        {
            Steps[step].State = step < index
                ? OnboardingStepState.Done
                : step == index ? OnboardingStepState.Current : OnboardingStepState.Pending;
        }
    }

    private void RunChecks()
    {
        var drive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var free = new DriveInfo(drive).AvailableFreeSpace;
        var build = Environment.OSVersion.Version.Build;
        var checks = CheckThisPc(build, SystemInterop.IsVirtualizationEnabled(), free, drive.TrimEnd('\\'));

        Checks.Clear();
        foreach (var check in checks)
        {
            Checks.Add(check);
        }

        Steps[0].Detail = string.Join(" · ", checks.Select(check => check.Detail));
    }

    private async Task StartEngineAsync()
    {
        if (_daemon.Status.Engine == EngineState.Running)
        {
            IsEngineRunning = true;
            Steps[1].Detail = "Nothing to install";
            Steps[2].Detail = "The engine was already running";

            return;
        }

        IsBusy = true;
        EngineError = string.Empty;
        EngineSteps.Clear();
        EngineLog.Clear();
        IsEngineProgressIndeterminate = true;
        var started = Stopwatch.StartNew();
        var progress = new Progress<EngineProgress>(ShowProgress);
        try
        {
            var result = await _daemon.StartEngineAsync(progress, CancellationToken.None);

            if (result.IsFailure)
            {
                EngineError = result.Error.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }

        var status = _daemon.Status;
        IsWslMissing = status.ProblemCode == _wslMissing;
        IsEngineRunning = status.Engine == EngineState.Running && EngineError.Length == 0;

        if (status.Engine == EngineState.Failed && EngineError.Length == 0)
        {
            EngineError = status.Problem;
        }

        if (IsEngineRunning)
        {
            foreach (var step in EngineSteps)
            {
                step.IsDone = true;
            }

            EngineProgress = 100;
            IsEngineProgressIndeterminate = false;
            var seconds = started.Elapsed.TotalSeconds.ToString("0", CultureInfo.CurrentCulture);
            Steps[2].Detail = $"Engine started in {seconds} s";
        }
        else if (IsWslMissing)
        {
            Steps[1].Detail = "Needed";
            Show(1);
        }
    }

    private void ShowProgress(EngineProgress step)
    {
        if (EngineSteps.Count == 0 || EngineSteps[^1].Message != step.Message)
        {
            if (EngineSteps.Count > 0)
            {
                EngineSteps[^1].IsDone = true;
            }

            EngineSteps.Add(new EngineSetupStep(step.Message));
        }

        EngineLog.Add($"{DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)} {step.Message}");
        IsEngineProgressIndeterminate = step.Fraction is null;
        EngineProgress = (step.Fraction ?? 0) * 100;
    }

    private async Task LoadToolsAsync()
    {
        IsBusy = true;
        try
        {
            var tools = await _cliTools.GetAsync(CancellationToken.None);
            ShowTools(tools.IsSuccess ? tools.Value : null, tools.IsFailure ? tools.Error.Message : string.Empty);
        }
        finally
        {
            IsBusy = false;
        }

        var context = _daemon.Status.Details?.DockerContext ?? string.Empty;
        ContextText = context.Length > 0
            ? $"Contr made a Docker context named {context}. Use docker --context {context}, or make it the one the "
                + $"docker command uses with docker context use {context}."
            : "The daemon made no Docker context for this engine.";
        StartAtSignIn = _signIn.IsEnabled || !_settings.Read<bool>(SetupDoneKey);
    }

    private void ShowTools(CliToolsStatus? tools, string error)
    {
        if (tools is null)
        {
            ToolsText = error;
            CanInstallTools = false;
            InstallTools = false;

            return;
        }

        var bundled = tools.Tools
            .Where(tool => tool.BundledVersion.Length > 0)
            .Select(tool => $"{tool.Name} {tool.BundledVersion}")
            .ToList();
        ToolsText = bundled.Count > 0 ? string.Join(" · ", bundled) : "This daemon bundles no command-line tools.";
        CanInstallTools = bundled.Count > 0;
        InstallTools = CanInstallTools && !tools.IsOnPath;
        ToolsNote = tools.ShadowedBy.Length > 0
            ? $"Another docker comes first on your PATH: {tools.ShadowedBy}. Typing docker runs that one."
            : string.Empty;
    }

    private async Task ApplyToolsAsync()
    {
        IsBusy = true;
        try
        {
            _signIn.IsEnabled = StartAtSignIn;

            if (InstallTools)
            {
                var installed = await _cliTools.InstallAsync(CancellationToken.None);
                Steps[3].Detail = installed.IsSuccess ? "Command-line tools installed" : installed.Error.Message;
            }
            else
            {
                Steps[3].Detail = CanInstallTools ? "Command-line tools left as they were" : "No tools to install";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Summarise()
    {
        var status = _daemon.Status;
        Summary.Clear();
        Summary.Add(status.Details is { } engine
            ? new CheckRow(StatusKind.Good, "Ready", "Engine", $"Engine {engine.Version}")
            : new CheckRow(StatusKind.Caution, "Not running", "Engine", "Start it from the tray when you need it"));
        Summary.Add(new CheckRow(StatusKind.Good, "Set", "Command-line tools", Steps[3].Detail));
        Summary.Add(StartAtSignIn
            ? new CheckRow(StatusKind.Good, "On", "Start at sign-in", "Contr starts in the notification area")
            : new CheckRow(StatusKind.Neutral, "Off", "Start at sign-in", "Off"));
    }
}
