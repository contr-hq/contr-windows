using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Queries.StreamContainerLogs;
using Contr.Modules.Containers.Domain;
using Contr.UI.Contracts.Services.Platform;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>The log tab: a container's output, followed live, searchable.</summary>
/// <remarks>Use it on the UI thread: output resumes on the captured context.</remarks>
public sealed partial class ContainerLogsViewModel : ObservableObject, IDisposable
{
    /// <summary>The tab keeps the newest lines up to this count.</summary>
    private const int _capacity = 5000;

    private const int _sinceLastStart = 0;
    private const int _sinceLastHour = 1;

    private readonly ISender _sender;
    private readonly IClipboardService _clipboard;
    private LogLine? _openStdout;
    private LogLine? _openStderr;
    private ContainerId? _id;
    private DateTimeOffset? _startedAt;
    private CancellationTokenSource? _follow;
    private int _current = -1;

    /// <summary>0 shows output since the container last started, 1 the last hour, 2 the newest lines.</summary>
    [ObservableProperty]
    public partial int SinceIndex { get; set; }

    [ObservableProperty]
    public partial bool ShowTimestamps { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    /// <summary>Such as <c>2 of 5</c>; empty without a search.</summary>
    [ObservableProperty]
    public partial string MatchText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    public ContainerLogsViewModel(ISender sender, IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(clipboard);
        _sender = sender;
        _clipboard = clipboard;
        ShowTimestamps = true;
        SearchText = string.Empty;
        MatchText = string.Empty;
    }

    /// <summary>Raised with the line the search moved to.</summary>
    public event EventHandler<LogLine>? MatchSelected;

    public ObservableCollection<LogLine> Lines { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>Follows the output; <paramref name="startedAt"/> begins the "last start" range.</summary>
    public void Open(ContainerId id, DateTimeOffset? startedAt)
    {
        ArgumentNullException.ThrowIfNull(id);

        _id = id;
        _startedAt = startedAt;
        Restart();
    }

    public void Close()
    {
        _follow?.Cancel();
        _follow?.Dispose();
        _follow = null;
    }

    /// <summary>Adds a client note after the output, such as the exit code.</summary>
    public void Note(string text)
    {
        CloseOpenLines();
        Add(new LogLine(string.Empty, $"— {text} —", isError: false, isNote: true));
    }

    public void CopyAll()
    {
        var text = string.Join(
            Environment.NewLine,
            Lines.Select(line => line.HasTime ? $"{line.Time}  {line.Text}" : line.Text));
        _clipboard.SetText(text);
    }

    public void NextMatch()
    {
        MoveMatch(1);
    }

    public void PreviousMatch()
    {
        MoveMatch(-1);
    }

    public void Dispose()
    {
        Close();
    }

    partial void OnSinceIndexChanged(int value)
    {
        Restart();
    }

    partial void OnShowTimestampsChanged(bool value)
    {
        Restart();
    }

    partial void OnSearchTextChanged(string value)
    {
        _current = -1;
        Mark();
    }

    private static string FormatTime(DateTimeOffset? timestamp)
    {
        return timestamp?.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private void Restart()
    {
        Close();

        if (_id is null)
        {
            return;
        }

        Lines.Clear();
        _openStdout = null;
        _openStderr = null;
        Error = null;
        _follow = new CancellationTokenSource();
        _ = FollowAsync(_id, Range(), _follow.Token);
    }

    private LogRange Range()
    {
        return SinceIndex switch {
            _sinceLastStart when _startedAt is { } startedAt => new LogRange(null, startedAt, ShowTimestamps),
            _sinceLastHour => new LogRange(null, DateTimeOffset.Now.AddHours(-1), ShowTimestamps),
            _ => new LogRange(_capacity, null, ShowTimestamps),
        };
    }

    private async Task FollowAsync(ContainerId id, LogRange range, CancellationToken cancellationToken)
    {
        try
        {
            var events = await _sender.Send(new StreamContainerLogsQuery(id, range), cancellationToken);
            await foreach (var logEvent in events.WithCancellation(cancellationToken))
            {
                switch (logEvent)
                {
                    case ContainerLogEvent.Output output:
                        Append(output);
                        break;
                    case ContainerLogEvent.Failed failed:
                        Error = failed.Error.Message;
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The tab closed or the range changed.
        }
    }

    /// <summary>Splits output into lines; a line without its newline stays open for the stream's next chunk.</summary>
    private void Append(ContainerLogEvent.Output output)
    {
        var time = FormatTime(output.Timestamp);
        var parts = output.Text.Split('\n');

        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index].TrimEnd('\r');
            var isLast = index == parts.Length - 1;
            var open = output.IsError ? _openStderr : _openStdout;

            if (open is not null)
            {
                open.Append(part);
                SetOpen(output.IsError, isLast ? open : null);
                Mark(open);

                continue;
            }

            if (isLast && part.Length == 0)
            {
                continue;
            }

            var line = new LogLine(time, part, output.IsError);
            Add(line);
            SetOpen(output.IsError, isLast ? line : null);
        }

        UpdateMatchText();
    }

    private void SetOpen(bool isError, LogLine? line)
    {
        if (isError)
        {
            _openStderr = line;
        }
        else
        {
            _openStdout = line;
        }
    }

    private void CloseOpenLines()
    {
        _openStdout = null;
        _openStderr = null;
    }

    private void Add(LogLine line)
    {
        Mark(line);
        Lines.Add(line);

        if (Lines.Count > _capacity)
        {
            Lines.RemoveAt(0);
            _current = Math.Max(-1, _current - 1);
        }
    }

    private void Mark(LogLine line)
    {
        var query = SearchText.Trim();
        line.IsMatch = query.Length > 0 && line.Text.Contains(query, StringComparison.OrdinalIgnoreCase);
        line.Highlight = line.IsMatch ? query : string.Empty;
    }

    private void Mark()
    {
        foreach (var line in Lines)
        {
            Mark(line);
        }

        UpdateMatchText();
    }

    private void MoveMatch(int step)
    {
        var matches = Lines
            .Select((line, index) => (line, index))
            .Where(entry => entry.line.IsMatch)
            .Select(entry => entry.index)
            .ToList();

        if (matches.Count == 0)
        {
            return;
        }

        var position = matches.IndexOf(_current);
        position = position < 0
            ? (step > 0 ? 0 : matches.Count - 1)
            : (position + step + matches.Count) % matches.Count;
        _current = matches[position];
        UpdateMatchText();
        MatchSelected?.Invoke(this, Lines[_current]);
    }

    private void UpdateMatchText()
    {
        if (SearchText.Trim().Length == 0)
        {
            MatchText = string.Empty;

            return;
        }

        var count = Lines.Count(line => line.IsMatch);
        var position = _current >= 0 && _current < Lines.Count && Lines[_current].IsMatch
            ? Lines
                .Take(_current + 1)
                .Count(line => line.IsMatch)
            : 0;

        MatchText = (count, position) switch {
            (0, _) => "No matches",
            (1, 0) => "1 match",
            (_, 0) => $"{count} matches",
            _ => $"{position} of {count}",
        };
    }
}
