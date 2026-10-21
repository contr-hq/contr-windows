using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.SharedKernel;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Formatting;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>One line of the system checks: what was checked, its outcome and what was found.</summary>
/// <param name="Outcome">Such as <c>Passed</c> or <c>Failed</c>; the pill's dot repeats it.</param>
/// <param name="Requirement">What passes, such as <c>At least 10 GB on C:</c>; empty where the name says it.</param>
public sealed record CheckRow(StatusKind Kind, string Outcome, string Name, string Detail, string Requirement = "")
{
    /// <summary>The fixes the daemon runs for what the check found, in the order to offer them.</summary>
    public IReadOnlyList<CheckFix> Fixes { get; init; } = [];

    public PillKind PillKind => PillView.For(Kind);

    public bool IsFailed => Kind == StatusKind.Critical;

    public static CheckRow From(DiagnosticCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        var row = check.Status switch {
            DiagnosticStatus.Pass => new CheckRow(StatusKind.Good, "Passed", check.Title, check.Detail),
            DiagnosticStatus.Warn => new CheckRow(StatusKind.Caution, "Warning", check.Title, check.Detail),
            DiagnosticStatus.Fail => new CheckRow(StatusKind.Critical, "Failed", check.Title, check.Detail),
            DiagnosticStatus.Skip => new CheckRow(StatusKind.Neutral, "Skipped", check.Title, check.Detail),
            _ => new CheckRow(StatusKind.Neutral, "Unknown", check.Title, check.Detail),
        };

        return row with { Fixes = [.. check.Remediations.Select(fix => new CheckFix(fix))] };
    }
}

/// <summary>
/// Backs Troubleshoot: why the engine is down, the daemon's checks and the fixes it offers, and the client's log.
/// </summary>
public sealed partial class TroubleshootViewModel : ViewModel
{
    /// <summary>WSL 2 needs Windows 10 version 2004, build 19041, or later.</summary>
    private const int _minimumBuild = 19041;

    private const int _logLines = 80;

    /// <summary>The first API revision with diagnostics bundles.</summary>
    private const uint _bundleRevision = 18;

    private readonly IDaemonConnection _daemon;
    private readonly IDiagnostics _diagnostics;
    private readonly IKubernetesCluster _cluster;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly DispatcherQueue? _dispatcher;
    private IDisposable? _subscription;
    private IDisposable? _usage;
    private int _checkRuns;

    /// <summary>Fixes that finished but wait for a restart or sign-out; offering them again would repeat them.
    /// </summary>
    private readonly HashSet<string> _awaitingRestart = [];

    /// <summary>What the last fix that waits for a restart or sign-out asked the user to do.</summary>
    private FixNotice? _restartNotice;

    [ObservableProperty]
    public partial bool HasProblem { get; set; }

    /// <summary>Error for the engine's problem; informational once a fix only waits for a restart.</summary>
    [ObservableProperty]
    public partial InfoBarSeverity ProblemSeverity { get; set; }

    [ObservableProperty]
    public partial string ProblemTitle { get; set; }

    [ObservableProperty]
    public partial string ProblemMessage { get; set; }

    /// <summary>The daemon's reason, such as <c>VIRTUALIZATION_DISABLED</c>; empty without one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblemCode))]
    public partial string ProblemCode { get; set; }

    /// <summary>Such as <c>ran at 10:52</c>.</summary>
    [ObservableProperty]
    public partial string ChecksSummary { get; set; }

    /// <summary>The problems and warnings the checks found, as pills; none when all passed.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PillItem> CheckPills { get; set; }

    [ObservableProperty]
    public partial bool HasLog { get; set; }

    /// <summary>The fix for the problem the engine failed with, offered on the problem bar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblemFix), nameof(ProblemFixLabel), nameof(ProblemFixNeedsApproval))]
    public partial CheckFix? ProblemFix { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFix), nameof(IsFixingProblem), nameof(IsFixingCheck))]
    public partial bool IsFixing { get; set; }

    /// <summary>Whether the running fix started from the problem bar, which then follows it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFixingProblem), nameof(IsFixingCheck))]
    public partial bool IsFixFromProblem { get; set; }

    /// <summary>Why the last fix failed while the problem bar showed; the bar is the page's one error bar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFixError))]
    public partial string FixError { get; set; }

    /// <summary>The running fix's latest step, such as <c>Installing WSL; approve the administrator prompt</c>.
    /// </summary>
    [ObservableProperty]
    public partial string FixStatus { get; set; }

    /// <summary>Whether the daemon resets the engine; it offers that only where it owns the engine.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReset))]
    public partial bool OffersReset { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReset))]
    public partial bool IsResetting { get; set; }

    /// <summary>The reset's latest step, such as <c>Stopping the engine</c>.</summary>
    [ObservableProperty]
    public partial string ResetStatus { get; set; }

    /// <summary>Whether the daemon makes diagnostics bundles.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateBundle))]
    public partial bool OffersBundle { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateBundle))]
    public partial bool IsCreatingBundle { get; set; }

    /// <summary>The bundle's latest step, such as <c>Collecting the engine's logs</c>.</summary>
    [ObservableProperty]
    public partial string BundleStatus { get; set; }

    /// <summary>Whether the daemon runs a Kubernetes cluster to reset.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanResetKubernetes))]
    public partial bool OffersKubernetes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanResetKubernetes))]
    public partial bool IsResettingKubernetes { get; set; }

    /// <summary>The space the engine's data takes, such as <c>18.6 GB</c>; empty until measured.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiskText))]
    public partial string DiskText { get; set; }

    public TroubleshootViewModel(
        IDaemonConnection daemon,
        IDiagnostics diagnostics,
        IKubernetesCluster cluster,
        IDialogService dialogs,
        INotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(cluster);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        _daemon = daemon;
        _diagnostics = diagnostics;
        _cluster = cluster;
        _dialogs = dialogs;
        _notifications = notifications;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        ProblemTitle = string.Empty;
        ProblemMessage = string.Empty;
        ProblemCode = string.Empty;
        ChecksSummary = string.Empty;
        CheckPills = [];
        ResetStatus = string.Empty;
        BundleStatus = string.Empty;
        DiskText = string.Empty;
        FixStatus = string.Empty;
        FixError = string.Empty;
        ProblemSeverity = InfoBarSeverity.Error;
    }

    public ObservableCollection<CheckRow> Checks { get; } = [];

    /// <summary>The newest lines of the client's log, oldest first.</summary>
    public ObservableCollection<ComponentLogLine> LogLines { get; } = [];

    public bool HasProblemCode => ProblemCode.Length > 0;

    public bool CanReset => OffersReset && !IsResetting;

    public bool CanCreateBundle => OffersBundle && !IsCreatingBundle;

    public bool CanResetKubernetes => OffersKubernetes && !IsResettingKubernetes;

    /// <summary>Such as <c>contr-diagnostics-2026-10-22</c>, the name the save dialog suggests.</summary>
    public static string BundleName(DateTimeOffset now)
    {
        return $"contr-diagnostics-{now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }

    public bool HasDiskText => DiskText.Length > 0;

    public bool HasProblemFix => ProblemFix is not null;

    public string ProblemFixLabel => ProblemFix?.Label ?? string.Empty;

    public bool ProblemFixNeedsApproval => ProblemFix?.RequiresElevation == true;

    public bool CanFix => !IsFixing;

    public bool IsFixingProblem => IsFixing && IsFixFromProblem;

    public bool IsFixingCheck => IsFixing && !IsFixFromProblem;

    public bool HasFixError => FixError.Length > 0;

    /// <summary>Follows the daemon and runs the checks; call it from the page's <c>Loaded</c>.</summary>
    public void Activate()
    {
        _subscription ??= _daemon.StatusChanges.Subscribe(status => {
            // StatusChanges emits on a background thread.
            if (_dispatcher is null)
            {
                Run(status);
            }
            else
            {
                _dispatcher.TryEnqueue(() => Run(status));
            }
        });
        _usage ??= _daemon.UsageChanges.Subscribe(usage => {
            var disk = usage is { DiskUsedBytes: > 0 } ? HumanFormat.Bytes(usage.DiskUsedBytes) : null;

            if (disk is not null)
            {
                _dispatcher?.TryEnqueue(() => DiskText = disk);
            }
        });
    }

    public void Deactivate()
    {
        _subscription?.Dispose();
        _subscription = null;
        _usage?.Dispose();
        _usage = null;
    }

    /// <summary>Asks first, then replaces the engine's runtime; images, containers and volumes stay.</summary>
    public async Task ResetRuntimeAsync()
    {
        const string message = "Contr stops the engine and its containers and replaces the engine's runtime. "
            + "Images, containers and volumes stay. Start the engine again when it's done.";

        if (!await _dialogs.ShowConfirmAsync("Reset the engine", message, "Reset", "Cancel"))
        {
            return;
        }

        if (await ResetAsync(EngineResetScope.Runtime) is { } error)
        {
            _notifications.Show(error, InfoBarSeverity.Error, "The engine wasn't reset");
        }
        else
        {
            _notifications.Show("The engine was reset. Start it when you need it.", InfoBarSeverity.Success);
        }
    }

    /// <summary>Writes the daemon's diagnostics bundle to <paramref name="destination"/>, following its steps in
    /// <see cref="BundleStatus"/>.</summary>
    /// <param name="path">Where the bundle goes, for the notice.</param>
    /// <returns>Whether the bundle is whole; the caller deletes a file that is not.</returns>
    public async Task<bool> CreateBundleAsync(Stream destination, string path)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(path);

        IsCreatingBundle = true;
        BundleStatus = "Collecting";
        try
        {
            var progress = new Progress<EngineProgress>(step => BundleStatus = step.Message);
            var result = await _diagnostics.CreateBundleAsync(destination, progress, CancellationToken.None);

            if (result.IsFailure)
            {
                _notifications.Show(
                    result.Error.Message, InfoBarSeverity.Error, "The diagnostics bundle wasn't created");

                return false;
            }

            _notifications.Show($"Saved the diagnostics bundle to {path}.", InfoBarSeverity.Success);

            return true;
        }
        finally
        {
            IsCreatingBundle = false;
        }
    }

    /// <summary>Asks first, then deletes the cluster's workloads and data; it starts anew while Kubernetes is on.
    /// </summary>
    public async Task ResetKubernetesAsync()
    {
        const string message = "Deletes every workload and the cluster's data; a new cluster starts while Kubernetes "
            + "is on. Your images stay.";

        if (!await _dialogs.ShowConfirmAsync("Reset Kubernetes", message, "Reset", "Cancel"))
        {
            return;
        }

        IsResettingKubernetes = true;
        try
        {
            var result = await _cluster.ResetAsync(CancellationToken.None);

            if (result.IsFailure)
            {
                _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "Kubernetes wasn't reset");
            }
            else
            {
                _notifications.Show("The cluster was reset.", InfoBarSeverity.Success);
            }
        }
        finally
        {
            IsResettingKubernetes = false;
        }
    }

    public void ShowFactoryResetDone()
    {
        _notifications.Show(
            "Contr deleted the engine and its data. Starting the engine sets it up again.", InfoBarSeverity.Success);
    }

    /// <summary>Resets the engine, following its steps in <see cref="ResetStatus"/>.</summary>
    /// <returns>Why the reset failed; absent once it is done.</returns>
    public async Task<string?> ResetAsync(EngineResetScope scope)
    {
        IsResetting = true;
        ResetStatus = "Stopping the engine";
        try
        {
            var progress = new Progress<EngineProgress>(step => ResetStatus = step.Message);
            var result = await _daemon.ResetEngineAsync(scope, progress, CancellationToken.None);

            return result.IsSuccess ? null : result.Error.Message;
        }
        finally
        {
            IsResetting = false;
        }
    }

    public void RunAgain()
    {
        Run(_daemon.Status);
    }

    /// <summary>Runs a fix a check offered, following it in <see cref="FixStatus"/>, then checks again.</summary>
    /// <remarks>While the problem bar shows, the bar tells how the fix ended; otherwise a notification does.
    /// </remarks>
    public async Task FixAsync(CheckFix fix)
    {
        ArgumentNullException.ThrowIfNull(fix);

        if (IsFixing)
        {
            return;
        }

        IsFixFromProblem = fix == ProblemFix;
        IsFixing = true;
        FixStatus = fix.Running;
        FixError = string.Empty;
        Result<RemediationOutcome> result;
        try
        {
            var progress = new Progress<EngineProgress>(step => FixStatus = step.Message);
            result = await _diagnostics.RunRemediationAsync(fix.ActionId, progress, CancellationToken.None);
        }
        finally
        {
            IsFixing = false;
        }

        var notice = fix.Notice(result);
        var waitsForRestart = result.IsSuccess && (result.Value.RestartRequired || result.Value.SignOutRequired);

        if (waitsForRestart)
        {
            _awaitingRestart.Add(fix.ActionId);
            _restartNotice = notice;
        }

        if (!HasProblem || (result.IsSuccess && !waitsForRestart))
        {
            _notifications.Show(notice.Message, notice.Severity, notice.Title);
        }
        else if (result.IsFailure)
        {
            FixError = $"{notice.Title}. {notice.Message}";
        }

        RunAgain();
    }

    /// <summary>The daemon's checks after the one only the client makes: the Windows build.</summary>
    public static CheckRow[] Combine(int windowsBuild, IReadOnlyList<DiagnosticCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);

        return [WindowsCheck(windowsBuild), .. checks.Select(CheckRow.From)];
    }

    /// <summary>The checks the client makes itself where the daemon makes none: it is unreachable, or older than
    /// API revision 8.</summary>
    public static CheckRow[] Evaluate(DaemonStatus status, DaemonIdentity? identity, int windowsBuild)
    {
        ArgumentNullException.ThrowIfNull(status);

        return [
            WindowsCheck(windowsBuild),
            DaemonCheck(status, identity),
            EngineCheck(status),
            ContextCheck(status),
        ];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Deactivate();
        }
    }

    /// <summary>Passes the Windows build WSL 2 needs.</summary>
    public static CheckRow WindowsCheck(int build)
    {
        var name = build >= 22000 ? "Windows 11" : "Windows 10";
        var detail = $"{name} · build {build.ToString(CultureInfo.CurrentCulture)}";

        return build >= _minimumBuild
            ? new CheckRow(StatusKind.Good, "Passed", "Windows version", detail)
            : new CheckRow(StatusKind.Critical, "Failed", "Windows version", $"{detail} · WSL 2 needs build 19041");
    }

    private static CheckRow DaemonCheck(DaemonStatus status, DaemonIdentity? identity)
    {
        return status.Connection switch {
            DaemonConnectionState.Connected => new CheckRow(
                StatusKind.Good,
                "Passed",
                "Contr daemon",
                identity is null ? "Connected" : $"{identity.Version} · {identity.Backend}"),
            DaemonConnectionState.Connecting => new CheckRow(StatusKind.Busy, "Checking", "Contr daemon", "Connecting"),
            _ => new CheckRow(StatusKind.Critical, "Failed", "Contr daemon", status.Problem),
        };
    }

    private static CheckRow EngineCheck(DaemonStatus status)
    {
        if (status.Connection != DaemonConnectionState.Connected)
        {
            return new CheckRow(StatusKind.Neutral, "Skipped", "Engine", "The daemon isn't reachable");
        }

        return status.Engine switch {
            EngineState.Running => new CheckRow(StatusKind.Good, "Passed", "Engine", RunningEngine(status)),
            EngineState.Failed => new CheckRow(StatusKind.Critical, "Failed", "Engine", status.Problem),
            EngineState.Stopped or EngineState.Uninitialized =>
                new CheckRow(StatusKind.Caution, "Warning", "Engine", "Stopped"),
            _ => new CheckRow(StatusKind.Busy, "Checking", "Engine", status.Engine.ToString()),
        };
    }

    private static string RunningEngine(DaemonStatus status)
    {
        return status.Details is { } engine ? $"Engine {engine.Version}" : "Running";
    }

    private static CheckRow ContextCheck(DaemonStatus status)
    {
        if (status.Engine != EngineState.Running || status.Details is not { } engine)
        {
            return new CheckRow(StatusKind.Neutral, "Skipped", "Docker context", "Engine isn't running");
        }

        return engine.DockerContext.Length > 0
            ? new CheckRow(StatusKind.Good, "Passed", "Docker context", engine.DockerContext)
            : new CheckRow(StatusKind.Caution, "Warning", "Docker context", "The daemon made none");
    }

    /// <summary>The last lines of the newest client log; empty when there is none.</summary>
    private static string[] ReadLog()
    {
        try
        {
            var newest = new DirectoryInfo(Services.Platform.FileLoggerProvider.LogDirectory)
                .EnumerateFiles("app-*.log")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            if (newest is null)
            {
                return [];
            }

            using var stream = new FileStream(newest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            return reader
                .ReadToEnd()
                .TrimEnd()
                .Split('\n')
                .TakeLast(_logLines)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Run(DaemonStatus status)
    {
        OffersReset = _daemon.Identity?.Capabilities.Contains(DaemonCapability.Reset) == true;
        OffersBundle = _daemon.Identity?.ApiRevision >= _bundleRevision;
        OffersKubernetes = _daemon.Identity?.Capabilities.Contains(DaemonCapability.Kubernetes) == true;
        HasProblem = status.Connection == DaemonConnectionState.Unavailable || status.Engine == EngineState.Failed;
        ProblemTitle = status.Connection == DaemonConnectionState.Unavailable
            ? "Contr's background service isn't reachable"
            : "The engine couldn't start";
        ProblemMessage = status.Problem;
        ProblemCode = status.Engine == EngineState.Failed ? status.ProblemCode : string.Empty;
        ProblemSeverity = InfoBarSeverity.Error;

        if (HasProblem && _restartNotice is { } restart)
        {
            ProblemSeverity = restart.Severity;
            ProblemTitle = restart.Title ?? string.Empty;
            ProblemMessage = restart.Message;
            ProblemCode = string.Empty;
        }

        LogLines.Clear();

        foreach (var line in ReadLog())
        {
            LogLines.Add(ComponentLogLine.Parse(line));
        }

        HasLog = LogLines.Count > 0;
        _ = CheckAsync(status);
    }

    /// <summary>Shows the daemon's checks, or the client's own where the daemon makes none.</summary>
    /// <remarks>Of overlapping runs, only the latest shows.</remarks>
    private async Task CheckAsync(DaemonStatus status)
    {
        var run = ++_checkRuns;
        var build = Environment.OSVersion.Version.Build;
        var checks = Evaluate(status, _daemon.Identity, build);

        if (status.Connection == DaemonConnectionState.Connected)
        {
            ChecksSummary = "checking…";
            var daemonChecks = await _diagnostics.RunChecksAsync(CancellationToken.None);

            if (run != _checkRuns)
            {
                return;
            }

            if (daemonChecks.IsSuccess)
            {
                checks = Combine(build, daemonChecks.Value);
            }
        }

        Show(checks);
    }

    /// <summary>Lists the checks; the engine's failure offers its fix on the problem bar instead of its row.</summary>
    private void Show(CheckRow[] checks)
    {
        checks = [.. checks.Select(check => check with {
            Fixes = [.. check.Fixes.Where(fix => !_awaitingRestart.Contains(fix.ActionId))],
        })];
        var problemFix = HasProblem
            ? checks.FirstOrDefault(check => check.IsFailed && check.Fixes.Count > 0)?.Fixes[0]
            : null;
        Checks.Clear();

        foreach (var check in checks)
        {
            Checks.Add(check with { Fixes = [.. check.Fixes.Where(fix => fix != problemFix)] });
        }

        ProblemFix = problemFix;
        var problems = checks.Count(check => check.Kind == StatusKind.Critical);
        var warnings = checks.Count(check => check.Kind == StatusKind.Caution);
        var ranAt = DateTimeOffset.Now.ToString("t", CultureInfo.CurrentCulture);
        ChecksSummary = $"ran at {ranAt}";
        List<PillItem> pills = [];

        if (problems > 0)
        {
            pills.Add(new(Plural(problems, "problem"), PillKind.Critical));
        }

        if (warnings > 0)
        {
            pills.Add(new(Plural(warnings, "warning"), PillKind.Caution));
        }

        CheckPills = pills;
    }

    private static string Plural(int count, string noun)
    {
        return count == 1 ? $"1 {noun}" : $"{count.ToString(CultureInfo.CurrentCulture)} {noun}s";
    }
}
