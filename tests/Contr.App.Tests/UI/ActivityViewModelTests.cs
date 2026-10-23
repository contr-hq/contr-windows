using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.UI;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class ActivityViewModelTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly IActivity _activity = Substitute.For<IActivity>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    [Fact]
    public void Show_PutsRunningWorkFirstAndCountsIt()
    {
        var viewModel = Create();

        viewModel.Show(
            [
                Operation("done", new OperationWork.PullImage("nginx", string.Empty), OperationState.Succeeded, 9),
                Operation("pull", new OperationWork.PullImage("postgres:17", string.Empty), OperationState.Running, 5),
            ],
            _now);

        Assert.Equal(["pull", "done"], viewModel.Rows.Select(row => row.Id));
        Assert.Equal(1, viewModel.RunningCount);
        Assert.Equal("1 running", viewModel.IndicatorText);
        Assert.True(viewModel.HasRows);
    }

    [Fact]
    public void Show_OffersCancelOnlyWhereTheDaemonAllowsIt()
    {
        var viewModel = Create();
        var reset = Operation("reset", new OperationWork.ResetEngine(EngineResetScope.Runtime), OperationState.Running, 1);
        var pull = Operation("pull", new OperationWork.PullImage("nginx", string.Empty), OperationState.Running, 2) with {
            IsCancellable = true,
            Progress = new EngineProgress("12 MB of 40 MB", 0.3),
        };

        viewModel.Show([pull, reset], _now);

        var pullRow = viewModel.Rows.Single(row => row.Id == "pull");
        Assert.True(pullRow.CanCancel);
        Assert.Equal("12 MB of 40 MB", pullRow.Detail);
        Assert.Equal(30, pullRow.Percent, 3);
        Assert.False(viewModel.Rows.Single(row => row.Id == "reset").CanCancel);
    }

    [Fact]
    public void Show_OffersAFailuresFixesAndSavingAFinishedBundle()
    {
        var viewModel = Create();
        var failed = Operation("start", new OperationWork.StartEngine(), OperationState.Failed, 1) with {
            Failure = new OperationFailure(new Error("Daemon.Failed", "WSL is not installed."), [
                new Remediation(Remediation.InstallWslId, true),
            ]),
        };
        var bundle = Operation("bundle", new OperationWork.CreateBundle(), OperationState.Succeeded, 2);

        viewModel.Show([failed, bundle], _now);

        var start = viewModel.Rows.Single(row => row.Id == "start");
        Assert.Equal("WSL is not installed.", start.Detail);
        Assert.Equal(["Install WSL"], start.Fixes.Select(fix => fix.Label));
        Assert.True(viewModel.Rows.Single(row => row.Id == "bundle").CanSave);
    }

    [Fact]
    public async Task Cancel_SaysWhenTheDaemonRefuses()
    {
        _activity
            .CancelAsync("reset", Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("Daemon.Conflict", "this operation cannot be cancelled")));
        var viewModel = Create();
        viewModel.Show([Operation("reset", new OperationWork.StopEngine(), OperationState.Running, 1)], _now);

        await viewModel.CancelAsync(viewModel.Rows.Single());

        _notifications.Received(1).Show(
            "this operation cannot be cancelled", InfoBarSeverity.Error, "Stop the engine wasn't cancelled");
    }

    [Theory]
    [InlineData(OperationState.Succeeded, "Pulled postgres:17")]
    [InlineData(OperationState.Failed, "Failed: Pull postgres:17")]
    [InlineData(OperationState.Cancelled, "Cancelled: Pull postgres:17")]
    public void Outcome_SaysHowAPullEnded(OperationState state, string expected)
    {
        var pull = Operation("pull", new OperationWork.PullImage("postgres:17", string.Empty), state, 1);

        Assert.Equal(expected, ActivityText.Outcome(pull));
    }

    [Fact]
    public void Title_NamesAComposeActionsTarget()
    {
        Assert.Equal(
            "Compose restart · api",
            ActivityText.Title(new OperationWork.ComposeAction("shop", "restart", ["api"], false)));
        Assert.Equal(
            "Compose up · shop",
            ActivityText.Title(new OperationWork.ComposeAction("shop", "up", [], false)));
    }

    [Fact]
    public void IsNews_LeavesAnEngineThatStartedAsAsked()
    {
        var started = Operation("start", new OperationWork.StartEngine(), OperationState.Succeeded, 1);
        var failed = started with { State = OperationState.Failed };
        var pulled = Operation("pull", new OperationWork.PullImage("nginx", string.Empty), OperationState.Succeeded, 1);

        Assert.False(ActivityText.IsNews(started));
        Assert.True(ActivityText.IsNews(failed));
        Assert.True(ActivityText.IsNews(pulled));
    }

    private ActivityViewModel Create()
    {
        return new ActivityViewModel(
            _activity, Substitute.For<IDiagnostics>(), Substitute.For<IDaemonConnection>(), _notifications);
    }

    private static DaemonOperation Operation(string id, OperationWork work, OperationState state, int minutesAgo)
    {
        var started = _now.AddMinutes(-minutesAgo);

        return new DaemonOperation(id, work, state, started) {
            FinishedAt = state == OperationState.Running ? null : started.AddSeconds(30),
        };
    }
}
