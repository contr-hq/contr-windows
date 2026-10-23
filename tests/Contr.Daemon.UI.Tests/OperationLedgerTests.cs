using System;
using System.Linq;
using Contr.Daemon.UI;
using Contr.UI.Contracts.Services.Daemon;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using ImagesV1 = Contr.Sdk.Images.V1;
using Wire = Contr.Sdk.Operations.V1;

namespace Contr.Daemon.UI.Tests;

public sealed class OperationLedgerTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apply_ListsTheNewestFirst()
    {
        var ledger = new OperationLedger();

        ledger.Apply(Snapshot(Pull("old", Wire.OperationState.Succeeded, 0), Pull("new", Wire.OperationState.Running, 5)));

        Assert.Equal(["new", "old"], ledger.Operations.Select(operation => operation.Id));
    }

    [Fact]
    public void Apply_ReportsARunningOperationThatEnds()
    {
        var ledger = new OperationLedger();
        ledger.Apply(Snapshot(Pull("op", Wire.OperationState.Running, 0)));

        var ended = ledger.Apply(new Wire.WatchOperationsResponse {
            Upserted = Pull("op", Wire.OperationState.Succeeded, 0),
        });

        var operation = Assert.Single(ended);
        Assert.Equal(OperationState.Succeeded, operation.State);
    }

    [Fact]
    public void Apply_ReportsNothingForWorkThatEndedBeforeTheClientSawIt()
    {
        var ledger = new OperationLedger();

        var ended = ledger.Apply(Snapshot(Pull("op", Wire.OperationState.Failed, 0)));

        Assert.Empty(ended);
    }

    [Fact]
    public void Apply_ReportsAnEndThatASnapshotAfterAReconnectShows()
    {
        var ledger = new OperationLedger();
        ledger.Apply(Snapshot(Pull("op", Wire.OperationState.Running, 0)));

        var ended = ledger.Apply(Snapshot(Pull("op", Wire.OperationState.Cancelled, 0)));

        Assert.Equal(OperationState.Cancelled, Assert.Single(ended).State);
    }

    [Fact]
    public void Apply_ForgetsTheRunningWorkOfADaemonThatStopped()
    {
        var ledger = new OperationLedger();
        ledger.Apply(Snapshot(Pull("op", Wire.OperationState.Running, 0)));
        ledger.Apply(Snapshot());

        var ended = ledger.Apply(new Wire.WatchOperationsResponse {
            Upserted = Pull("op", Wire.OperationState.Succeeded, 0),
        });

        Assert.Empty(ended);
    }

    [Fact]
    public void Apply_DropsARemovedOperation()
    {
        var ledger = new OperationLedger();
        ledger.Apply(Snapshot(Pull("op", Wire.OperationState.Succeeded, 0)));

        ledger.Apply(new Wire.WatchOperationsResponse { RemovedId = "op" });

        Assert.Empty(ledger.Operations);
    }

    private static Wire.WatchOperationsResponse Snapshot(params Wire.Operation[] operations)
    {
        return new Wire.WatchOperationsResponse { Snapshot = new Wire.OperationSnapshot { Operations = { operations } } };
    }

    private static Wire.Operation Pull(string id, Wire.OperationState state, int minutes)
    {
        return new Wire.Operation {
            Id = id,
            Request = new Wire.OperationRequest { PullImage = new ImagesV1.PullImageRequest { Reference = "nginx" } },
            State = state,
            StartedAt = Timestamp.FromDateTimeOffset(_now.AddMinutes(minutes)),
        };
    }
}
