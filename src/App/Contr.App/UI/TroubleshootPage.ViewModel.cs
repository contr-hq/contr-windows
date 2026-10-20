using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.UI.Dispatching;

namespace Contr.App.UI;

/// <summary>One line of the system checks: what was checked, its outcome and what was found.</summary>
/// <param name="Outcome">Such as <c>Passed</c> or <c>Failed</c>; the icon repeats it.</param>
public sealed record CheckRow(StatusKind Kind, string Outcome, string Name, string Detail);

/// <summary>Backs Troubleshoot: why the engine is down, the checks the client can make, and the client's log.</summary>
public sealed partial class TroubleshootViewModel : ViewModel
{
    /// <summary>WSL 2 needs Windows 10 version 2004, build 19041, or later.</summary>
    private const int _minimumBuild = 19041;

    private const int _logLines = 80;

    private readonly IDaemonConnection _daemon;
    private readonly DispatcherQueue? _dispatcher;
    private IDisposable? _subscription;

    [ObservableProperty]
    public partial bool HasProblem { get; set; }

    [ObservableProperty]
    public partial string ProblemTitle { get; set; }

    [ObservableProperty]
    public partial string ProblemMessage { get; set; }

    /// <summary>The daemon's reason, such as <c>VIRTUALIZATION_DISABLED</c>; empty without one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblemCode))]
    public partial string ProblemCode { get; set; }

    /// <summary>Such as <c>ran at 10:52 · 1 problem</c>.</summary>
    [ObservableProperty]
    public partial string ChecksSummary { get; set; }

    [ObservableProperty]
    public partial bool HasLog { get; set; }

    public TroubleshootViewModel(IDaemonConnection daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        ProblemTitle = string.Empty;
        ProblemMessage = string.Empty;
        ProblemCode = string.Empty;
        ChecksSummary = string.Empty;
    }

    public ObservableCollection<CheckRow> Checks { get; } = [];

    /// <summary>The newest lines of the client's log, oldest first.</summary>
    public ObservableCollection<ComponentLogLine> LogLines { get; } = [];

    public bool HasProblemCode => ProblemCode.Length > 0;

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
    }

    public void Deactivate()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    public void RunAgain()
    {
        Run(_daemon.Status);
    }

    /// <summary>The checks the client can make itself from the daemon's status and this PC.</summary>
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
        var checks = Evaluate(status, _daemon.Identity, Environment.OSVersion.Version.Build);
        Checks.Clear();
        foreach (var check in checks)
        {
            Checks.Add(check);
        }

        var problems = checks.Count(check => check.Kind == StatusKind.Critical);
        var warnings = checks.Count(check => check.Kind == StatusKind.Caution);
        var ranAt = DateTimeOffset.Now.ToString("t", CultureInfo.CurrentCulture);
        ChecksSummary = $"ran at {ranAt} · {Plural(problems, "problem")}, {Plural(warnings, "warning")}";

        HasProblem = status.Connection == DaemonConnectionState.Unavailable || status.Engine == EngineState.Failed;
        ProblemTitle = status.Connection == DaemonConnectionState.Unavailable
            ? "Contr's background service isn't reachable"
            : "The engine couldn't start";
        ProblemMessage = status.Problem;
        ProblemCode = status.Engine == EngineState.Failed ? status.ProblemCode : string.Empty;
        LogLines.Clear();

        foreach (var line in ReadLog())
        {
            LogLines.Add(ComponentLogLine.Parse(line));
        }

        HasLog = LogLines.Count > 0;
    }

    private static string Plural(int count, string noun)
    {
        return count == 1 ? $"1 {noun}" : $"{count.ToString(CultureInfo.CurrentCulture)} {noun}s";
    }
}
