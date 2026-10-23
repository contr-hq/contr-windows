using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>One operation in the activity view.</summary>
/// <param name="Title">Such as <c>Pull postgres:17</c>.</param>
/// <param name="Detail">The latest step, or how and when it ended.</param>
public sealed record ActivityRow(string Id, string Title, string Detail, OperationState State)
{
    /// <summary>The share done, 0 to 1; absent while the daemon cannot tell.</summary>
    public double? Fraction { get; init; }

    public bool CanCancel { get; init; }

    /// <summary>The fixes the daemon offers for the failure.</summary>
    public IReadOnlyList<CheckFix> Fixes { get; init; } = [];

    /// <summary>A finished bundle, whose zip the daemon keeps until it drops the operation.</summary>
    public bool CanSave { get; init; }

    public bool IsRunning => State == OperationState.Running;

    public bool IsIndeterminate => Fraction is null;

    public double Percent => (Fraction ?? 0) * 100;

    public bool HasFixes => Fixes.Count > 0;

    public bool IsSucceeded => State == OperationState.Succeeded;

    public bool IsFailed => State == OperationState.Failed;

    public bool IsCancelled => State == OperationState.Cancelled;

    public static ActivityRow From(DaemonOperation operation, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return new ActivityRow(
            operation.Id, ActivityText.Title(operation.Work), ActivityText.Detail(operation, now), operation.State) {
            Fraction = operation.IsRunning ? operation.Progress?.Fraction : null,
            CanCancel = operation is { IsRunning: true, IsCancellable: true },
            Fixes = operation.Failure is { Remediations.Count: > 0 } failure
                ? [.. failure.Remediations.Select(fix => new CheckFix(fix))]
                : [],
            CanSave = operation is { State: OperationState.Succeeded, Work: OperationWork.CreateBundle },
        };
    }
}

/// <summary>
/// The status bar's activity view: the operations the daemon runs and ran, from any page or client, with Cancel,
/// the fixes a failure offers, and Save… for a finished bundle.
/// </summary>
public sealed partial class ActivityViewModel : ViewModel
{
    private readonly IActivity _activity;
    private readonly IDiagnostics _diagnostics;
    private readonly IDaemonConnection _daemon;
    private readonly INotificationService _notifications;
    private readonly DispatcherQueue? _dispatcher;
    private IDisposable? _operations;
    private IDisposable? _status;

    /// <summary>Whether the daemon runs work as operations; the view shows only then.</summary>
    [ObservableProperty]
    public partial bool IsOffered { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunning), nameof(IndicatorText))]
    public partial int RunningCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool HasRows { get; set; }

    public ActivityViewModel(
        IActivity activity, IDiagnostics diagnostics, IDaemonConnection daemon, INotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(notifications);
        _activity = activity;
        _diagnostics = diagnostics;
        _daemon = daemon;
        _notifications = notifications;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>Running operations first, then the finished ones the daemon keeps for an hour, newest first.</summary>
    public ObservableCollection<ActivityRow> Rows { get; } = [];

    public bool HasRunning => RunningCount > 0;

    public bool IsEmpty => !HasRows;

    /// <summary>Such as <c>2 running</c>.</summary>
    public string IndicatorText => RunningCount == 0
        ? "Activity"
        : $"{RunningCount.ToString(CultureInfo.CurrentCulture)} running";

    /// <summary>Follows the daemon's operations; call it when the shell loads.</summary>
    public void Start()
    {
        _status ??= _daemon.StatusChanges.Subscribe(_ => OnUi(() =>
            IsOffered = _daemon.Identity?.ApiRevision >= IActivity.OperationsRevision));
        _operations ??= _activity.Changes.Subscribe(operations => OnUi(() => Show(operations, DateTimeOffset.Now)));
    }

    public void Stop()
    {
        _status?.Dispose();
        _status = null;
        _operations?.Dispose();
        _operations = null;
    }

    /// <summary>Shows the operations, the running ones first; call it on the UI thread.</summary>
    public void Show(IReadOnlyList<DaemonOperation> operations, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(operations);

        List<ActivityRow> rows = [.. operations
            .OrderByDescending(operation => operation.IsRunning)
            .Select(operation => ActivityRow.From(operation, now))
            .Select(row => Rows.FirstOrDefault(existing => existing == row) ?? row)];
        CollectionSync.Sync(Rows, rows, row => row.Id);
        RunningCount = rows.Count(row => row.IsRunning);
        HasRows = rows.Count > 0;
    }

    public async Task CancelAsync(ActivityRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var result = await _activity.CancelAsync(row.Id, CancellationToken.None);

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, $"{row.Title} wasn't cancelled");
        }
    }

    /// <summary>Runs a fix the failure offers; the fix shows in the view as it runs.</summary>
    public async Task FixAsync(CheckFix fix)
    {
        ArgumentNullException.ThrowIfNull(fix);

        var result = await _diagnostics.RunRemediationAsync(
            fix.ActionId, new Progress<EngineProgress>(), CancellationToken.None);
        var notice = fix.Notice(result);
        _notifications.Show(notice.Message, notice.Severity, notice.Title);
    }

    /// <summary>Writes a finished bundle's zip to <paramref name="destination"/>.</summary>
    /// <param name="path">Where the bundle goes, for the notice.</param>
    /// <returns>Whether the zip is whole; the caller deletes a file that is not.</returns>
    public async Task<bool> SaveBundleAsync(ActivityRow row, Stream destination, string path)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(path);

        var result = await _activity.SaveBundleAsync(
            row.Id, destination, new Progress<EngineProgress>(), CancellationToken.None);

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "The diagnostics bundle wasn't saved");

            return false;
        }

        _notifications.Show($"Saved the diagnostics bundle to {path}.", InfoBarSeverity.Success);

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
        }
    }

    private void OnUi(Action action)
    {
        // Both streams emit on a background thread.
        if (_dispatcher is null)
        {
            action();
        }
        else
        {
            _dispatcher.TryEnqueue(() => action());
        }
    }
}
