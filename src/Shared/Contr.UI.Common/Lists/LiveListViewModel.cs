using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Application.Abstractions.Daemon;
using Contr.Application.Abstractions.Watching;
using Contr.UI.Contracts;

namespace Contr.UI.Common.Lists;

/// <summary>
/// A page's view model over a watched list: it follows the stream while the page is loaded and
/// watches again after the stream breaks.
/// </summary>
/// <remarks>
/// Start watching on the UI thread: the loop resumes on the captured context, so the rows change
/// only on that thread.
/// </remarks>
public abstract partial class LiveListViewModel<TItem, TRow> : ViewModel
    where TRow : class
{
    private readonly LiveCollection<TItem, TRow> _list;
    private readonly TimeSpan _retryDelay;
    private CancellationTokenSource? _watch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; set; }

    [ObservableProperty]
    public partial bool IsEngineDown { get; set; }

    protected LiveListViewModel(LiveCollection<TItem, TRow> list, TimeSpan retryDelay)
    {
        ArgumentNullException.ThrowIfNull(list);
        _list = list;
        _retryDelay = retryDelay;
        _list.Rows.CollectionChanged += OnRowsChanged;
    }

    public ObservableCollection<TRow> Rows => _list.Rows;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Whether a snapshot arrived and listed nothing.</summary>
    public bool IsEmpty => IsLoaded && Rows.Count == 0;

    public bool HasRows => Rows.Count > 0;

    /// <summary>Starts following the list; call it from the page's <c>Loaded</c>.</summary>
    public void StartWatching()
    {
        if (_watch is not null)
        {
            return;
        }

        _watch = new CancellationTokenSource();
        _ = WatchAsync(_watch.Token);
    }

    /// <summary>Stops following the list; call it from the page's <c>Unloaded</c>.</summary>
    public void StopWatching()
    {
        _watch?.Cancel();
        _watch?.Dispose();
        _watch = null;
    }

    /// <summary>Follows the list until <paramref name="cancellationToken"/> is cancelled.</summary>
    public async Task WatchAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var changes = await OpenAsync(cancellationToken);
                await foreach (var change in changes.WithCancellation(cancellationToken))
                {
                    Apply(change);
                }

                await Task.Delay(_retryDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    protected abstract Task<IAsyncEnumerable<ListChange<TItem>>> OpenAsync(CancellationToken cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopWatching();
            _list.Rows.CollectionChanged -= OnRowsChanged;
        }
    }

    private void Apply(ListChange<TItem> change)
    {
        if (change is ListChange<TItem>.Failed failed)
        {
            ErrorMessage = failed.Error.Message;
            IsEngineDown = failed.Error.Code is DaemonErrors.EngineNotRunningCode or DaemonErrors.UnavailableCode;

            return;
        }

        ErrorMessage = null;
        IsEngineDown = false;
        _list.Apply(change);

        if (change is ListChange<TItem>.Snapshot)
        {
            IsLoaded = true;
        }
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasRows));
    }
}
