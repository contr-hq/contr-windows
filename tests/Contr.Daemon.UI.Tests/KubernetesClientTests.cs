using System;
using Contr.Daemon.UI;
using Contr.Sdk.Images.V1;
using Contr.UI.Contracts.Services.Daemon;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using Wire = Contr.Sdk.Kubernetes.V1;

namespace Contr.Daemon.UI.Tests;

public sealed class KubernetesClientTests
{
    [Fact]
    public void ToCluster_ReadsTheStateStepsPodsAndRevision()
    {
        var started = new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);
        var cluster = new Wire.Cluster {
            State = Wire.ClusterState.Starting,
            ApiServer = "https://127.0.0.1:6443",
            Kubeconfig = @"C:\Users\dev\.kube\config",
            Context = "contr",
            CurrentContext = "staging-eu",
            AvailableVersions = { "v1.37.1", "v1.36.5" },
            SettingsEtag = "7",
            Steps = {
                new Wire.Step {
                    Kind = Wire.StepKind.PullImage,
                    Status = Wire.StepStatus.Done,
                    Detail = "236 MB",
                    StartedAt = Timestamp.FromDateTimeOffset(started),
                    FinishedAt = Timestamp.FromDateTimeOffset(started.AddSeconds(28)),
                },
                new Wire.Step { Kind = Wire.StepKind.WaitForNode, Status = Wire.StepStatus.Running },
            },
            SystemPods = {
                new Wire.Pod { Name = "coredns-6799fbcd5", Phase = "Running", ReadyContainers = 1, Containers = 1 },
            },
        };

        var mapped = KubernetesClient.ToCluster(cluster);

        Assert.Equal(ClusterState.Starting, mapped.State);
        Assert.Equal(7UL, mapped.SettingsRevision);
        Assert.False(mapped.IsCurrentContext);
        Assert.Equal(["v1.37.1", "v1.36.5"], mapped.AvailableVersions);
        var finished = started.AddSeconds(28);
        Assert.Equal(
            new ClusterStep(ClusterStepKind.PullImage, ClusterStepStatus.Done, "236 MB", started, finished),
            mapped.Steps[0]);
        Assert.Equal(
            (ClusterStepKind.WaitForNode, ClusterStepStatus.Running), (mapped.Steps[1].Kind, mapped.Steps[1].Status));
        Assert.Equal(new SystemPod("coredns-6799fbcd5", "Running", 1, 1), Assert.Single(mapped.SystemPods));
    }

    [Fact]
    public void TaggedImages_LeavesOutUntaggedOnes()
    {
        var tagged = new Image { RepoTags = { "shop-api:dev", "shop-api:latest" } };
        var untagged = new Image { RepoTags = { "<none>:<none>" } };
        var other = new Image { RepoTags = { "nginx:1.29" } };

        var images = KubernetesClient.TaggedImages([tagged, untagged, other]);

        Assert.Equal(["nginx:1.29", "shop-api:dev", "shop-api:latest"], images);
    }
}
