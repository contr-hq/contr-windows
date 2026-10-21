using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.UI;
using Contr.SharedKernel;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class KubernetesViewModelTests
{
    private static readonly DateTimeOffset _started = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly IKubernetesCluster _cluster = Substitute.For<IKubernetesCluster>();
    private readonly IDaemonSettings _settings = Substitute.For<IDaemonSettings>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly Subject<KubernetesCluster?> _clusters = new();
    private readonly Subject<DaemonSettings?> _allSettings = new();

    public KubernetesViewModelTests()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 16, "WSL 2") {
            Capabilities = new HashSet<DaemonCapability> { DaemonCapability.Kubernetes },
        });
        _cluster.Changes.Returns(_clusters);
        _settings.Changes.Returns(_allSettings);
    }

    [Fact]
    public async Task TurningOn_WaitsForTheClusterToFollowTheNewSettings()
    {
        _settings
            .UpdateAsync(default!, default)
            .ReturnsForAnyArgs(Result.Success(new DaemonSettingsChange(
                Settings(enabled: true, revision: 5), new HashSet<DaemonSetting>())));
        var viewModel = Active();
        Show(Cluster(ClusterState.Disabled, revision: 4), Settings(revision: 4));

        Assert.Equal(("Kubernetes is off", "Turn on"), (viewModel.Status.Title, viewModel.Status.Action));
        Assert.True(viewModel.Status.ActionIsAccent);

        await viewModel.SwitchAsync();

        await _settings.Received(1).UpdateAsync(
            Arg.Is<DaemonSettingsEdit>(edit => edit != null && edit.KubernetesEnabled == true),
            Arg.Any<CancellationToken>());
        Assert.Equal("Applying your change", viewModel.Status.Title);
        Assert.False(viewModel.CanAct);

        _clusters.OnNext(Cluster(ClusterState.Starting, revision: 5));

        Assert.Equal(("Starting Kubernetes", "Cancel"), (viewModel.Status.Title, viewModel.Status.Action));
        Assert.True(viewModel.CanSwitch);
    }

    [Fact]
    public void Starting_ShowsEachStepAndHowFarItGot()
    {
        var viewModel = Active();
        var cluster = Cluster(ClusterState.Starting) with {
            Steps = [
                Step(ClusterStepKind.PullImage, ClusterStepStatus.Done, "236 MB", 28),
                Step(ClusterStepKind.StartServer, ClusterStepStatus.Done, "127.0.0.1:6443", 0),
                Step(ClusterStepKind.WaitForNode, ClusterStepStatus.Running, string.Empty, null),
                Step(ClusterStepKind.AddKubeconfig, ClusterStepStatus.Pending, string.Empty, null),
            ],
        };

        Show(cluster, Settings());

        Assert.Equal(62.5, viewModel.Status.Progress);
        Assert.Equal("Starting", viewModel.Status.PillText);
        Assert.Equal(
            ["Pull k3s image", "Start the server", "Wait for node Ready", "Add kubeconfig"],
            viewModel.Steps.Select(step => step.Title));
        Assert.Equal(("236 MB · 28s", string.Empty), (viewModel.Steps[0].Detail, viewModel.Steps[0].Code));
        Assert.Equal((string.Empty, "127.0.0.1:6443"), (viewModel.Steps[1].Detail, viewModel.Steps[1].Code));
        Assert.Equal(("Context ", "contr"), (viewModel.Steps[3].Detail, viewModel.Steps[3].Code));
        Assert.True(viewModel.ShowsSteps);
        Assert.False(viewModel.CanImport);
    }

    [Fact]
    public void ARunningStep_CountsTheSecondsSoFar()
    {
        var step = Step(ClusterStepKind.WaitForNode, ClusterStepStatus.Running, string.Empty, null);

        var row = ClusterStepRow.From(step, "contr", _started.AddSeconds(42));

        Assert.Equal("42s so far", row.Detail);
    }

    [Fact]
    public void Ready_ShowsWhereKubectlReachesItAndItsPods()
    {
        var viewModel = Active();
        var cluster = Cluster(ClusterState.Ready) with {
            Version = "v1.37.1+k3s1",
            SystemPods = [
                new SystemPod("coredns-6799fbcd5", "Running", 1, 1),
                new SystemPod("helm-install", "Succeeded", 0, 1),
            ],
        };

        Show(cluster, Settings());

        Assert.True(viewModel.IsReady);
        Assert.Equal(("v1.37.1", "127.0.0.1:6443"), (viewModel.ShownVersion, viewModel.Address));
        Assert.Equal("Kubernetes is running", viewModel.Status.Title);
        Assert.Equal(PillKind.Good, viewModel.Status.PillKind);
        Assert.Equal("KUBE-SYSTEM PODS · 1 OF 2 READY", viewModel.PodsCaption);
        Assert.True(viewModel.ShowsApiServer);
        Assert.True(viewModel.CanImport);
        Assert.Equal(
            ("kubectl uses ", "staging-eu", " right now. Contr only changes that if you turn this on."),
            (viewModel.ContextLead, viewModel.CurrentContext, viewModel.ContextTail));
        Assert.True(viewModel.CanChooseContext);
    }

    [Fact]
    public void Failed_PutsTheErrorInThePagesBar()
    {
        var viewModel = Active();

        Show(Cluster(ClusterState.Failed) with { Error = "k3s exited: port 6443 is taken" }, Settings());

        Assert.True(viewModel.HasFailed);
        Assert.Equal("k3s exited: port 6443 is taken", viewModel.Error);
        Assert.Equal("Turn off", viewModel.Status.Action);
    }

    [Fact]
    public async Task ChooseVersion_ChangesOnlyADifferentVersion()
    {
        _settings
            .UpdateAsync(default!, default)
            .ReturnsForAnyArgs(Result.Success(new DaemonSettingsChange(Settings(), new HashSet<DaemonSetting>())));
        var viewModel = Active();
        Show(Cluster(ClusterState.Ready), Settings());

        await viewModel.ChooseVersionAsync("v1.37.1");
        await viewModel.ChooseVersionAsync("v1.36.5");

        await _settings.Received(1).UpdateAsync(Arg.Any<DaemonSettingsEdit>(), Arg.Any<CancellationToken>());
        await _settings.Received(1).UpdateAsync(
            Arg.Is<DaemonSettingsEdit>(edit => edit != null && edit.KubernetesVersion == "v1.36.5"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ALockedVersion_CannotChange()
    {
        var viewModel = Active();

        Show(Cluster(ClusterState.Ready), Settings(locked: [DaemonSetting.KubernetesVersion]));

        Assert.True(viewModel.IsVersionLocked);
        Assert.False(viewModel.CanChooseVersion);
        Assert.True(viewModel.CanChooseTraefik);
    }

    [Fact]
    public async Task ChooseContext_ShowsTheClusterTheDaemonReturns()
    {
        var current = Cluster(ClusterState.Ready) with { CurrentContext = "contr" };
        _cluster
            .SetCurrentContextAsync(true, Arg.Any<CancellationToken>())
            .Returns(Result.Success(current));
        var viewModel = Active();
        Show(Cluster(ClusterState.Ready), Settings());

        await viewModel.ChooseContextAsync(true);

        Assert.True(viewModel.IsCurrentContext);
        Assert.StartsWith(". Turning this off", viewModel.ContextTail, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutTheCapability_ThePageSaysSo()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 16, "Fake"));

        var viewModel = Active();

        Assert.True(viewModel.IsNotOffered);
        Assert.False(viewModel.CanAct);
    }

    private KubernetesViewModel Active()
    {
        var viewModel = new KubernetesViewModel(_cluster, _settings, _daemon, _notifications);
        viewModel.Activate();

        return viewModel;
    }

    private void Show(KubernetesCluster cluster, DaemonSettings settings)
    {
        _allSettings.OnNext(settings);
        _clusters.OnNext(cluster);
    }

    private static KubernetesCluster Cluster(ClusterState state, ulong revision = 1)
    {
        return new KubernetesCluster(
            state, string.Empty, "https://127.0.0.1:6443", @"C:\Users\dev\.kube\config", "contr", "staging-eu") {
            AvailableVersions = ["v1.37.1", "v1.36.5"],
            SettingsRevision = revision,
            StartedAt = _started,
        };
    }

    private static DaemonSettings Settings(
        bool enabled = false, ulong revision = 1, DaemonSetting[]? locked = null)
    {
        return new DaemonSettings(false, true, new HashSet<DaemonSetting>(locked ?? [])) {
            Kubernetes = new KubernetesSettings(enabled, string.Empty, false),
            Revision = revision,
        };
    }

    private static ClusterStep Step(ClusterStepKind kind, ClusterStepStatus status, string detail, int? took)
    {
        DateTimeOffset? finished = took is { } seconds ? _started.AddSeconds(seconds) : null;

        return new ClusterStep(kind, status, detail, _started, finished);
    }
}
