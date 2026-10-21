using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Compose;
using Contr.Modules.Containers.Application.Compose.Commands.AddComposeProject;
using Contr.Modules.Containers.Application.Compose.Commands.RemoveComposeProject;
using Contr.Modules.Containers.Application.Compose.Commands.RunComposeAction;
using Contr.Modules.Containers.Application.Compose.Queries.StreamComposeLogs;
using Contr.Modules.Containers.Application.Compose.Queries.WatchComposeProjects;
using Contr.Modules.Containers.Domain;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Engine;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>
/// The engine's Compose projects, one project's services, the Compose commands run on it and its merged logs.
/// </summary>
/// <remarks>Use it on the UI thread: the list, the commands and the logs resume on the captured context.</remarks>
public sealed partial class ComposeViewModel : LiveListViewModel<ComposeProjectDto, ComposeProjectDto>
{
    private const string _allServices = "All";

    /// <summary>The logs keep the newest lines up to this count.</summary>
    private const int _capacity = 5000;

    /// <summary>A command's output keeps the newest lines up to this count.</summary>
    private const int _outputCapacity = 1000;

    private const int _tailLines = 100;

    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly INotificationService _notifications;
    private readonly IDaemonConnection _daemon;
    private readonly List<ComposeLogLine> _allLines = [];
    private CancellationTokenSource? _logs;
    private string _logsKey = string.Empty;
    private string? _selectedName;
    private string _runProject = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CanAct), nameof(CanUp), nameof(ShowsRun))]
    [NotifyPropertyChangedFor(nameof(LacksConfigFiles))]
    [NotifyPropertyChangedFor(nameof(WorkingDir))]
    public partial ComposeProjectItem? Selected { get; set; }

    /// <summary>The selected project's Compose files, comma-separated; empty when it records none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConfigFiles), nameof(LacksConfigFiles), nameof(CanUp))]
    public partial string ConfigFiles { get; set; }

    /// <summary>Why Compose cannot read the selected project's files; empty when it can.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConfigError))]
    public partial string ConfigError { get; set; }

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    /// <summary>Whether the logs take new output as the services write it, and keep it in sight.</summary>
    [ObservableProperty]
    public partial bool IsFollowing { get; set; }

    [ObservableProperty]
    public partial bool HasProjects { get; set; }

    /// <summary>Counted over every project.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PillItem> SummaryPills { get; set; }

    /// <summary>The last command run on <see cref="_runProject"/>; absent before one runs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRun))]
    public partial ComposeRun? Run { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAct), nameof(CanUp))]
    public partial bool IsActing { get; set; }

    /// <summary>Why the logs stopped; empty while they stream or ended on their own.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogsNote))]
    public partial string LogsError { get; set; }

    /// <summary>Whether the daemon runs Compose; true while no daemon has answered yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LacksOffer))]
    public partial bool IsOffered { get; set; }

    public ComposeViewModel(
        ISender sender, INotificationService notifications, IDaemonConnection daemon, IDaemonSettings settings)
        : base(
            new LiveCollection<ComposeProjectDto, ComposeProjectDto>(
                project => project, project => project.Name, ComposeProjectItem.Compare),
            _retryDelay)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(daemon);
        _sender = sender;
        _notifications = notifications;
        _daemon = daemon;
        Engine = new EngineGateViewModel(daemon, settings, notifications);
        ConfigFiles = string.Empty;
        ConfigError = string.Empty;
        LogsError = string.Empty;
        SummaryPills = [];
        IsFollowing = true;
        IsOffered = true;
        Rows.CollectionChanged += OnRowsChanged;
    }

    public EngineGateViewModel Engine { get; }

    public ObservableCollection<ComposeProjectItem> Projects { get; } = [];

    /// <summary>The selected project's services, sorted by name.</summary>
    public ObservableCollection<ComposeServiceRow> Services { get; } = [];

    /// <summary><c>All</c>, then each service, for the log filter.</summary>
    public ObservableCollection<string> Filters { get; } = [];

    /// <summary>The merged log lines the filter lets through.</summary>
    public ObservableCollection<ComposeLogLine> Lines { get; } = [];

    /// <summary>What the last command wrote, newest last.</summary>
    public ObservableCollection<string> Output { get; } = [];

    public bool LacksOffer => !IsOffered;

    public bool HasSelection => Selected is not null;

    public bool HasConfigFiles => ConfigFiles.Length > 0;

    /// <summary>Whether a selected project records no Compose files, which Up and Pull need.</summary>
    public bool LacksConfigFiles => Selected is not null && ConfigFiles.Length == 0;

    public bool HasConfigError => ConfigError.Length > 0;

    /// <summary>Whether a command can start: a project is selected and no command runs.</summary>
    public bool CanAct => Selected is not null && !IsActing;

    /// <summary>Up and Pull also read the project's Compose files.</summary>
    public bool CanUp => CanAct && HasConfigFiles;

    /// <summary>Whether the selected project is the one the last command ran on.</summary>
    public bool ShowsRun => Run is not null && Selected?.Name == _runProject;

    /// <summary>The selected project's working directory, which Open folder opens; empty without one.</summary>
    public string WorkingDir => Selected?.Location ?? string.Empty;

    /// <summary>Shown in place of log lines while there are none.</summary>
    public string LogsNote => LogsError.Length > 0
        ? $"The logs stopped: {LogsError}"
        : "No log lines yet. They appear here as the services write them.";

    /// <summary>Raised when a log line arrives while following, so the view can keep it in sight.</summary>
    public event EventHandler<ComposeLogLine>? LineAdded;

    /// <summary>Raised when a command writes a line, so the view can keep it in sight.</summary>
    public event EventHandler<string>? OutputAdded;

    public void Activate()
    {
        IsOffered = _daemon.Identity is not { } identity || identity.Capabilities.Contains(DaemonCapability.Compose);
        Engine.Start();

        if (IsOffered)
        {
            StartWatching();
        }

        Show();
    }

    public void Deactivate()
    {
        StopWatching();
        StopLogs();
        Engine.Stop();
    }

    public void Select(ComposeProjectItem project)
    {
        ArgumentNullException.ThrowIfNull(project);

        _selectedName = project.Name;
        Show();
    }

    public Task UpAsync()
    {
        return RunAsync(ComposeAction.Up, []);
    }

    public Task PullAsync()
    {
        return RunAsync(ComposeAction.Pull, []);
    }

    public Task RestartAsync()
    {
        return RunAsync(ComposeAction.Restart, []);
    }

    public Task StopAsync()
    {
        return RunAsync(ComposeAction.Stop, []);
    }

    /// <summary>Stops and removes the project's containers and networks; ask first.</summary>
    /// <param name="removeVolumes">Also deletes the project's named volumes.</param>
    public Task DownAsync(bool removeVolumes)
    {
        return RunAsync(ComposeAction.Down, [], removeVolumes);
    }

    public Task RestartServiceAsync(ComposeServiceRow service)
    {
        ArgumentNullException.ThrowIfNull(service);

        return RunAsync(ComposeAction.Restart, [service.Name]);
    }

    /// <summary>Adds the project <paramref name="configFile"/> describes and selects it.</summary>
    public async Task AddProjectAsync(string configFile)
    {
        ArgumentException.ThrowIfNullOrEmpty(configFile);

        var result = await _sender.Send(new AddComposeProjectCommand([configFile]));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "Couldn't add the project");

            return;
        }

        _selectedName = result.Value.Name;
        Show();
        _notifications.Show($"Added {result.Value.Name}.", InfoBarSeverity.Success);
    }

    /// <summary>Forgets a project the user added; one with containers stays listed through them.</summary>
    public async Task RemoveProjectAsync(ComposeProjectItem project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (!project.IsAdded)
        {
            return;
        }

        var result = await _sender.Send(new RemoveComposeProjectCommand(project.Name));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, $"Couldn't remove {project.Name}");

            return;
        }

        _notifications.Show(
            project.Total > 0
                ? $"Removed {project.Name}. It stays listed while it has containers."
                : $"Removed {project.Name}.",
            InfoBarSeverity.Success);
    }

    protected override Task<IAsyncEnumerable<ListChange<ComposeProjectDto>>> OpenAsync(
        CancellationToken cancellationToken)
    {
        return _sender.Send(new WatchComposeProjectsQuery(), cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Rows.CollectionChanged -= OnRowsChanged;
            Deactivate();
        }

        base.Dispose(disposing);
    }

    partial void OnFilterIndexChanged(int value)
    {
        Refilter();
    }

    partial void OnIsFollowingChanged(bool value)
    {
        if (value)
        {
            Show();

            return;
        }

        // The lines so far stay; following again reads the end of each log anew.
        StopLogs();
        _logsKey = LogsKey(Rows.FirstOrDefault(project => project.Name == _selectedName));
    }

    private static string Verb(ComposeAction action)
    {
        return action switch {
            ComposeAction.Up => "Up",
            ComposeAction.Pull => "Pull",
            ComposeAction.Start => "Start",
            ComposeAction.Stop => "Stop",
            ComposeAction.Restart => "Restart",
            _ => "Down",
        };
    }

    private static string Done(ComposeAction action, string target, bool removeVolumes)
    {
        return action switch {
            ComposeAction.Up => $"{target} is up.",
            ComposeAction.Pull => $"Pulled the images of {target}.",
            ComposeAction.Start => $"Started {target}.",
            ComposeAction.Stop => $"Stopped {target}.",
            ComposeAction.Restart => $"Restarted {target}.",
            _ when removeVolumes => $"Took down {target} and deleted its volumes.",
            _ => $"Took down {target}.",
        };
    }

    private async Task RunAsync(ComposeAction action, IReadOnlyList<string> services, bool removeVolumes = false)
    {
        if (Selected is not { } project || IsActing)
        {
            return;
        }

        var target = services.Count == 1 ? services[0] : project.Name;
        var verb = Verb(action);
        var run = new ComposeRun($"{verb} · {target}", "Running", PillKind.Busy);
        IsActing = true;
        _runProject = project.Name;
        Output.Clear();
        Run = run;

        try
        {
            var events = await _sender.Send(new RunComposeActionCommand(project.Name, action, services, removeVolumes));
            await foreach (var item in events)
            {
                switch (item)
                {
                    case ComposeActionEvent.Output output:
                        AddOutput(output.Line);
                        break;
                    case ComposeActionEvent.Exited { ExitCode: 0 }:
                        run = run with { StatusText = "Done", PillKind = PillKind.Good };
                        _notifications.Show(Done(action, target, removeVolumes), InfoBarSeverity.Success);
                        break;
                    case ComposeActionEvent.Exited exited:
                        var code = exited.ExitCode.ToString(CultureInfo.CurrentCulture);
                        run = run with { StatusText = $"Exited ({code})", PillKind = PillKind.Critical };
                        _notifications.Show(
                            $"Compose exited with code {code}. Its output is under the project's commands.",
                            InfoBarSeverity.Error,
                            $"Compose {verb.ToLowerInvariant()} failed for {target}");
                        break;
                    case ComposeActionEvent.Failed failed:
                        run = run with { StatusText = "Failed", PillKind = PillKind.Critical };
                        _notifications.Show(
                            failed.Error.Message,
                            InfoBarSeverity.Error,
                            $"Couldn't run {verb.ToLowerInvariant()} on {target}");
                        break;
                }
            }

            Run = run.IsRunning ? run with { StatusText = "Ended", PillKind = PillKind.Hollow } : run;
        }
        finally
        {
            IsActing = false;
        }
    }

    private void AddOutput(string line)
    {
        Output.Add(line);

        if (Output.Count > _outputCapacity)
        {
            Output.RemoveAt(0);
        }

        OutputAdded?.Invoke(this, line);
    }

    private static List<PillItem> Summarize(List<ComposeProjectItem> projects)
    {
        var running = projects.Count(project => project.Running > 0);
        var stopped = projects.Count - running;
        List<PillItem> pills = [
            new(projects.Count == 1 ? "1 project" : $"{Count(projects.Count)} projects", PillKind.Plain),
        ];

        if (running > 0)
        {
            pills.Add(new($"{Count(running)} running", PillKind.Good));
        }

        if (stopped > 0)
        {
            pills.Add(new($"{Count(stopped)} stopped", PillKind.Hollow));
        }

        return pills;
    }

    private static string Count(int count)
    {
        return count.ToString(CultureInfo.CurrentCulture);
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Show();
    }

    private void Show()
    {
        var selected = Rows.Any(project => project.Name == _selectedName)
            ? _selectedName
            : Rows.FirstOrDefault()?.Name;
        // An unchanged item keeps its instance, so the list keeps it selected.
        List<ComposeProjectItem> projects = [.. Rows
            .Select(ComposeProjectItem.From)
            .Select(item => Projects.FirstOrDefault(existing => existing == item) ?? item)];
        CollectionSync.Sync(Projects, projects, project => project.Name);

        HasProjects = projects.Count > 0;
        var pills = Summarize(projects);

        if (!pills.SequenceEqual(SummaryPills))
        {
            SummaryPills = pills;
        }

        _selectedName = selected;
        Selected = projects.FirstOrDefault(project => project.Name == selected);

        var project = Rows.FirstOrDefault(project => project.Name == selected);
        List<ComposeServiceRow> services = project is null
            ? []
            : [.. project.Services.Select(service => ComposeServiceRow.From(service, project.WorkingDir))];
        CollectionSync.Sync(Services, services, service => service.Name);
        ConfigFiles = project is null ? string.Empty : string.Join(", ", project.ConfigFiles);
        ConfigError = project?.ConfigError ?? string.Empty;

        FollowLogs(project);
    }

    /// <summary>What the log stream depends on: the project, whether it follows, and which containers run.</summary>
    /// <remarks>A following stream takes new output only from the containers running when it opened.</remarks>
    private string LogsKey(ComposeProjectDto? project)
    {
        if (project is null)
        {
            return string.Empty;
        }

        if (!IsFollowing)
        {
            return project.Name;
        }

        var running = project.Services
            .SelectMany(service => service.Containers)
            .Where(container => container.State == ContainerState.Running)
            .Select(container => container.Id.Value);

        return $"{project.Name}|{string.Join(',', running)}";
    }

    private void FollowLogs(ComposeProjectDto? project)
    {
        var key = LogsKey(project);

        if (key == _logsKey)
        {
            return;
        }

        var sameProject = project is not null && _logsKey.Split('|')[0] == project.Name;
        StopLogs();
        _logsKey = key;
        _allLines.Clear();
        Lines.Clear();
        LogsError = string.Empty;

        List<string> filters = [_allServices, .. project?.Services.Select(service => service.Name) ?? []];
        var filter = sameProject && FilterIndex < Filters.Count ? Filters[FilterIndex] : _allServices;
        CollectionSync.Sync(Filters, filters, name => name);
        FilterIndex = Math.Max(0, Filters.IndexOf(filter));

        if (project is null || !project.Services.Any(service => service.Containers.Count > 0))
        {
            return;
        }

        var colors = project.Services
            .Select((service, index) => (service.Name, index))
            .ToDictionary(entry => entry.Name, entry => entry.index, StringComparer.Ordinal);
        _logs = new CancellationTokenSource();
        _ = ReadLogsAsync(project.Name, colors, IsFollowing, _logs.Token);
    }

    private void StopLogs()
    {
        _logs?.Cancel();
        _logs?.Dispose();
        _logs = null;
        _logsKey = string.Empty;
    }

    private async Task ReadLogsAsync(
        string project, Dictionary<string, int> colors, bool follow, CancellationToken cancellationToken)
    {
        Dictionary<(ContainerId Container, bool IsError), string> pending = [];

        try
        {
            var events = await _sender.Send(
                new StreamComposeLogsQuery(project, _tailLines, follow), cancellationToken);
            await foreach (var logEvent in events.WithCancellation(cancellationToken))
            {
                if (logEvent is ComposeLogEvent.Failed failed)
                {
                    LogsError = failed.Error.Message;

                    continue;
                }

                if (logEvent is not ComposeLogEvent.Output output)
                {
                    continue;
                }

                var key = (output.Container, output.IsError);
                var parts = (pending.GetValueOrDefault(key, string.Empty) + output.Text).Split('\n');
                pending[key] = parts[^1];
                var color = colors.GetValueOrDefault(output.Service, colors.Count);

                foreach (var part in parts[..^1])
                {
                    Add(new ComposeLogLine(output.Service, color, part.TrimEnd('\r')));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The project changed, following stopped or the page closed.
        }
    }

    private void Add(ComposeLogLine line)
    {
        _allLines.Add(line);

        if (_allLines.Count > _capacity)
        {
            var dropped = _allLines[0];
            _allLines.RemoveAt(0);

            if (Lines.Count > 0 && ReferenceEquals(Lines[0], dropped))
            {
                Lines.RemoveAt(0);
            }
        }

        if (Passes(line))
        {
            Lines.Add(line);

            if (IsFollowing)
            {
                LineAdded?.Invoke(this, line);
            }
        }
    }

    private bool Passes(ComposeLogLine line)
    {
        return FilterIndex <= 0 || FilterIndex >= Filters.Count || line.Service == Filters[FilterIndex];
    }

    private void Refilter()
    {
        Lines.Clear();

        foreach (var line in _allLines.Where(Passes))
        {
            Lines.Add(line);
        }
    }
}
