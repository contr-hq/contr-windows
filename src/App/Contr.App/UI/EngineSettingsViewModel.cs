using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.UI.Common.Formatting;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>One line of what is wrong with the edited <c>daemon.json</c>, under the editor.</summary>
/// <param name="Line">From 1; 0 where the problem has no line.</param>
public sealed record EngineFileProblem(int Line, string Text)
{
    /// <summary>Such as <c>Line 11 · Error · unknown option "log-opt"</c>.</summary>
    public string Display => Line > 0
        ? $"Line {Line.ToString(CultureInfo.CurrentCulture)} · Error · {Text}"
        : $"Error · {Text}";
}

/// <summary>
/// Backs Settings' Engine tab: the engine's <c>daemon.json</c> as text, kept apart from the saved file until the
/// user applies it, and what the daemon or the engine found wrong with it.
/// </summary>
public sealed partial class EngineSettingsViewModel : ObservableObject
{
    /// <summary>The first API revision with <see cref="DaemonSetting.DaemonJson"/>.</summary>
    private const uint _daemonJsonRevision = 13;

    private static readonly JsonSerializerOptions _indented = new() {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IDaemonSettings _settings;
    private readonly IDaemonConnection _daemon;
    private readonly INotificationService _notifications;
    private readonly DispatcherQueue? _dispatcher;
    private IDisposable? _status;
    private DispatcherQueueTimer? _clock;
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges), nameof(CanApply), nameof(LineNumbers), nameof(EditedText))]
    public partial string Text { get; set; }

    /// <summary>The settings the daemon keeps; absent while it keeps none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges), nameof(CanApply), nameof(CanEdit), nameof(IsLocked))]
    public partial DaemonSettings? Saved { get; set; }

    /// <summary>When the user last changed the text; absent while it matches the saved file.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditedText))]
    public partial DateTimeOffset? EditedAt { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(CanApply), nameof(FooterText))]
    public partial string ErrorTitle { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    /// <summary>The unknown key and the key the engine suggests, for the error bar's action; absent without one.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuggestion), nameof(SuggestionText))]
    public partial UnknownDaemonKey? Suggestion { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply), nameof(CanEdit))]
    public partial bool IsApplying { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApplyText), nameof(ApplyHint))]
    public partial bool EngineRuns { get; set; }

    public EngineSettingsViewModel(
        IDaemonSettings settings, IDaemonConnection daemon, INotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(notifications);
        _settings = settings;
        _daemon = daemon;
        _notifications = notifications;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        IsOffered = daemon.Identity is { ApiRevision: >= _daemonJsonRevision } identity
            && identity.Capabilities.Contains(DaemonCapability.EngineConfiguration);
        EngineRuns = daemon.Status.Engine == EngineState.Running;
        Text = "{}";
        ErrorTitle = string.Empty;
        ErrorMessage = string.Empty;
        Problems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasProblems));
    }

    /// <summary>What is wrong with the file, under the editor.</summary>
    public ObservableCollection<EngineFileProblem> Problems { get; } = [];

    /// <summary>Whether the daemon gives a <c>daemon.json</c> to its engine; the WSL backend does.</summary>
    public bool IsOffered { get; }

    public bool LacksOffer => !IsOffered;

    public bool IsLocked => Saved?.IsLocked(DaemonSetting.DaemonJson) == true;

    public bool CanEdit => IsOffered && Saved is not null && !IsLocked && !IsApplying;

    public bool HasChanges => Saved is not null && WithLineFeeds(Text) != WithLineFeeds(Saved.DaemonJson);

    public bool HasError => ErrorTitle.Length > 0;

    public bool HasProblems => Problems.Count > 0;

    public bool CanApply => HasChanges && !HasError && !IsApplying;

    public bool HasSuggestion => Suggestion is not null;

    /// <summary>Such as <c>Change to log-opts</c>.</summary>
    public string SuggestionText => Suggestion is { } suggestion ? $"Change to {suggestion.Suggestion}" : string.Empty;

    /// <summary>The editor's line numbers, one per line of <see cref="Text"/>.</summary>
    public string LineNumbers => string.Join(
        "\n", Enumerable.Range(1, WithLineFeeds(Text).Count(character => character == '\n') + 1));

    /// <summary>Such as <c>· edited now</c>; empty while the text matches the saved file.</summary>
    public string EditedText => HasChanges && EditedAt is { } edited
        ? $"· edited {HumanFormat.Ago(edited, DateTimeOffset.Now)}"
        : string.Empty;

    public string FooterText => HasError ? "Fix the error to apply." : "Unsaved changes to daemon.json.";

    public string ApplyHint => EngineRuns
        ? "Applying restarts the engine. Running containers stop and start again."
        : "The engine uses it when it starts.";

    public string ApplyText => EngineRuns ? "Apply and restart engine" : "Apply";

    /// <summary>Follows the engine's state and keeps <see cref="EditedText"/> current.</summary>
    public void Activate()
    {
        _status ??= _daemon.StatusChanges.Subscribe(status => {
            // StatusChanges emits on a background thread.
            var runs = status.Engine == EngineState.Running;

            if (_dispatcher is null)
            {
                EngineRuns = runs;
            }
            else
            {
                _dispatcher.TryEnqueue(() => EngineRuns = runs);
            }
        });

        if (_clock is null && _dispatcher is not null)
        {
            _clock = _dispatcher.CreateTimer();
            _clock.Interval = TimeSpan.FromSeconds(30);
            _clock.Tick += (_, _) => OnPropertyChanged(nameof(EditedText));
            _clock.Start();
        }
    }

    public void Deactivate()
    {
        _status?.Dispose();
        _status = null;
        _clock?.Stop();
        _clock = null;
    }

    /// <summary>Shows the daemon's settings; text the user has not applied stays.</summary>
    public void Show(DaemonSettings? settings)
    {
        var keepsEdits = HasChanges && settings is not null;
        Saved = settings;

        if (!keepsEdits)
        {
            Load(settings?.DaemonJson ?? "{}");
        }
    }

    public void Discard()
    {
        Load(Saved?.DaemonJson ?? "{}");
    }

    /// <summary>Lays the text out as the daemon saves it, two spaces deep; text that is not JSON says why.</summary>
    public void Format()
    {
        if (Check(Text) is { } problem)
        {
            ShowProblem(problem.Line, problem.Text, "Nothing was formatted.");

            return;
        }

        Text = Formatted(Text);
    }

    /// <summary>Empties the file; applying it returns the engine to its own defaults.</summary>
    public void ResetToDefaults()
    {
        Text = "{}";
    }

    /// <summary>Renames the unknown key to the one the engine suggests, then applies the file again.</summary>
    public async Task ChangeToSuggestionAsync()
    {
        if (Suggestion is not { } suggestion)
        {
            return;
        }

        Text = Renamed(Text, suggestion.Key, suggestion.Suggestion);
        await ApplyAsync();
    }

    /// <summary>Saves the file once the engine accepts it, and restarts a running engine to use it.</summary>
    public async Task ApplyAsync()
    {
        if (!HasChanges || IsApplying)
        {
            return;
        }

        if (Check(Text) is { } problem)
        {
            ShowProblem(problem.Line, problem.Text, "The engine won't start with this file.");

            return;
        }

        IsApplying = true;
        try
        {
            var update = await _settings.UpdateDaemonJsonAsync(WithLineFeeds(Text), CancellationToken.None);

            if (update.Result.IsFailure)
            {
                ShowRefusal(update);

                return;
            }

            Saved = update.Result.Value.Settings;
            Load(Saved.DaemonJson);

            if (update.Result.Value.RestartRequired.Count == 0 || !EngineRuns)
            {
                _notifications.Show(
                    "Saved. The engine uses the new daemon.json when it starts.", InfoBarSeverity.Success);

                return;
            }

            var restarted = await _daemon.RestartEngineAsync(CancellationToken.None);

            if (restarted.IsSuccess)
            {
                _notifications.Show("The engine restarted with the new daemon.json.", InfoBarSeverity.Success);
            }
            else
            {
                _notifications.Show(
                    restarted.Error.Message, InfoBarSeverity.Error, "Saved, but the engine didn't restart");
            }
        }
        finally
        {
            IsApplying = false;
        }
    }

    /// <summary>Why <paramref name="text"/> cannot be a <c>daemon.json</c>; absent when it can.</summary>
    public static EngineFileProblem? Check(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            using var document = JsonDocument.Parse(WithLineFeeds(text));

            return document.RootElement.ValueKind == JsonValueKind.Object
                ? null
                : new EngineFileProblem(1, "daemon.json holds one JSON object, between { and }");
        }
        catch (JsonException exception)
        {
            var reason = exception.Message;
            var end = reason.IndexOf(" LineNumber:", StringComparison.Ordinal);

            // The reason quotes the text it stopped at, line breaks and all.
            return new EngineFileProblem(
                (int)(exception.LineNumber ?? 0) + 1,
                Whitespace().Replace(end > 0 ? reason[..end].TrimEnd('.', ' ') : reason, " "));
        }
    }

    /// <summary>The line, from 1, where <paramref name="key"/> is written as a key; 0 where it is not.</summary>
    public static int LineOf(string text, string key)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(key);

        var lines = WithLineFeeds(text);
        var match = KeyPattern(key).Match(lines);

        return match.Success ? lines[..match.Index].Count(character => character == '\n') + 1 : 0;
    }

    /// <summary><paramref name="text"/> with the first key <paramref name="key"/> renamed.</summary>
    public static string Renamed(string text, string key, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(name);

        var match = KeyPattern(key).Match(text);

        return match.Success
            ? string.Concat(
                text.AsSpan(0, match.Index), JsonSerializer.Serialize(name), text.AsSpan(match.Index + match.Length))
            : text;
    }

    public static string Formatted(string text)
    {
        return JsonNode.Parse(text)?.ToJsonString(_indented) ?? "{}";
    }

    /// <summary>The text with <c>\n</c> between lines; TextBox puts <c>\r</c> there.</summary>
    private static string WithLineFeeds(string text)
    {
        return text.ReplaceLineEndings("\n");
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    private static Regex KeyPattern(string key)
    {
        return new Regex($"{Regex.Escape(JsonSerializer.Serialize(key))}(?=\\s*:)", RegexOptions.CultureInvariant);
    }

    private void Load(string text)
    {
        _isLoading = true;
        Text = text;
        _isLoading = false;
        EditedAt = null;
        ClearProblems();
    }

    partial void OnTextChanged(string oldValue, string newValue)
    {
        // The editor hands loaded text back with its own line breaks.
        if (_isLoading || WithLineFeeds(oldValue ?? string.Empty) == WithLineFeeds(newValue))
        {
            return;
        }

        EditedAt = DateTimeOffset.Now;
        ClearProblems();
    }

    private void ClearProblems()
    {
        ErrorTitle = string.Empty;
        ErrorMessage = string.Empty;
        Suggestion = null;
        Problems.Clear();
    }

    /// <summary>Opens the error bar in place of any notification; a page shows one bar.</summary>
    private void ShowProblem(int line, string text, string message)
    {
        _notifications.Close();
        Problems.Clear();
        Problems.Add(new EngineFileProblem(line, text));
        var headline = text.TrimEnd('.');
        ErrorTitle = line > 0 ? $"Line {line.ToString(CultureInfo.CurrentCulture)}: {headline}." : $"{headline}.";
        ErrorMessage = message;
        Suggestion = null;
    }

    private void ShowRefusal(DaemonJsonUpdate update)
    {
        var error = update.Result.Error;

        if (update.UnknownKeys.Count > 0)
        {
            _notifications.Close();
            Problems.Clear();

            foreach (var unknown in update.UnknownKeys)
            {
                Problems.Add(new EngineFileProblem(LineOf(Text, unknown.Key), $"unknown option \"{unknown.Key}\""));
            }

            var first = update.UnknownKeys[0];
            var line = LineOf(Text, first.Key);
            ErrorTitle = line > 0
                ? $"Line {line.ToString(CultureInfo.CurrentCulture)}: unknown option \"{first.Key}\"."
                : $"Unknown option \"{first.Key}\".";
            ErrorMessage = first.Suggestion.Length > 0
                ? $"Did you mean \"{first.Suggestion}\"? The engine won't start with this file."
                : "The engine won't start with this file.";
            Suggestion = first.Suggestion.Length > 0 ? first : null;

            return;
        }

        var reason = DaemonSettingPaths.Refused(error.Message)?.Reason ?? error.Message;
        const string rejected = "the engine rejects its configuration: ";

        if (reason.StartsWith(rejected, StringComparison.Ordinal))
        {
            ShowProblem(0, reason[rejected.Length..], "Nothing was saved. The engine won't start with this file.");

            return;
        }

        // Keys Contr sets come back as "<key> is not available: <what to change instead>".
        var key = reason.Split(' ', 2)[0];
        ShowProblem(LineOf(Text, key), reason, "Nothing was saved.");
    }
}
