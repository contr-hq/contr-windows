using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Daemon;
using Contr.Sdk;
using Contr.Sdk.Images.V1;
using Contr.Sdk.Kubernetes.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ClusterState = Contr.UI.Contracts.Services.Daemon.ClusterState;
using WireCluster = Contr.Sdk.Kubernetes.V1.Cluster;
using WireState = Contr.Sdk.Kubernetes.V1.ClusterState;

namespace Contr.Daemon.UI;

/// <summary><see cref="IKubernetesCluster"/> over KubernetesService.</summary>
public sealed partial class KubernetesClient : IKubernetesCluster
{
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly IDaemonClient _daemon;
    private readonly ILogger<KubernetesClient> _logger;
    private readonly IObservable<KubernetesCluster?> _changes;

    public KubernetesClient(IDaemonConnection connection, IDaemonClient daemon, ILogger<KubernetesClient> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(logger);
        _daemon = daemon;
        _logger = logger;
        _changes = connection.StatusChanges
            .Select(status => status.Connection == DaemonConnectionState.Connected
                && connection.Identity?.Capabilities.Contains(DaemonCapability.Kubernetes) == true)
            .DistinctUntilChanged()
            .Select(offered => offered ? Watch() : Observable.Return<KubernetesCluster?>(null))
            .Switch()
            .Replay(1)
            .RefCount();
    }

    public IObservable<KubernetesCluster?> Changes => _changes;

    public Task<Result> ResetAsync(CancellationToken cancellationToken)
    {
        return _daemon.RunAsync(
            (client, token) => client.Kubernetes
                .ResetClusterAsync(new ResetClusterRequest(), cancellationToken: token)
                .ResponseAsync,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<ImageImport>>> ImportImagesAsync(
        IReadOnlyList<string> images, IProgress<ImageImport> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(progress);

        var request = new ImportImagesRequest { Images = { images } };
        List<ImageImport> imports = [];
        var outcomes = _daemon.StreamAsync(
            (client, token) => client.Kubernetes.ImportImages(request, cancellationToken: token),
            response => new ImportOutcome(new ImageImport(response.Image, response.Error), null),
            error => new ImportOutcome(null, error),
            cancellationToken);

        await foreach (var outcome in outcomes.ConfigureAwait(false))
        {
            if (outcome.Error is { } error)
            {
                return Result.Failure<IReadOnlyList<ImageImport>>(error);
            }

            if (outcome.Import is { } import)
            {
                imports.Add(import);
                progress.Report(import);
            }
        }

        return Result.Success<IReadOnlyList<ImageImport>>(imports);
    }

    public async Task<Result<KubernetesCluster>> SetCurrentContextAsync(
        bool current, CancellationToken cancellationToken)
    {
        var request = new SetCurrentContextRequest { Current = current };
        var result = await _daemon
            .CallAsync(
                async (client, token) => await client.Kubernetes
                    .SetCurrentContextAsync(request, cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<KubernetesCluster>(result.Error)
            : Result.Success(ToCluster(result.Value.Cluster ?? new WireCluster()));
    }

    public async Task<Result<IReadOnlyList<string>>> ListEngineImagesAsync(CancellationToken cancellationToken)
    {
        var result = await _daemon
            .CallAsync(
                async (client, token) => await client.Images
                    .ListImagesAsync(new ListImagesRequest(), cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<IReadOnlyList<string>>(result.Error)
            : Result.Success(TaggedImages(result.Value.Images));
    }

    /// <summary>Every tag of <paramref name="images"/>, sorted; untagged images have nothing to import by.</summary>
    public static IReadOnlyList<string> TaggedImages(IEnumerable<Image> images)
    {
        ArgumentNullException.ThrowIfNull(images);

        return [.. images
            .SelectMany(image => image.RepoTags)
            .Where(tag => tag.Length > 0 && tag != "<none>:<none>")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    public static KubernetesCluster ToCluster(WireCluster cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        return new KubernetesCluster(
            ToState(cluster.State),
            cluster.Version,
            cluster.ApiServer,
            cluster.Kubeconfig,
            cluster.Context,
            cluster.CurrentContext) {
            StartedAt = Time(cluster.StartedAt),
            Steps = [.. cluster.Steps.Select(ToStep)],
            SystemPods = [.. cluster.SystemPods.Select(pod => new SystemPod(
                pod.Name, pod.Phase, (int)pod.ReadyContainers, (int)pod.Containers))],
            AvailableVersions = [.. cluster.AvailableVersions],
            Error = cluster.Error,
            SettingsRevision = ulong.TryParse(
                cluster.SettingsEtag, NumberStyles.None, CultureInfo.InvariantCulture, out var revision)
                ? revision
                : 0,
        };
    }

    private static ClusterState ToState(WireState state)
    {
        return state switch {
            WireState.WaitingForEngine => ClusterState.WaitingForEngine,
            WireState.Starting => ClusterState.Starting,
            WireState.Ready => ClusterState.Ready,
            WireState.Stopping => ClusterState.Stopping,
            WireState.Failed => ClusterState.Failed,
            _ => ClusterState.Disabled,
        };
    }

    private static ClusterStep ToStep(Step step)
    {
        var kind = step.Kind switch {
            StepKind.StartServer => ClusterStepKind.StartServer,
            StepKind.WaitForNode => ClusterStepKind.WaitForNode,
            StepKind.AddKubeconfig => ClusterStepKind.AddKubeconfig,
            _ => ClusterStepKind.PullImage,
        };
        var status = step.Status switch {
            StepStatus.Running => ClusterStepStatus.Running,
            StepStatus.Done => ClusterStepStatus.Done,
            StepStatus.Failed => ClusterStepStatus.Failed,
            _ => ClusterStepStatus.Pending,
        };

        return new ClusterStep(kind, status, step.Detail, Time(step.StartedAt), Time(step.FinishedAt));
    }

    private static DateTimeOffset? Time(Timestamp? timestamp)
    {
        return timestamp is null ? null : timestamp.ToDateTimeOffset();
    }

    /// <summary>The cluster until the connection ends; a broken stream opens again after a pause.</summary>
    private IObservable<KubernetesCluster?> Watch()
    {
        return Observable.Create<KubernetesCluster?>(async (observer, cancellationToken) => {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _daemon.GetAsync(cancellationToken).ConfigureAwait(false);
                    using var call = client.Kubernetes.WatchCluster(
                        new WatchClusterRequest(), cancellationToken: cancellationToken);
                    var responses = call.ResponseStream.ReadAllAsync(cancellationToken);
                    await foreach (var response in responses.ConfigureAwait(false))
                    {
                        observer.OnNext(ToCluster(response.Cluster ?? new WireCluster()));
                    }
                }
                catch (RpcException exception) when (exception.StatusCode != StatusCode.Cancelled)
                {
                    LogWatchEnded(exception.Status.Detail);
                }
                catch (ContrException exception)
                {
                    LogWatchEnded(exception.Message);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                observer.OnNext(null);

                try
                {
                    await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The cluster stream ended: {Reason}")]
    private partial void LogWatchEnded(string reason);

    /// <summary>One message of an import stream: an image's outcome, or why the stream broke.</summary>
    private sealed record ImportOutcome(ImageImport? Import, Error? Error);
}
