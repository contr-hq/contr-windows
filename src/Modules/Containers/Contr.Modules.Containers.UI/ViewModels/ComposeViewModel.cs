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
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Commands.RestartContainer;
using Contr.Modules.Containers.Application.Containers.Commands.StartContainer;
using Contr.Modules.Containers.Application.Containers.Commands.StopContainer;
using Contr.Modules.Containers.Application.Containers.Queries.StreamContainerLogs;
using Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;
using Contr.SharedKernel;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Engine;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>The Compose projects the containers' labels name, one project's services and their merged logs.</summary>
/// <remarks>Use it on the UI thread: the list and the logs resume on the captured context.</remarks>
public sealed partial class ComposeViewModel : LiveListViewModel<ContainerDto, ContainerRow>
{
    private const string _allServices = "All";

    /// <summary>The logs keep the newest lines up to this count.</summary>
    private const int _capacity = 5000;

    private const int _tailLines = 100;

    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly INotificationService _notifications;
    private readonly List<ComposeLogLine> _allLines = [];
    private CancellationTokenSource? _logs;
    private string _logsFollow = string.Empty;
    private string? _selectedName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial ComposeProjectItem? Selected { get; set; }

    /// <summary>The selected project's Compose file; empty when the labels name none.</summary>
    [ObservableProperty]
    public partial string ConfigFile { get; set; }

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    [ObservableProperty]
    public partial bool IsFollowing { get; set; }

    [ObservableProperty]
    public partial bool HasProjects { get; set; }

    /// <summary>Counted over every project.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PillItem> SummaryPills { get; set; }

    public ComposeViewModel(
        ISender sender, INotificationService notifications, IDaemonConnection daemon, IDaemonSettings settings)
        : this(sender, notifications, daemon, settings, new ContainerUsage())
    {
    }

    private ComposeViewModel(
        ISender sender,
        INotificationService notifications,
        IDaemonConnection daemon,
        IDaemonSettings settings,
        ContainerUsage usage)
        : base(
            new LiveCollection<ContainerDto, ContainerRow>(
                container => ContainerRow.From(container, usage),
                row => row.Id,
                ContainerRow.Compare),
            _retryDelay)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(notifications);
        _sender = sender;
        _notifications = notifications;
        Engine = new EngineGateViewModel(daemon, settings, notifications);
        ConfigFile = string.Empty;
        SummaryPills = [];
        IsFollowing = true;
        Rows.CollectionChanged += OnRowsChanged;
    }

    public EngineGateViewModel Engine { get; }

    public ObservableCollection<ComposeProjectItem> Projects { get; } = [];

    /// <summary>The selected project's containers, one row per service.</summary>
    public ObservableCollection<ContainerRow> Services { get; } = [];

    /// <summary><c>All</c>, then each service, for the log filter.</summary>
    public ObservableCollection<string> Filters { get; } = [];

    /// <summary>The merged log lines the filter lets through.</summary>
    public ObservableCollection<ComposeLogLine> Lines { get; } = [];

    public bool HasSelection => Selected is not null;

    public bool HasConfigFile => ConfigFile.Length > 0;

    /// <summary>Raised when a line arrives while following, so the view can keep it in sight.</summary>
    public event EventHandler<ComposeLogLine>? LineAdded;

    public void Activate()
    {
        Engine.Start();
        StartWatching();
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

    public Task StartAllAsync()
    {
        return RunEachAsync(row => row.CanStart, row => new StartContainerCommand(row.ContainerId, row.State), "start");
    }

    public Task StopAllAsync()
    {
        return RunEachAsync(row => row.CanStop, row => new StopContainerCommand(row.ContainerId, row.State), "stop");
    }

    public Task RestartAllAsync()
    {
        return RunEachAsync(
            row => row.CanRestart, row => new RestartContainerCommand(row.ContainerId, row.State), "restart");
    }

    protected override Task<IAsyncEnumerable<ListChange<ContainerDto>>> OpenAsync(
        CancellationToken cancellationToken)
    {
        return _sender.Send(new WatchContainersQuery(), cancellationToken);
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

    partial void OnSelectedChanged(ComposeProjectItem? value)
    {
        OnPropertyChanged(nameof(HasConfigFile));
    }

    partial void OnConfigFileChanged(string value)
    {
        OnPropertyChanged(nameof(HasConfigFile));
    }

    partial void OnFilterIndexChanged(int value)
    {
        Refilter();
    }

    private static string ServiceOf(ContainerRow row)
    {
        return row.Service.Length > 0 ? row.Service : row.Name;
    }

    private async Task RunEachAsync(
        Func<ContainerRow, bool> applies, Func<ContainerRow, IRequest<Result>> command, string verb)
    {
        var rows = Services
            .Where(applies)
            .ToList();

        foreach (var row in rows)
        {
            var result = await _sender.Send(command(row));

            if (result.IsFailure)
            {
                _notifications.Show(result.Error.Message, InfoBarSeverity.Error, $"Could not {verb} {row.Name}");
            }
        }
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
        var byProject = Rows
            .Where(row => row.HasProject)
            .GroupBy(row => row.Project, StringComparer.Ordinal)
            .ToList();
        var selected = byProject.Any(group => group.Key == _selectedName)
            ? _selectedName
            : byProject
                .Select(group => group.Key)
                .Order(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

        var projects = byProject
            .Select(group => new ComposeProjectItem(
                group.Key,
                Location(group.ToList()),
                group.Count(row => row.IsRunning),
                group.Count(),
                group.Key == selected))
            .ToList();
        projects.Sort(ComposeProjectItem.Compare);
        CollectionSync.Sync(Projects, projects, project => project.Name);

        HasProjects = projects.Count > 0;
        var pills = Summarize(projects);

        if (!pills.SequenceEqual(SummaryPills))
        {
            SummaryPills = pills;
        }

        _selectedName = selected;
        Selected = projects.FirstOrDefault(project => project.IsSelected);

        var project = byProject.FirstOrDefault(group => group.Key == selected);
        List<ContainerRow> services = project is null
            ? []
            : project
                .OrderBy(ServiceOf, StringComparer.OrdinalIgnoreCase)
                .ToList();
        CollectionSync.Sync(Services, services, row => row.Id);
        ConfigFile = services
            .Select(row => row.ComposeFile)
            .FirstOrDefault(file => file.Length > 0) ?? string.Empty;

        FollowLogs(services);
    }

    private static string Location(List<ContainerRow> rows)
    {
        var folder = rows
            .Select(row => row.ComposeFolder)
            .FirstOrDefault(path => path.Length > 0);

        return folder ?? rows
            .Select(row => row.ComposeFile)
            .FirstOrDefault(path => path.Length > 0) ?? string.Empty;
    }

    /// <summary>Follows every container of the project, restarting only when the set of containers changes.</summary>
    private void FollowLogs(List<ContainerRow> services)
    {
        var key = string.Join(',', services.Select(row => row.Id));

        if (key == _logsFollow)
        {
            return;
        }

        StopLogs();
        _logsFollow = key;
        _allLines.Clear();
        Lines.Clear();

        var filters = new List<string> { _allServices };
        filters.AddRange(services
            .Select(ServiceOf)
            .Distinct(StringComparer.Ordinal));
        CollectionSync.Sync(Filters, filters, filter => filter);
        FilterIndex = 0;

        if (services.Count == 0)
        {
            return;
        }

        _logs = new CancellationTokenSource();
        for (var index = 0; index < services.Count; index++)
        {
            _ = FollowAsync(services[index], index, _logs.Token);
        }
    }

    private void StopLogs()
    {
        _logs?.Cancel();
        _logs?.Dispose();
        _logs = null;
        _logsFollow = string.Empty;
    }

    private async Task FollowAsync(ContainerRow row, int color, CancellationToken cancellationToken)
    {
        var service = ServiceOf(row);
        var pending = string.Empty;
        var range = new LogRange(_tailLines, null, Timestamps: false);

        try
        {
            var events = await _sender.Send(new StreamContainerLogsQuery(row.ContainerId, range), cancellationToken);
            await foreach (var logEvent in events.WithCancellation(cancellationToken))
            {
                if (logEvent is not ContainerLogEvent.Output output)
                {
                    continue;
                }

                var parts = (pending + output.Text).Split('\n');
                pending = parts[^1];
                foreach (var part in parts[..^1])
                {
                    Add(new ComposeLogLine(service, color, part.TrimEnd('\r')));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The project changed or the page closed.
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
