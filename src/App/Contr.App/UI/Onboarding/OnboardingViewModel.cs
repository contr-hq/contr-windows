using System;
using System.Collections.Generic;
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
using Contr.SharedKernel;
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

    /// <summary>The setting that makes the next setup start at the engine, once WSL needs only a restart.</summary>
    public const string ResumeAtEngineKey = "SetupResumeAtEngine";

    /// <summary>The command Microsoft documents for installing WSL without a Linux distribution.</summary>
    public const string InstallWslCommand = "wsl --install --no-distribution";

    /// <summary>A first command to try once setup is done.</summary>
    public const string TryCommand = "docker run --rm hello-world";

    private const string _wslMissing = "WSL_NOT_INSTALLED";
    private const long _lowDiskBytes = 10_000_000_000;

    private static readonly CheckFix _installWsl = new(new Remediation(Remediation.InstallWslId, true));

    private readonly IDaemonConnection _daemon;
    private readonly IDiagnostics _diagnostics;
    private readonly ICliTools _cliTools;
    private readonly IWslDistributions _wsl;
    private readonly ISignInStartup _signIn;
    private readonly ILocalSettingsService _settings;
    private readonly IClipboardService _clipboard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepCaption), nameof(StepNumber), nameof(StepTitle), nameof(StepLead))]
    [NotifyPropertyChangedFor(nameof(IsChecking), nameof(IsWsl), nameof(IsEngine))]
    [NotifyPropertyChangedFor(nameof(IsTools), nameof(IsReady), nameof(CanGoBack), nameof(NextText))]
    [NotifyPropertyChangedFor(nameof(CanPostpone), nameof(NextNeedsApproval), nameof(CanGoNext))]
    public partial int StepIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext), nameof(IsIdle), nameof(NextText))]
    public partial bool IsBusy { get; set; }

    /// <summary>Whether the daemon found WSL missing, so setup offers to install it.</summary>
    [ObservableProperty]
    public partial bool IsWslMissing { get; set; }

    /// <summary>Why installing WSL failed; empty unless it did.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWslError))]
    public partial string WslError { get; set; }

    /// <summary>The install's latest step, such as <c>Installing WSL; approve the administrator prompt</c>.</summary>
    [ObservableProperty]
    public partial string WslStatus { get; set; }

    /// <summary>WSL is installed, and Windows must restart or the user sign out before the engine can use it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack), nameof(NextText), nameof(CanPostpone), nameof(NextNeedsApproval))]
    public partial bool IsWslRestartNeeded { get; set; }

    /// <summary>Such as <c>Restart Windows to finish</c>; empty until WSL is installed.</summary>
    [ObservableProperty]
    public partial string WslDoneTitle { get; set; }

    [ObservableProperty]
    public partial string WslDoneMessage { get; set; }

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

    /// <summary>Whether the daemon integrates WSL distributions and the user has any besides Contr's own.</summary>
    [ObservableProperty]
    public partial bool HasDistributions { get; set; }

    /// <summary>Which distributions reach the engine once setup finishes, for the summary.</summary>
    [ObservableProperty]
    public partial string WslText { get; set; }

    [ObservableProperty]
    public partial string ContextText { get; set; }

    [ObservableProperty]
    public partial bool StartAtSignIn { get; set; }

    public OnboardingViewModel(
        IDaemonConnection daemon,
        IDiagnostics diagnostics,
        ICliTools cliTools,
        IWslDistributions wsl,
        ISignInStartup signIn,
        ILocalSettingsService settings,
        IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(cliTools);
        ArgumentNullException.ThrowIfNull(wsl);
        ArgumentNullException.ThrowIfNull(signIn);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clipboard);
        _daemon = daemon;
        _diagnostics = diagnostics;
        _cliTools = cliTools;
        _wsl = wsl;
        _signIn = signIn;
        _settings = settings;
        _clipboard = clipboard;
        EngineError = string.Empty;
        WslError = string.Empty;
        WslStatus = string.Empty;
        WslDoneTitle = string.Empty;
        WslDoneMessage = string.Empty;
        ToolsText = string.Empty;
        ToolsNote = string.Empty;
        ContextText = string.Empty;
        WslText = string.Empty;
        StartAtSignIn = true;
        Steps = [
            new OnboardingStep(1, "Check this PC"),
            new OnboardingStep(2, "Install WSL"),
            new OnboardingStep(3, "Set up the engine"),
            new OnboardingStep(4, "Connect your tools"),
            new OnboardingStep(5, "Ready"),
        ];
    }

    /// <summary>Raised when the user finishes or leaves setup for later; the window closes.</summary>
    public event EventHandler? Finished;

    public ObservableCollection<OnboardingStep> Steps { get; }

    public ObservableCollection<CheckRow> Checks { get; } = [];

    public ObservableCollection<EngineSetupStep> EngineSteps { get; } = [];

    public ObservableCollection<string> EngineLog { get; } = [];

    public ObservableCollection<CheckRow> Summary { get; } = [];

    /// <summary>Such as <c>STEP 4 OF 5 · CONNECT YOUR TOOLS</c>.</summary>
    public string StepCaption =>
        $"STEP {StepNumber} OF {Steps.Count} · {Steps[StepIndex].Title.ToUpperInvariant()}";

    /// <summary>The step from 1, for the progress bar.</summary>
    public int StepNumber => StepIndex + 1;

    public string StepTitle => StepIndex switch {
        0 => "Welcome to Contr",
        1 => "Install WSL",
        2 => "Setting up the engine",
        3 => "Connect your tools",
        _ => "You're all set",
    };

    public string StepLead => StepIndex switch {
        0 => "Contr runs containers on this PC with a Docker-compatible engine. First, a quick check that this PC "
            + "is ready.",
        1 => "Contr runs its engine inside WSL 2, the Linux layer built into Windows.",
        2 => "You can keep working while this runs. The tray icon shows when the engine is ready.",
        3 => "Choose how the docker command and your other tools reach the engine. You can change all of this "
            + "later in Settings.",
        _ => "Contr stays in the notification area when you close its window.",
    };

    /// <summary>The user's WSL distributions; the checked ones reach the engine once setup applies step 4.</summary>
    public ObservableCollection<WslDistributionItem> Distributions { get; } = [];

    public bool IsChecking => StepIndex == 0;

    public bool IsWsl => StepIndex == 1;

    public bool IsEngine => StepIndex == 2;

    public bool IsTools => StepIndex == 3;

    public bool IsReady => StepIndex == 4;

    public bool CanGoBack => StepIndex is > 0 and < 4 && !IsWslRestartNeeded;

    public bool CanGoNext => !IsBusy && (StepIndex != 2 || IsEngineRunning);

    public bool IsIdle => !IsBusy;

    /// <summary>Installing WSL can wait; setup opens again the next time Contr starts.</summary>
    public bool CanPostpone => IsWsl && !IsWslRestartNeeded;

    /// <summary>The next step asks Windows for administrator approval, so its button carries the shield.</summary>
    public bool NextNeedsApproval => IsWsl && !IsWslRestartNeeded;

    public string NextText => StepIndex switch {
        0 when IsBusy => "Checking…",
        1 when IsWslRestartNeeded => "Close",
        1 => _installWsl.Label,
        3 => "Finish setup",
        4 => "Open Contr",
        _ => "Next",
    };

    public bool HasEngineError => EngineError.Length > 0;

    public bool HasWslError => WslError.Length > 0;

    /// <summary>The title over <see cref="WslError"/>.</summary>
    public static string WslErrorTitle => _installWsl.FailedTitle;

    public bool HasToolsNote => ToolsNote.Length > 0;

    /// <summary>Shows the first step, or the engine's after a restart that installing WSL needed; call it once the
    /// window is up.</summary>
    public async Task StartAsync()
    {
        RunChecks();

        if (_settings.Read<bool>(ResumeAtEngineKey))
        {
            _settings.Save(ResumeAtEngineKey, false);
            Steps[1].Detail = "WSL installed";
            Show(2);
            await StartEngineAsync();

            return;
        }

        Show(0);
    }

    public async Task NextAsync()
    {
        switch (StepIndex)
        {
            case 0:
                await CheckWslAsync();
                break;
            case 1 when IsWslRestartNeeded:
                Finished?.Invoke(this, EventArgs.Empty);
                break;
            case 1:
                await InstallWslAsync();
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

    /// <summary>Goes a step back, past Install WSL where WSL is there.</summary>
    public void Back()
    {
        if (CanGoBack)
        {
            Show(StepIndex == 2 && !IsWslMissing ? 0 : StepIndex - 1);
        }
    }

    /// <summary>Leaves setup without finishing it; it opens again the next time Contr starts.</summary>
    public void Postpone()
    {
        Finished?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Whether the daemon's checks offer to install WSL for a prerequisite they found missing.</summary>
    public static bool NeedsWsl(IReadOnlyList<DiagnosticCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);

        return checks.Any(check => check is { Id: DiagnosticCheck.PrerequisitesId, Status: DiagnosticStatus.Fail }
            && check.Remediations.Any(fix => fix.ActionId == Remediation.InstallWslId));
    }

    /// <summary>What to send whoever can install WSL on this PC.</summary>
    public static string ItInstructions(string machineName, int windowsBuild)
    {
        var build = windowsBuild.ToString(CultureInfo.InvariantCulture);

        return $"""
            Contr needs WSL 2 on {machineName}, and installing it needs administrator rights.

            In a terminal opened as administrator, run the command below, then restart the PC if Windows asks.
            It installs WSL without a Linux distribution.

                {InstallWslCommand}

            WSL 2 needs Windows 10 build 19041 or later; this PC runs build {build}.
            Microsoft's guide: https://learn.microsoft.com/windows/wsl/install
            """.ReplaceLineEndings();
    }

    public async Task RetryEngineAsync()
    {
        await StartEngineAsync();
    }

    public void CopyItInstructions()
    {
        _clipboard.SetText(ItInstructions(Environment.MachineName, Environment.OSVersion.Version.Build));
    }

    public void CopyTryCommand()
    {
        _clipboard.SetText(TryCommand);
    }

    /// <summary>The checks the client makes itself: the Windows build, virtualization and free disk space.</summary>
    public static CheckRow[] CheckThisPc(int windowsBuild, bool? virtualization, long freeBytes, string drive)
    {
        var free = $"{HumanFormat.Bytes(freeBytes)} free on {drive}";

        return [
            TroubleshootViewModel.WindowsCheck(windowsBuild) with {
                Requirement = "WSL 2 needs Windows 10 build 19041 or later",
            },
            virtualization switch {
                true => new CheckRow(StatusKind.Good, "Passed", "Virtualization", "Turned on"),
                false => new CheckRow(StatusKind.Critical, "Failed", "Virtualization", "Turned off in the firmware"),
                null => new CheckRow(StatusKind.Neutral, "Unknown", "Virtualization", "The engine's start checks it"),
            } with {
                Requirement = "WSL 2 needs it turned on in the firmware",
            },
            (freeBytes >= _lowDiskBytes
                ? new CheckRow(StatusKind.Good, "Passed", "Free disk space", free)
                : new CheckRow(StatusKind.Caution, "Warning", "Free disk space", $"{free} · images need room")) with {
                Requirement = $"At least {HumanFormat.Bytes(_lowDiskBytes)} for images",
            },
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

    /// <summary>Asks the daemon whether WSL is missing; Install WSL shows only where it is.</summary>
    private async Task CheckWslAsync()
    {
        IsBusy = true;
        try
        {
            var checks = await _diagnostics.RunChecksAsync(CancellationToken.None);

            // A daemon older than API revision 8 makes no checks; starting the engine tells instead.
            IsWslMissing = checks.IsSuccess && NeedsWsl(checks.Value);
        }
        finally
        {
            IsBusy = false;
        }

        if (IsWslMissing)
        {
            Steps[1].Detail = "Needs administrator approval";
            Show(1);

            return;
        }

        Steps[1].Detail = "Nothing to install";
        Show(2);
        await StartEngineAsync();
    }

    private async Task InstallWslAsync()
    {
        IsBusy = true;
        WslError = string.Empty;
        WslStatus = _installWsl.Running;
        Result<RemediationOutcome> result;
        try
        {
            var progress = new Progress<EngineProgress>(step => WslStatus = step.Message);
            result = await _diagnostics.RunRemediationAsync(Remediation.InstallWslId, progress, CancellationToken.None);
        }
        finally
        {
            IsBusy = false;
        }

        var notice = _installWsl.Notice(result);

        if (result.IsFailure)
        {
            WslError = notice.Message;

            return;
        }

        Steps[1].Detail = "WSL installed";

        if (result.Value.RestartRequired || result.Value.SignOutRequired)
        {
            _settings.Save(ResumeAtEngineKey, true);
            WslDoneTitle = notice.Title ?? string.Empty;
            WslDoneMessage = $"{notice.Message} Open Contr afterwards, and setup continues at step 3.";
            IsWslRestartNeeded = true;

            return;
        }

        IsWslMissing = false;
        Show(2);
        await StartEngineAsync();
    }

    private async Task StartEngineAsync()
    {
        if (_daemon.Status.Engine == EngineState.Running)
        {
            IsEngineRunning = true;
            Steps[1].Detail = "Nothing to install";
            Steps[2].Detail = "The engine was already running";
            EngineSteps.Clear();
            EngineSteps.Add(new EngineSetupStep("The engine is already running") { IsDone = true });
            EngineProgress = 100;

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
        await LoadDistributionsAsync();
    }

    private async Task LoadDistributionsAsync()
    {
        Distributions.Clear();
        HasDistributions = false;

        if (_daemon.Identity?.Capabilities.Contains(DaemonCapability.WslIntegration) != true)
        {
            return;
        }

        var listed = await _wsl.ListAsync(CancellationToken.None);

        if (listed.IsFailure)
        {
            return;
        }

        foreach (var distribution in WslDistributionItem.Ordered(listed.Value))
        {
            Distributions.Add(new WslDistributionItem(distribution));
        }

        // The default distribution starts out checked while none has engine access yet.
        if (!Distributions.Any(item => item.IsOn) && Distributions.FirstOrDefault(Offered) is { } first)
        {
            first.SetQuietly(true);
        }

        HasDistributions = Distributions.Count > 0;
    }

    private static bool Offered(WslDistributionItem item)
    {
        return item.CanChange && item.Distribution.IsDefault;
    }

    private async Task ApplyDistributionsAsync()
    {
        var failures = new List<string>();

        foreach (var item in Distributions.Where(item => item.CanChange))
        {
            if (item.IsOn == WslDistributionItem.IsIntegrated(item.Distribution))
            {
                continue;
            }

            var result = await _wsl.SetIntegrationAsync(item.Name, item.IsOn, CancellationToken.None);

            if (result.IsSuccess)
            {
                item.Show(result.Value);
            }
            else
            {
                failures.Add($"{item.Name}: {result.Error.Message}");
            }
        }

        var on = Distributions
            .Where(item => WslDistributionItem.IsIntegrated(item.Distribution))
            .Select(item => item.Name)
            .ToList();
        WslText = failures.Count > 0
            ? string.Join(" · ", failures)
            : on.Count > 0 ? string.Join(", ", on) : "None";
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

            await ApplyDistributionsAsync();
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

        if (HasDistributions)
        {
            Summary.Add(new CheckRow(StatusKind.Good, "Set", "WSL distros", WslText));
        }

        Summary.Add(StartAtSignIn
            ? new CheckRow(StatusKind.Good, "On", "Start at sign-in", "Contr starts in the notification area")
            : new CheckRow(StatusKind.Neutral, "Off", "Start at sign-in", "Off"));
    }
}
