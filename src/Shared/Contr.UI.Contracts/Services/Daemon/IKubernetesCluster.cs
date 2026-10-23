using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.SharedKernel;

namespace Contr.UI.Contracts.Services.Daemon;

/// <summary>
/// The daemon's single-node Kubernetes cluster: k3s as a container on the engine. The settings
/// <see cref="DaemonSetting.KubernetesEnabled"/>, <see cref="DaemonSetting.KubernetesVersion"/> and
/// <see cref="DaemonSetting.KubernetesTraefik"/> decide what runs.
/// </summary>
public interface IKubernetesCluster
{
    /// <summary>
    /// The cluster, then again after every change; absent while no daemon is connected or where it runs no cluster.
    /// </summary>
    /// <remarks>Emits on a background thread; subscribers share one stream from the daemon.</remarks>
    IObservable<KubernetesCluster?> Changes { get; }

    /// <summary>Deletes the cluster's workloads and data, then starts a new cluster when Kubernetes is on; images
    /// stay.</summary>
    Task<Result> ResetAsync(CancellationToken cancellationToken);

    /// <summary>Copies images from the engine into a ready cluster, so pods use them without a registry.</summary>
    /// <param name="images">References on the engine, such as <c>shop-api:dev</c>.</param>
    /// <param name="progress">Told each image's outcome as the daemon reports it.</param>
    /// <param name="cancellationToken">Stops following; from API revision 22 the import goes on, and before it the
    /// import stops.</param>
    /// <returns>Every image's outcome; a failure when the daemon refused the import, such as before the cluster is
    /// ready.</returns>
    Task<Result<IReadOnlyList<ImageImport>>> ImportImagesAsync(
        IReadOnlyList<string> images, IProgress<ImageImport> progress, CancellationToken cancellationToken);

    /// <summary>Follows an import already running, such as one a page started before it closed.</summary>
    /// <param name="operationId">The import's <see cref="DaemonOperation.Id"/>.</param>
    /// <param name="progress">Told each image's outcome, the ones the daemon kept first.</param>
    /// <param name="cancellationToken">Stops following; the import goes on.</param>
    /// <returns>Every image's outcome; a failure when the import failed or was cancelled.</returns>
    Task<Result<IReadOnlyList<ImageImport>>> FollowImportAsync(
        string operationId, IProgress<ImageImport> progress, CancellationToken cancellationToken);

    /// <summary>Makes the cluster's context kubectl's current one, or restores the one that was current before.
    /// </summary>
    Task<Result<KubernetesCluster>> SetCurrentContextAsync(bool current, CancellationToken cancellationToken);

    /// <summary>The engine's tagged images, which <see cref="ImportImagesAsync"/> can copy, sorted.</summary>
    Task<Result<IReadOnlyList<string>>> ListEngineImagesAsync(CancellationToken cancellationToken);
}

/// <summary>What the cluster is doing.</summary>
public enum ClusterState
{
    /// <summary>Kubernetes is off.</summary>
    Disabled,

    /// <summary>Kubernetes is on and waits for the engine to run.</summary>
    WaitingForEngine,

    /// <summary>The cluster is turning on; <see cref="KubernetesCluster.Steps"/> say how far it got.</summary>
    Starting,

    /// <summary>The node is ready and kubectl reaches the cluster.</summary>
    Ready,

    /// <summary>The cluster's container is stopping, such as for a reset.</summary>
    Stopping,

    /// <summary>The cluster failed; <see cref="KubernetesCluster.Error"/> says why.</summary>
    Failed,
}

/// <summary>A step of turning the cluster on, in the order they run.</summary>
public enum ClusterStepKind
{
    /// <summary>Pulls the k3s image onto the engine.</summary>
    PullImage,

    /// <summary>Starts the k3s server's container.</summary>
    StartServer,

    /// <summary>Waits for the node to report Ready.</summary>
    WaitForNode,

    /// <summary>Adds the cluster's context to the kubeconfig file.</summary>
    AddKubeconfig,
}

/// <summary>How far a step of turning the cluster on got.</summary>
public enum ClusterStepStatus
{
    /// <summary>Not started.</summary>
    Pending,

    /// <summary>Under way.</summary>
    Running,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>Failed; the cluster failed with it.</summary>
    Failed,
}

/// <summary>The cluster as the daemon reports it.</summary>
/// <param name="State">What the cluster is doing.</param>
/// <param name="Version">The running k3s version, such as <c>v1.37.1+k3s1</c>; empty while none runs.</param>
/// <param name="ApiServer">Where kubectl reaches the API server, such as <c>https://127.0.0.1:6443</c>.</param>
/// <param name="Kubeconfig">The kubeconfig file that holds the cluster's context.</param>
/// <param name="Context">The cluster's context in that file, such as <c>contr</c>.</param>
/// <param name="CurrentContext">kubectl's current context; empty when the file names none.</param>
public sealed record KubernetesCluster(
    ClusterState State, string Version, string ApiServer, string Kubeconfig, string Context, string CurrentContext)
{
    /// <summary>When the running server started; absent while none runs.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>The steps of turning the cluster on; empty unless it is starting or failed to start.</summary>
    public IReadOnlyList<ClusterStep> Steps { get; init; } = [];

    /// <summary>The kube-system pods, sorted by name; empty unless the cluster is ready.</summary>
    public IReadOnlyList<SystemPod> SystemPods { get; init; } = [];

    /// <summary>The versions <see cref="DaemonSetting.KubernetesVersion"/> takes, newest first; the first is the
    /// default.</summary>
    public IReadOnlyList<string> AvailableVersions { get; init; } = [];

    /// <summary>Why the cluster failed; empty unless it did.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>The <see cref="DaemonSettings.Revision"/> this state follows: it reflects every change up to that
    /// revision.</summary>
    public ulong SettingsRevision { get; init; }

    /// <summary>Whether kubectl uses the cluster's context unless told otherwise.</summary>
    public bool IsCurrentContext => Context.Length > 0 && Context == CurrentContext;
}

/// <summary>A step of turning the cluster on.</summary>
/// <param name="Kind">Which step.</param>
/// <param name="Status">How far it got.</param>
/// <param name="Detail">What the step did or is doing, such as <c>378 MB</c> or <c>127.0.0.1:6443</c>.</param>
/// <param name="StartedAt">When it started; absent while pending.</param>
/// <param name="FinishedAt">When it finished; absent until then.</param>
public sealed record ClusterStep(
    ClusterStepKind Kind,
    ClusterStepStatus Status,
    string Detail,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);

/// <summary>A pod of the cluster's kube-system namespace.</summary>
/// <param name="Name">The pod's name.</param>
/// <param name="Phase">The Kubernetes pod phase, such as <c>Running</c> or <c>Succeeded</c>.</param>
/// <param name="ReadyContainers">Its containers that are ready.</param>
/// <param name="Containers">All its containers.</param>
public sealed record SystemPod(string Name, string Phase, int ReadyContainers, int Containers);

/// <summary>One image's outcome of an import into the cluster.</summary>
/// <param name="Image">The image's reference on the engine.</param>
/// <param name="Error">Why the image was not imported; empty when it was.</param>
public sealed record ImageImport(string Image, string Error)
{
    /// <summary>Whether the cluster has the image now.</summary>
    public bool IsImported => Error.Length == 0;
}
