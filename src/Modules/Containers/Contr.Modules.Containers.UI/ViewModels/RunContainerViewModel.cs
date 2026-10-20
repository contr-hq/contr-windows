using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Commands.CreateContainer;
using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>A port of the Run dialog as typed: the side on this PC and the container's.</summary>
public sealed partial class PortEntry : ObservableObject
{
    [ObservableProperty]
    public partial string Host { get; set; }

    [ObservableProperty]
    public partial string Container { get; set; }

    public PortEntry()
    {
        Host = string.Empty;
        Container = string.Empty;
    }

    public bool IsBlank => Host.Trim().Length == 0 && Container.Trim().Length == 0;
}

/// <summary>A folder or volume of the Run dialog as typed.</summary>
public sealed partial class MountEntry : ObservableObject
{
    /// <summary>A Windows path, a volume's name, or nothing for a new anonymous volume.</summary>
    [ObservableProperty]
    public partial string Source { get; set; }

    [ObservableProperty]
    public partial string Destination { get; set; }

    [ObservableProperty]
    public partial bool ReadOnly { get; set; }

    public MountEntry()
    {
        Source = string.Empty;
        Destination = string.Empty;
    }

    public bool IsBlank => Source.Trim().Length == 0 && Destination.Trim().Length == 0;

    /// <summary>Whether <see cref="Source"/> names a folder on this PC rather than a volume.</summary>
    public bool IsFolder => FolderPattern().IsMatch(Source.Trim());

    [GeneratedRegex(@"^([A-Za-z]:|\\\\|\.{1,2}[\\/])", RegexOptions.CultureInvariant)]
    private static partial Regex FolderPattern();
}

/// <summary>The Run dialog: a container from a local image, and the <c>docker run</c> command it amounts to.</summary>
public sealed partial class RunContainerViewModel : ObservableObject
{
    private const string _detailPageKey = "ContainerDetail";

    private static readonly RestartMode[] _restartModes =
        [RestartMode.No, RestartMode.Always, RestartMode.UnlessStopped, RestartMode.OnFailure];

    private readonly ISender _sender;
    private readonly INavigationService _navigation;
    private readonly INotificationService _notifications;
    private readonly IClipboardService _clipboard;

    /// <summary>Set once every field has its first value; the preview waits for it.</summary>
    private readonly bool _isReady;

    /// <summary>A local image's reference, such as <c>nginx:1.27-alpine</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    public partial string Image { get; set; }

    /// <summary>Empty lets the engine choose a name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary><c>VAR=value</c> entries, one per line.</summary>
    [ObservableProperty]
    public partial string Environment { get; set; }

    [ObservableProperty]
    public partial string Network { get; set; }

    /// <summary>No, Always, Unless stopped or On failure.</summary>
    [ObservableProperty]
    public partial int RestartIndex { get; set; }

    /// <summary>Replaces the image's command; empty keeps it.</summary>
    [ObservableProperty]
    public partial string Command { get; set; }

    [ObservableProperty]
    public partial bool AutoRemove { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    public partial bool IsRunning { get; set; }

    /// <summary>The <c>docker run</c> command that creates the same container.</summary>
    [ObservableProperty]
    public partial string Preview { get; set; }

    [ObservableProperty]
    public partial bool HasFolder { get; set; }

    public RunContainerViewModel(
        ISender sender, INavigationService navigation, INotificationService notifications, IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(clipboard);
        _sender = sender;
        _navigation = navigation;
        _notifications = notifications;
        _clipboard = clipboard;
        Image = string.Empty;
        Name = string.Empty;
        Environment = string.Empty;
        Network = string.Empty;
        Command = string.Empty;
        Preview = string.Empty;
        Ports.CollectionChanged += OnEntriesChanged;
        Mounts.CollectionChanged += OnEntriesChanged;
        AddPort();
        AddMount();
        _isReady = true;
        Refresh();
    }

    public ObservableCollection<PortEntry> Ports { get; } = [];

    public ObservableCollection<MountEntry> Mounts { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(Error);

    public bool CanRun => !IsRunning && Image.Trim().Length > 0;

    public void AddPort()
    {
        Ports.Add(new PortEntry());
    }

    public void RemovePort(PortEntry entry)
    {
        Ports.Remove(entry);
    }

    public void AddMount()
    {
        Mounts.Add(new MountEntry());
    }

    public void RemoveMount(MountEntry entry)
    {
        Mounts.Remove(entry);
    }

    public void CopyCommand()
    {
        _clipboard.SetText(Preview);
    }

    /// <summary>The container as typed; the first entry the engine would refuse fails it.</summary>
    public Result<ContainerSpec> Build()
    {
        return Collect(strict: true);
    }

    /// <summary>Creates and starts the container, then opens its page.</summary>
    /// <returns>Whether it runs; otherwise <see cref="Error"/> says why, and the dialog stays open.</returns>
    public async Task<bool> RunAsync()
    {
        var spec = Build();

        if (spec.IsFailure)
        {
            Error = spec.Error.Message;

            return false;
        }

        IsRunning = true;
        try
        {
            var created = await _sender.Send(new CreateContainerCommand(spec.Value));

            if (created.IsFailure)
            {
                Error = created.Error.Message;

                return false;
            }

            Error = null;

            if (created.Value.Warnings.Count > 0)
            {
                _notifications.Show(
                    string.Join(" ", created.Value.Warnings), InfoBarSeverity.Warning, "The engine warned");
            }

            _navigation.NavigateTo(_detailPageKey, created.Value.Id, ContainerDetailTab.Logs);

            return true;
        }
        finally
        {
            IsRunning = false;
        }
    }

    partial void OnImageChanged(string value)
    {
        Refresh();
    }

    partial void OnNameChanged(string value)
    {
        Refresh();
    }

    partial void OnEnvironmentChanged(string value)
    {
        Refresh();
    }

    partial void OnNetworkChanged(string value)
    {
        Refresh();
    }

    partial void OnRestartIndexChanged(int value)
    {
        Refresh();
    }

    partial void OnCommandChanged(string value)
    {
        Refresh();
    }

    partial void OnAutoRemoveChanged(bool value)
    {
        Refresh();
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var entry in e.NewItems?.OfType<INotifyPropertyChanged>() ?? [])
        {
            entry.PropertyChanged += OnEntryChanged;
        }

        foreach (var entry in e.OldItems?.OfType<INotifyPropertyChanged>() ?? [])
        {
            entry.PropertyChanged -= OnEntryChanged;
        }

        Refresh();
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        Refresh();
    }

    /// <summary>The preview leaves out what does not parse yet.</summary>
    private void Refresh()
    {
        if (!_isReady)
        {
            return;
        }

        Preview = DockerRunCommand.Format(Collect(strict: false).Value);
        HasFolder = Mounts.Any(mount => mount.IsFolder);
    }

    private Result<ContainerSpec> Collect(bool strict)
    {
        ContainerName? name = null;

        if (Name.Trim().Length > 0)
        {
            var parsed = ContainerName.Parse(Name);

            if (parsed.IsSuccess)
            {
                name = parsed.Value;
            }
            else if (strict)
            {
                return Result.Failure<ContainerSpec>(parsed.Error);
            }
        }

        var ports = new List<PortBinding>();

        foreach (var entry in Ports.Where(entry => !entry.IsBlank))
        {
            var port = PortBinding.Parse(entry.Host, entry.Container);

            if (port.IsSuccess)
            {
                ports.Add(port.Value);
            }
            else if (strict)
            {
                return Result.Failure<ContainerSpec>(port.Error);
            }
        }

        var mounts = Mounts
            .Where(entry => !entry.IsBlank)
            .Select(ToMount)
            .ToList();
        var spec = new ContainerSpec(
            Image.Trim(),
            name,
            ports,
            mounts,
            [.. Lines(Environment)],
            CommandLine.Split(Command),
            Network.Trim(),
            new RestartPolicy(_restartModes[Math.Clamp(RestartIndex, 0, _restartModes.Length - 1)]),
            AutoRemove);

        return Result.Success(spec);
    }

    private static MountRequest ToMount(MountEntry entry)
    {
        var source = entry.Source.Trim();
        var kind = entry.IsFolder ? MountSource.Bind : MountSource.Volume;

        return new MountRequest(kind, source, entry.Destination.Trim(), entry.ReadOnly);
    }

    private static IEnumerable<string> Lines(string text)
    {
        return text
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);
    }
}
