using System;
using Contr.Application.Abstractions.Daemon;
using Contr.Daemon.UI;
using Contr.UI.Contracts.Services.Daemon;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Xunit;
using ComposeV1 = Contr.Sdk.Compose.V1;
using EngineV1 = Contr.Sdk.Engine.V1;
using ImagesV1 = Contr.Sdk.Images.V1;
using Wire = Contr.Sdk.Operations.V1;
using WireCommon = Contr.Sdk.Common.V1;

namespace Contr.Daemon.UI.Tests;

public sealed class DaemonOperationsTests
{
    private static readonly DateTimeOffset _started = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToOperation_MapsARunningPull()
    {
        var operation = DaemonOperations.ToOperation(new Wire.Operation {
            Id = "op-1",
            Request = new Wire.OperationRequest {
                PullImage = new ImagesV1.PullImageRequest { Reference = "postgres:17", Platform = "linux/arm64" },
            },
            State = Wire.OperationState.Running,
            Progress = new WireCommon.Progress { Message = "87 MB of 141 MB", Fraction = 0.6f },
            StartedAt = Timestamp.FromDateTimeOffset(_started),
            Cancellable = true,
        });

        Assert.Equal("op-1", operation.Id);
        Assert.Equal(new OperationWork.PullImage("postgres:17", "linux/arm64"), operation.Work);
        Assert.True(operation.IsRunning);
        Assert.True(operation.IsCancellable);
        Assert.Equal("87 MB of 141 MB", operation.Progress?.Message);
        Assert.Equal(0.6, operation.Progress?.Fraction ?? 0, 3);
        Assert.Equal(_started, operation.StartedAt);
        Assert.Null(operation.FinishedAt);
    }

    [Fact]
    public void ToOperation_MapsAComposeActionWithItsServices()
    {
        var operation = DaemonOperations.ToOperation(new Wire.Operation {
            Id = "op-2",
            Request = new Wire.OperationRequest {
                ComposeAction = new ComposeV1.RunActionRequest {
                    Project = "shop",
                    Action = ComposeV1.Action.Restart,
                    Services = { "api" },
                },
            },
            State = Wire.OperationState.Succeeded,
        });

        var work = Assert.IsType<OperationWork.ComposeAction>(operation.Work);
        Assert.Equal("shop", work.Project);
        Assert.Equal("restart", work.Action);
        Assert.Equal(["api"], work.Services);
        Assert.Equal(OperationState.Succeeded, operation.State);
    }

    [Fact]
    public void ToOperation_MapsAFactoryResetAndUnknownWork()
    {
        var reset = DaemonOperations.ToOperation(new Wire.Operation {
            Request = new Wire.OperationRequest {
                ResetEngine = new EngineV1.ResetRequest { Scope = EngineV1.ResetScope.Factory },
            },
        });
        var unknown = DaemonOperations.ToOperation(new Wire.Operation());

        Assert.Equal(new OperationWork.ResetEngine(EngineResetScope.Factory), reset.Work);
        Assert.IsType<OperationWork.Unknown>(unknown.Work);
    }

    [Fact]
    public void ToOperation_KeepsAFailuresRemediations()
    {
        var operation = DaemonOperations.ToOperation(new Wire.Operation {
            State = Wire.OperationState.Failed,
            Error = new Wire.OperationError {
                Code = (int)StatusCode.FailedPrecondition,
                Message = "WSL is not installed",
                Remediations = { new WireCommon.Remediation { ActionId = "install-wsl", RequiresElevation = true } },
            },
        });

        Assert.Equal(OperationState.Failed, operation.State);
        Assert.Equal("WSL is not installed", operation.Failure?.Error.Message);
        Assert.Equal(DaemonErrors.ConflictCode, operation.Failure?.Error.Code);
        Assert.Equal([new Remediation("install-wsl", true)], operation.Failure?.Remediations);
    }

    [Fact]
    public void ToError_MapsAStoppedEngineByItsReason()
    {
        var error = DaemonOperations.ToError(new Wire.OperationError {
            Code = (int)StatusCode.FailedPrecondition,
            Message = "the engine is stopped",
            Reason = WireCommon.ErrorReason.EngineNotRunning,
        });

        Assert.Equal(DaemonErrors.EngineNotRunningCode, error.Code);
    }

    [Fact]
    public void Ended_SaysWhyAnOperationDidNotSucceed()
    {
        var cancelled = new DaemonOperation("op", new OperationWork.CreateBundle(), OperationState.Cancelled, _started);

        Assert.Equal("It was cancelled.", DaemonOperations.Ended(cancelled).Message);
    }

    [Fact]
    public void Step_DescribesAnImportResult()
    {
        var imported = DaemonOperations.Step(new Wire.FollowOperationResponse {
            ImportImages = new Contr.Sdk.Kubernetes.V1.ImportImagesResponse { Image = "shop-api:dev" },
        });
        var refused = DaemonOperations.Step(new Wire.FollowOperationResponse {
            ImportImages = new Contr.Sdk.Kubernetes.V1.ImportImagesResponse { Image = "x:1", Error = "no such image" },
        });

        Assert.Equal("Imported shop-api:dev", imported?.Message);
        Assert.Equal("x:1: no such image", refused?.Message);
    }

    [Fact]
    public void Step_SkipsAnOperationsEnd()
    {
        Assert.Null(DaemonOperations.Step(new Wire.FollowOperationResponse { Finished = new Wire.Operation() }));
    }
}
