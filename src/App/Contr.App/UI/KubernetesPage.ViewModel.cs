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
using Contr.UI.Common.Lists;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>One step of turning the cluster on, as the status card lists it.</summary>
/// <param name="Detail">Plain text under the title, such as <c>236 MB · 28s</c> or <c>Context </c>.</param>
/// <param name="Code">An identifier after <paramref name="Detail"/>, such as <c>127.0.0.1:6443</c>.</param>
public sealed record ClusterStepRow(string Title, string Detail, string Code, ClusterStepStatus Status)
{
    public bool IsDone => Status == ClusterStepStatus.Done;

    public bool IsRunning => Status == ClusterStepStatus.Running;

    public bool IsPending => Status == ClusterStepStatus.Pending;

    public bool IsFailed => Status == ClusterStepStatus.Failed;

    public static string TitleOf(ClusterStepKind kind)
    {
        return kind switch {
            ClusterStepKind.PullImage => "Pull k3s image",
            ClusterStepKind.StartServer => "Start the server",
            ClusterStepKind.WaitForNode => "Wait for node Ready",
            _ => "Add kubeconfig",
        };
    }

    public static ClusterStepRow From(ClusterStep step, string context, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        var title = TitleOf(step.Kind);

        if (step.Kind == ClusterStepKind.AddKubeconfig && step.Detail.Length == 0 && context.Length > 0)
        {
            return new ClusterStepRow(title, "Context ", context, step.Status);
        }

        var took = step switch {
            { Status: ClusterStepStatus.Running, StartedAt: { } started } =>
                $"{HumanFormat.Duration(now - started)} so far",
            { StartedAt: { } started, FinishedAt: { } finished } when finished - started >= TimeSpan.FromSeconds(1)
                => HumanFormat.Duration(finished - started),
            _ => string.Empty,
        };

        // The server's address reads as an identifier.
        if (step.Kind == ClusterStepKind.StartServer)
        {
            return new ClusterStepRow(title, took.Length > 0 ? $"{took} · " : string.Empty, step.Detail, step.Status);
        }

        var detail = string.Join(" · ", new[] { step.Detail, took }.Where(part => part.Length > 0));

        return new ClusterStepRow(title, detail, string.Empty, step.Status);
    }
}

/// <summary>One kube-system pod of a ready cluster.</summary>
public sealed record SystemPodRow(string Name, string Phase, string ReadyText, PillKind PillKind)
{
    public static SystemPodRow From(SystemPod pod)
    {
        ArgumentNullException.ThrowIfNull(pod);

        var ready = pod.Containers > 0 && pod.ReadyContainers == pod.Containers;
        var kind = pod.Phase switch {
            "Running" => ready ? PillKind.Good : PillKind.Busy,
            "Succeeded" => PillKind.Hollow,
            "Pending" => PillKind.Busy,
            _ => PillKind.Critical,
        };

        return new SystemPodRow(
            pod.Name,
            pod.Phase,
            string.Create(CultureInfo.CurrentCulture, $"{pod.ReadyContainers}/{pod.Containers} ready"),
            kind);
    }
}

/// <summary>What the status card says about the cluster.</summary>
/// <param name="Action">The card's button, such as <c>Turn on</c> or <c>Cancel</c>; empty without one.</param>
/// <param name="ActionTurnsOn">The button turns Kubernetes on rather than off.</param>
/// <param name="Progress">From 0 to 100 while starting; absent where the progress bar runs without one.</param>
public sealed record ClusterStatus(
    string Title,
    string Message,
    string PillText,
    PillKind PillKind,
    string Action,
    bool ActionTurnsOn,
    bool ShowsProgress,
    double? Progress)
{
    public bool HasAction => Action.Length > 0;

    public bool ActionIsAccent => HasAction && ActionTurnsOn;

    public bool ActionIsPlain => HasAction && !ActionTurnsOn;

    public bool IsIndeterminate => ShowsProgress && Progress is null;

    public double ProgressValue => Progress ?? 0;

    /// <summary>The card for <paramref name="cluster"/>.</summary>
    /// <param name="version">Such as <c>v1.37.1</c>; empty when unknown.</param>
    /// <param name="isApplying">The cluster predates the user's last change.</param>
    public static ClusterStatus From(KubernetesCluster? cluster, string version, bool isApplying)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (cluster is null)
        {
            return new ClusterStatus(
                "Waiting for the daemon",
                "The cluster shows here as soon as the daemon reports it.",
                "Unknown",
                PillKind.Hollow,
                string.Empty,
                false,
                true,
                null);
        }

        if (isApplying)
        {
            return new ClusterStatus(
                "Applying your change",
                "The cluster follows the new settings in a moment.",
                "Applying",
                PillKind.Busy,
                string.Empty,
                false,
                true,
                null);
        }

        var k3s = version.Length > 0 ? $"k3s {version}" : "k3s";

        return cluster.State switch {
            ClusterState.Disabled => new ClusterStatus(
                "Kubernetes is off",
                "Turn it on to run a single-node k3s cluster as a container on your engine.",
                "Off",
                PillKind.Hollow,
                "Turn on",
                true,
                false,
                null),
            ClusterState.WaitingForEngine => new ClusterStatus(
                "Waiting for the engine",
                "Kubernetes starts when the engine runs.",
                "Waiting for engine",
                PillKind.Hollow,
                "Turn off",
                false,
                false,
                null),
            ClusterState.Starting => new ClusterStatus(
                "Starting Kubernetes",
                $"{k3s} runs as a container on your engine. This usually takes under 3 minutes.",
                "Starting",
                PillKind.Busy,
                "Cancel",
                false,
                true,
                StepProgress(cluster.Steps)),
            ClusterState.Ready => new ClusterStatus(
                "Kubernetes is running",
                $"{k3s} runs as a container on your engine.",
                "Running",
                PillKind.Good,
                "Turn off",
                false,
                false,
                null),
            ClusterState.Stopping => new ClusterStatus(
                "Stopping Kubernetes",
                "The cluster's container stops; its data stays unless you reset it.",
                "Stopping",
                PillKind.Busy,
                string.Empty,
                false,
                true,
                null),
            _ => new ClusterStatus(
                "Kubernetes didn't start",
                "Reset recreates the cluster, or turn Kubernetes off.",
                "Failed",
                PillKind.Critical,
                "Turn off",
                false,
                false,
                null),
        };
    }

    /// <summary>Done steps count whole and the running one half, out of every step.</summary>
    private static double StepProgress(IReadOnlyList<ClusterStep> steps)
    {
        if (steps.Count == 0)
        {
            return 0;
        }

        var done = steps.Count(step => step.Status == ClusterStepStatus.Done)
            + (steps.Any(step => step.Status == ClusterStepStatus.Running) ? 0.5 : 0);

        return 100 * done / steps.Count;
    }
}

/// <summary>
/// Backs the Kubernetes page: the daemon's cluster, the settings that decide it, and the commands that maintain it.
/// </summary>
public sealed partial class KubernetesViewModel : ViewModel
{
    private const string _defaultKubeconfig = @"%USERPROFILE%\.kube\config";

    /// <summary>Every property that follows the cluster, the settings or a pending call.</summary>
    private static readonly string[] _derived = [
        nameof(State), nameof(IsApplying), nameof(IsReady), nameof(HasFailed), nameof(Error), nameof(ShowsSteps),
        nameof(ShowsPods), nameof(PodsCaption), nameof(ChosenVersion), nameof(ShownVersion),
        nameof(IsUnderway), nameof(HasShownVersion), nameof(Address), nameof(HasAddress), nameof(ApiServer), nameof(ShowsApiServer),
        nameof(Uptime), nameof(IsEnabledLocked), nameof(IsVersionLocked), nameof(IsTraefikLocked),
        nameof(CanAct), nameof(CanSwitch), nameof(CanChooseVersion), nameof(CanChooseTraefik), nameof(UsesTraefik),
        nameof(IsCurrentContext), nameof(CanChooseContext), nameof(ContextLead), nameof(CurrentContext),
        nameof(ContextTail), nameof(KubeconfigText), nameof(KubeconfigLead), nameof(KubeconfigTail),
        nameof(KubeconfigFolder), nameof(CanOpenKubeconfig), nameof(CanImport), nameof(CanReset),
    ];

    private const string _importLead =
        "Copies images from your engine into the cluster, so pods can use them without a registry.";

    private readonly IKubernetesCluster _cluster;
    private readonly IDaemonSettings _settings;
    private readonly IDaemonConnection _daemon;
    private readonly INotificationService _notifications;
    private readonly IActivity _activity;
    private readonly DispatcherQueue? _dispatcher;

    /// <summary>Cancelled when the page's view model goes; an import goes on in the daemon.</summary>
    private readonly CancellationTokenSource _lifetime = new();

    private IDisposable? _clusterChanges;
    private IDisposable? _settingsChanges;
    private IDisposable? _operations;

    /// <summary>The import operation the page follows; absent while it follows none.</summary>
    private string? _import;

    /// <summary>Imports the page reported, which the daemon may still list as running for a moment.</summary>
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private DispatcherQueueTimer? _clock;

    /// <summary>The settings revision the user's last change made; a cluster state older than it is stale.</summary>
    private ulong _awaitedRevision;

    [ObservableProperty]
    public partial KubernetesCluster? Cluster { get; set; }

    [ObservableProperty]
    public partial DaemonSettings? Settings { get; set; }

    /// <summary>Whether a call the user made has not returned yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    public partial bool IsBusy { get; set; }

    /// <summary>What the import card says: what it does, or how far an import got.</summary>
    [ObservableProperty]
    public partial string ImportDescription { get; set; }

    /// <summary>Whether the daemon runs an import that the page follows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    public partial bool IsImporting { get; set; }

    [ObservableProperty]
    public partial ClusterStatus Status { get; set; }

    /// <summary>Whether the daemon runs a cluster; the WSL and host backends do.</summary>
    [ObservableProperty]
    public partial bool IsOffered { get; set; }

    public KubernetesViewModel(
        IKubernetesCluster cluster,
        IDaemonSettings settings,
        IDaemonConnection daemon,
        INotificationService notifications,
        IActivity activity)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(activity);
        _cluster = cluster;
        _settings = settings;
        _daemon = daemon;
        _notifications = notifications;
        _activity = activity;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        ImportDescription = _importLead;
        Status = ClusterStatus.From(null, string.Empty, false);
        IsOffered = Offers(daemon);
    }

    public ObservableCollection<ClusterStepRow> Steps { get; } = [];

    public ObservableCollection<SystemPodRow> SystemPods { get; } = [];

    /// <summary>The versions the cluster takes, newest first; the first is the default.</summary>
    public ObservableCollection<string> Versions { get; } = [];

    public bool IsNotOffered => !IsOffered;

    public ClusterState? State => Cluster?.State;

    /// <summary>Whether the cluster shown predates the user's last change.</summary>
    public bool IsApplying => Cluster is { } cluster && cluster.SettingsRevision < _awaitedRevision;

    public bool IsReady => State == ClusterState.Ready && !IsApplying;

    public bool HasFailed => State == ClusterState.Failed && !IsApplying;

    /// <summary>Why the cluster failed; empty unless it did.</summary>
    public string Error => HasFailed ? Cluster?.Error ?? string.Empty : string.Empty;

    public bool ShowsSteps =>
        Steps.Count > 0 && State is (ClusterState.Starting or ClusterState.Failed) && !IsApplying;

    public bool ShowsPods => IsReady && SystemPods.Count > 0;

    /// <summary>Such as <c>KUBE-SYSTEM PODS · 6 OF 7 READY</c>.</summary>
    public string PodsCaption => string.Create(
        CultureInfo.CurrentCulture,
        $"KUBE-SYSTEM PODS · {SystemPods.Count(pod => pod.PillKind == PillKind.Good)} OF {SystemPods.Count} READY");

    /// <summary>The version the settings ask for, such as <c>v1.37.1</c>; the default when they name none.</summary>
    public string ChosenVersion => Settings?.Kubernetes.Version is { Length: > 0 } version
        ? version
        : Cluster?.AvailableVersions.FirstOrDefault() ?? string.Empty;

    /// <summary>The running version without k3s's build, such as <c>v1.37.1</c> for <c>v1.37.1+k3s1</c>.</summary>
    public string ShownVersion => Cluster?.Version is { Length: > 0 } running
        ? running.Split('+')[0]
        : ChosenVersion;

    /// <summary>Whether the cluster runs or is on its way, so its version and address mean something.</summary>
    public bool IsUnderway => State is ClusterState.Starting or ClusterState.Ready or ClusterState.Stopping
        or ClusterState.Failed;

    public bool HasShownVersion => IsUnderway && ShownVersion.Length > 0;

    /// <summary>The API server without its scheme, such as <c>127.0.0.1:6443</c>.</summary>
    public string Address => Cluster?.ApiServer is { Length: > 0 } server
        ? server.Replace("https://", string.Empty, StringComparison.Ordinal)
        : string.Empty;

    public bool HasAddress => IsUnderway && Address.Length > 0;

    public string ApiServer => Cluster?.ApiServer ?? string.Empty;

    public bool ShowsApiServer => IsReady && ApiServer.Length > 0;

    /// <summary>How long the cluster has run, such as <c>1 day</c>; empty unless it runs.</summary>
    public string Uptime => IsReady && Cluster?.StartedAt is { } started
        ? HumanFormat.Duration(DateTimeOffset.Now - started)
        : string.Empty;

    public bool IsEnabledLocked => Settings?.IsLocked(DaemonSetting.KubernetesEnabled) == true;

    public bool IsVersionLocked => Settings?.IsLocked(DaemonSetting.KubernetesVersion) == true;

    public bool IsTraefikLocked => Settings?.IsLocked(DaemonSetting.KubernetesTraefik) == true;

    public bool CanAct => IsOffered && Cluster is not null && Settings is not null && !IsBusy && !IsApplying;

    public bool CanSwitch => CanAct && !IsEnabledLocked && Status.HasAction;

    public bool CanChooseVersion => CanAct && !IsVersionLocked && Versions.Count > 0;

    public bool CanChooseTraefik => CanAct && !IsTraefikLocked;

    public bool UsesTraefik => Settings?.Kubernetes.Traefik == true;

    public bool IsCurrentContext => Cluster?.IsCurrentContext == true;

    /// <summary>kubectl's context moves to the cluster once it is ready, and back whenever it is current.</summary>
    public bool CanChooseContext => CanAct && (IsReady || IsCurrentContext);

    /// <summary>Before the current context's name, such as <c>kubectl uses </c>.</summary>
    public string ContextLead => Cluster?.CurrentContext is { Length: > 0 } ? "kubectl uses " : string.Empty;

    /// <summary>kubectl's current context, such as <c>staging-eu</c>.</summary>
    public string CurrentContext => Cluster?.CurrentContext ?? string.Empty;

    public string ContextTail => CurrentContext.Length == 0
        ? "kubectl has no current context. Contr only changes that if you turn this on."
        : IsCurrentContext
            ? ". Turning this off gives kubectl back the context it used before."
            : " right now. Contr only changes that if you turn this on.";

    /// <summary>The kubeconfig file, with the user's profile folder written as <c>%USERPROFILE%</c>.</summary>
    public string KubeconfigText => Cluster?.Kubeconfig is { Length: > 0 } path
        ? Shorten(path)
        : _defaultKubeconfig;

    public string KubeconfigLead => IsReady ? "Contr added its context to " : "Added to ";

    public string KubeconfigTail => IsReady ? ". A backup is kept." : " when the node is ready. A backup is kept.";

    /// <summary>The folder that holds the kubeconfig, for Open folder; empty until the daemon names the file.</summary>
    public string KubeconfigFolder => Cluster?.Kubeconfig is { Length: > 0 } path
        ? Path.GetDirectoryName(path) ?? string.Empty
        : string.Empty;

    public bool CanOpenKubeconfig => KubeconfigFolder.Length > 0 && Directory.Exists(KubeconfigFolder);

    public bool CanImport => CanAct && IsReady && !IsImporting;

    /// <summary>A reset needs a running engine and a cluster that is not on its way down.</summary>
    public bool CanReset => CanAct && State is not (ClusterState.WaitingForEngine or ClusterState.Stopping);

    /// <summary>Follows the cluster and the settings until <see cref="Deactivate"/>.</summary>
    public void Activate()
    {
        IsOffered = Offers(_daemon);
        _clusterChanges ??= _cluster.Changes.Subscribe(cluster => OnUi(() => Cluster = cluster));
        _settingsChanges ??= _settings.Changes.Subscribe(settings => OnUi(() => Settings = settings));
        _operations ??= _activity.Changes.Subscribe(operations => OnUi(() => FollowImport(operations)));

        if (_clock is null && _dispatcher is not null)
        {
            _clock = _dispatcher.CreateTimer();
            _clock.Interval = TimeSpan.FromSeconds(1);
            _clock.Tick += (_, _) => ShowSteps();
            _clock.Start();
        }
    }

    public void Deactivate()
    {
        _clusterChanges?.Dispose();
        _clusterChanges = null;
        _settingsChanges?.Dispose();
        _settingsChanges = null;
        _operations?.Dispose();
        _operations = null;
        _clock?.Stop();
        _clock = null;
    }

    /// <summary>Turns Kubernetes on or off; cancelling a start turns it off.</summary>
    public Task SwitchAsync()
    {
        return UpdateAsync(new DaemonSettingsEdit { KubernetesEnabled = Status.ActionTurnsOn });
    }

    /// <summary>Changes the cluster's version, which deletes its data; ask first.</summary>
    public Task ChooseVersionAsync(string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return version == ChosenVersion
            ? Task.CompletedTask
            : UpdateAsync(new DaemonSettingsEdit { KubernetesVersion = version });
    }

    public Task ChooseTraefikAsync(bool traefik)
    {
        return traefik == UsesTraefik
            ? Task.CompletedTask
            : UpdateAsync(new DaemonSettingsEdit { KubernetesTraefik = traefik });
    }

    public async Task ChooseContextAsync(bool current)
    {
        if (current == IsCurrentContext)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _cluster.SetCurrentContextAsync(current, CancellationToken.None);

            if (result.IsFailure)
            {
                _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "kubectl's context didn't change");

                return;
            }

            Cluster = result.Value;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Deletes the cluster's workloads and data, then starts it anew; ask first.</summary>
    public async Task ResetAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _cluster.ResetAsync(CancellationToken.None);

            if (result.IsFailure)
            {
                _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "The cluster wasn't reset");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Copies <paramref name="images"/> into the cluster and says how it went.</summary>
    public async Task<Result<IReadOnlyList<ImageImport>>> ImportAsync(
        IReadOnlyList<string> images, IProgress<ImageImport> progress)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(progress);

        IsBusy = true;
        try
        {
            var result = await _cluster.ImportImagesAsync(images, progress, CancellationToken.None);

            if (result.IsFailure)
            {
                _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "No images were imported");

                return result;
            }

            ReportImport(result.Value);

            return result;
        }
        finally
        {
            // The import the dialog followed is the one the daemon lists.
            _reported.UnionWith(_activity.Current
                .Where(operation => operation.Work is OperationWork.ImportImages)
                .Select(operation => operation.Id));
            IsBusy = false;
        }
    }

    public Task<Result<IReadOnlyList<string>>> ListEngineImagesAsync()
    {
        return _cluster.ListEngineImagesAsync(CancellationToken.None);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Deactivate();
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }

    /// <summary>Follows an import the daemon runs, such as one the page started before it closed.</summary>
    private void FollowImport(IReadOnlyList<DaemonOperation> operations)
    {
        if (IsBusy || _import is not null)
        {
            return;
        }

        var running = operations.FirstOrDefault(operation =>
            operation is { IsRunning: true, Work: OperationWork.ImportImages } && !_reported.Contains(operation.Id));

        if (running is { Work: OperationWork.ImportImages work })
        {
            _ = FollowImportAsync(running.Id, work.Images.Count);
        }
    }

    private async Task FollowImportAsync(string operationId, int count)
    {
        _import = operationId;
        IsImporting = true;
        var done = 0;
        var images = count == 1 ? "1 image" : $"{count} images";
        ImportDescription = $"Importing {images} into the cluster…";
        var progress = new Progress<ImageImport>(_ =>
            ImportDescription = $"Importing {images} into the cluster · {++done} done…");

        try
        {
            var result = await _cluster.FollowImportAsync(operationId, progress, _lifetime.Token);

            if (result.IsFailure)
            {
                _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "No images were imported");
            }
            else
            {
                ReportImport(result.Value);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The page closed; the import goes on in the daemon.
        }
        finally
        {
            _reported.Add(operationId);
            _import = null;
            IsImporting = false;
            ImportDescription = _importLead;
        }
    }

    partial void OnClusterChanged(KubernetesCluster? value)
    {
        CollectionSync.Sync(Versions, value?.AvailableVersions ?? [], version => version);
        CollectionSync.Sync(SystemPods, [.. (value?.SystemPods ?? []).Select(SystemPodRow.From)], pod => pod.Name);
        ShowSteps();
        Refresh();
    }

    partial void OnSettingsChanged(DaemonSettings? value)
    {
        Refresh();
    }

    partial void OnIsBusyChanged(bool value)
    {
        Refresh();
    }

    partial void OnIsOfferedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotOffered));
        Refresh();
    }

    private static bool Offers(IDaemonConnection daemon)
    {
        return daemon.Identity?.Capabilities.Contains(DaemonCapability.Kubernetes) == true;
    }

    private static string Shorten(string path)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return profile.Length > 0 && path.StartsWith(profile, StringComparison.OrdinalIgnoreCase)
            ? $"%USERPROFILE%{path[profile.Length..]}"
            : path;
    }

    private void ReportImport(IReadOnlyList<ImageImport> imports)
    {
        var failed = imports
            .Where(import => !import.IsImported)
            .ToList();
        var imported = imports.Count - failed.Count;

        if (failed.Count == 0)
        {
            _notifications.Show(
                imported == 1 ? "Imported 1 image into the cluster." : $"Imported {imported} images into the cluster.",
                InfoBarSeverity.Success);

            return;
        }

        _notifications.Show(
            string.Join("; ", failed.Select(import => $"{import.Image}: {import.Error}")),
            InfoBarSeverity.Error,
            imported == 0 ? "No images were imported" : $"Imported {imported}; these weren't");
    }

    private async Task UpdateAsync(DaemonSettingsEdit edit)
    {
        IsBusy = true;
        try
        {
            var result = await _settings.UpdateAsync(edit, CancellationToken.None);

            if (result.IsFailure)
            {
                _notifications.Show(
                    result.Error.Code == IDaemonSettings.SettingLockedCode
                        ? "Your organization manages this setting."
                        : result.Error.Message,
                    InfoBarSeverity.Error,
                    "Kubernetes didn't change");

                return;
            }

            _awaitedRevision = Math.Max(_awaitedRevision, result.Value.Settings.Revision);
            Settings = result.Value.Settings;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowSteps()
    {
        var context = Cluster?.Context ?? string.Empty;
        List<ClusterStepRow> steps = [.. (Cluster?.Steps ?? [])
            .Select(step => ClusterStepRow.From(step, context, DateTimeOffset.Now))];
        CollectionSync.Sync(Steps, steps, step => step.Title);
        OnPropertyChanged(nameof(ShowsSteps));
        OnPropertyChanged(nameof(Uptime));
    }

    private void Refresh()
    {
        Status = ClusterStatus.From(IsOffered ? Cluster : null, ShownVersion, IsApplying);

        foreach (var name in _derived)
        {
            OnPropertyChanged(name);
        }
    }

    private void OnUi(Action action)
    {
        // Changes emit on a background thread.
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
