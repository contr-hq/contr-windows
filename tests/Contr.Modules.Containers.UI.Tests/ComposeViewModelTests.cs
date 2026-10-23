using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Compose;
using Contr.Modules.Containers.Application.Compose.Commands.AddComposeProject;
using Contr.Modules.Containers.Application.Compose.Commands.RemoveComposeProject;
using Contr.Modules.Containers.Application.Compose.Commands.RunComposeAction;
using Contr.Modules.Containers.Application.Compose.Queries.FollowComposeAction;
using Contr.Modules.Containers.Application.Compose.Queries.StreamComposeLogs;
using Contr.Modules.Containers.Application.Compose.Queries.WatchComposeProjects;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.SharedKernel;
using Contr.UI.Common.Controls;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ComposeViewModelTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IActivity _activity = Substitute.For<IActivity>();
    private readonly BehaviorSubject<IReadOnlyList<DaemonOperation>> _operations = new([]);

    public ComposeViewModelTests()
    {
        _activity.Changes.Returns(_operations);
        _activity.Current.Returns(_ => _operations.Value);
        var running = new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty);
        _daemon.Status.Returns(running);
        _daemon.StatusChanges.Returns(Observable.Return(running));
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 15, "Fake") {
            Capabilities = new HashSet<DaemonCapability> { DaemonCapability.Compose },
        });
        Logs();
    }

    [Fact]
    public void Activate_ListsProjectsWithTheirStateAndSelectsTheFirst()
    {
        var viewModel = Watching(Shop(), Project("docs", Service("web", Container("docs-web-1"))), Added("payments"));

        viewModel.Activate();

        Assert.Equal(["docs", "payments", "shop"], viewModel.Projects.Select(project => project.Name));
        var shop = viewModel.Projects.Single(project => project.Name == "shop");
        Assert.Equal("1 of 2 running", shop.StatusText);
        Assert.Equal(PillKind.Caution, shop.PillKind);
        var payments = viewModel.Projects.Single(project => project.Name == "payments");
        Assert.Equal("Stopped", payments.StatusText);
        Assert.True(payments.IsAdded);
        Assert.Equal(["3 projects", "2 running", "1 stopped"], viewModel.SummaryPills.Select(pill => pill.Text));
        Assert.Equal("docs", viewModel.Selected?.Name);
        Assert.Equal(["All", "web"], viewModel.Filters);
    }

    [Fact]
    public void Select_ShowsTheProjectsServicesAsTheBoardDoes()
    {
        var viewModel = Watching(Shop());
        viewModel.Activate();

        viewModel.Select(viewModel.Projects.Single());

        Assert.Equal(@"C:\src\shop\compose.yaml", viewModel.ConfigFiles);
        Assert.Equal(@"C:\src\shop", viewModel.WorkingDir);
        var api = viewModel.Services[0];
        Assert.Equal(
            ("api", "node:22-alpine", "Running · healthy", "localhost:8080"),
            (api.Name, api.Image, api.StatusText, api.Ports.Single().Address));
        var worker = viewModel.Services[1];
        Assert.Equal(
            ("built from ./worker", "Exited (1)", PillKind.Critical),
            (worker.BuildText, worker.StatusText, worker.PillKind));
        Assert.Equal(["All", "api", "worker"], viewModel.Filters);
        Assert.True(viewModel.CanUp);
    }

    [Fact]
    public void AProjectWithoutComposeFiles_CannotGoUp()
    {
        var legacy = Project("legacy", Service("web", Container("legacy-web-1"))) with { ConfigFiles = [] };
        var viewModel = Watching(legacy);

        viewModel.Activate();

        Assert.True(viewModel.CanAct);
        Assert.False(viewModel.CanUp);
    }

    [Fact]
    public async Task Up_ShowsComposesOutputThenReportsSuccess()
    {
        Runs(new ComposeActionEvent.Output(" Container shop-api-1  Started"), new ComposeActionEvent.Exited(0));
        var viewModel = Watching(Shop());
        viewModel.Activate();

        await viewModel.UpAsync();

        await _sender.Received(1).Send(
            Arg.Is<RunComposeActionCommand>(command => command != null
                && command.Project == "shop"
                && command.Action == ComposeAction.Up
                && command.Services.Count == 0),
            Arg.Any<CancellationToken>());
        Assert.Equal([" Container shop-api-1  Started"], viewModel.Output.Select(line => line.Text));
        Assert.Equal(
            ("Up · shop", "Done", PillKind.Good),
            (viewModel.Run?.Title, viewModel.Run?.StatusText, viewModel.Run?.PillKind));
        Assert.True(viewModel.ShowsRun);
        Assert.True(viewModel.CanAct);
        _notifications.Received(1).Show("shop is up.", InfoBarSeverity.Success, Arg.Any<string?>());
    }

    [Fact]
    public async Task ACommandComposeFails_KeepsItsOutputAndReportsTheExitCode()
    {
        Runs(new ComposeActionEvent.Output("no such image"), new ComposeActionEvent.Exited(1));
        var viewModel = Watching(Shop());
        viewModel.Activate();

        await viewModel.PullAsync();

        Assert.Equal(("Exited (1)", PillKind.Critical), (viewModel.Run?.StatusText, viewModel.Run?.PillKind));
        Assert.Equal(["no such image"], viewModel.Output.Select(line => line.Text));
        _notifications.Received(1).Show(
            "Compose exited with code 1. Its output is under the project's commands.",
            InfoBarSeverity.Error,
            "Compose pull failed for shop");
    }

    [Fact]
    public async Task Down_CanAlsoDeleteTheVolumes()
    {
        Runs(new ComposeActionEvent.Exited(0));
        var viewModel = Watching(Shop());
        viewModel.Activate();

        await viewModel.DownAsync(removeVolumes: true);

        await _sender.Received(1).Send(
            Arg.Is<RunComposeActionCommand>(command =>
                command != null && command.Action == ComposeAction.Down && command.RemoveVolumes),
            Arg.Any<CancellationToken>());
        _notifications.Received(1).Show(
            "Took down shop and deleted its volumes.", InfoBarSeverity.Success, Arg.Any<string?>());
    }

    [Fact]
    public async Task RestartService_RestartsThatServiceAlone()
    {
        Runs(new ComposeActionEvent.Exited(0));
        var viewModel = Watching(Shop());
        viewModel.Activate();

        await viewModel.RestartServiceAsync(viewModel.Services[0]);

        await _sender.Received(1).Send(
            Arg.Is<RunComposeActionCommand>(command => command != null
                && command.Action == ComposeAction.Restart
                && command.Services.SequenceEqual(new[] { "api" })),
            Arg.Any<CancellationToken>());
        Assert.Equal("Restart · api", viewModel.Run?.Title);
        _notifications.Received(1).Show("Restarted api.", InfoBarSeverity.Success, Arg.Any<string?>());
    }

    [Fact]
    public async Task AddProject_ShowsWhyComposeCouldNotReadTheFile()
    {
        _sender
            .Send(Arg.Any<AddComposeProjectCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ComposeProjectDto>(new Error("Daemon.Failed", "yaml: line 3: did not find key")));
        var viewModel = Watching(Shop());
        viewModel.Activate();

        await viewModel.AddProjectAsync(@"C:\src\broken\compose.yaml");

        _notifications.Received(1).Show(
            "yaml: line 3: did not find key", InfoBarSeverity.Error, "Couldn't add the project");
    }

    [Fact]
    public async Task RemoveProject_ForgetsOnlyAProjectTheUserAdded()
    {
        _sender
            .Send(Arg.Any<RemoveComposeProjectCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var viewModel = Watching(Shop(), Added("payments"));
        viewModel.Activate();

        await viewModel.RemoveProjectAsync(viewModel.Projects.Single(project => project.Name == "shop"));
        await viewModel.RemoveProjectAsync(viewModel.Projects.Single(project => project.Name == "payments"));

        await _sender.Received(1).Send(Arg.Any<RemoveComposeProjectCommand>(), Arg.Any<CancellationToken>());
        _notifications.Received(1).Show("Removed payments.", InfoBarSeverity.Success, Arg.Any<string?>());
    }

    [Fact]
    public void Logs_ArriveAsLinesLabelledWithTheirService()
    {
        var api = new ContainerId("shop-api-1-id");
        Logs(
            new ComposeLogEvent.Output("api", api, "Listening on :3000\nGET /hea", IsError: false),
            new ComposeLogEvent.Output("worker", new ContainerId("shop-worker-1-id"), "exited\n", IsError: true),
            new ComposeLogEvent.Output("api", api, "lth 200\n", IsError: false));
        var viewModel = Watching(Shop());

        viewModel.Activate();

        Assert.Equal(
            [("api", 0, "Listening on :3000"), ("worker", 1, "exited"), ("api", 0, "GET /health 200")],
            viewModel.Lines.Select(line => (line.Service, line.Color, line.Text)));

        viewModel.FilterIndex = 2;

        Assert.Equal(["exited"], viewModel.Lines.Select(line => line.Text));
    }

    [Fact]
    public void Activate_ShowsAnActionAClosedPageStarted()
    {
        _operations.OnNext([Action("op-3", "shop", "up", OperationState.Running)]);
        Follows(
            "op-3", new ComposeActionEvent.Output("Container shop-api-1  Started"), new ComposeActionEvent.Exited(0));
        var viewModel = Watching(Shop());

        viewModel.Activate();

        Assert.Equal("Up · shop", viewModel.Run?.Title);
        Assert.Equal("Done", viewModel.Run?.StatusText);
        Assert.Equal(["Container shop-api-1  Started"], viewModel.Output.Select(line => line.Text));
        _notifications.Received(1).Show("shop is up.", InfoBarSeverity.Success, null);
    }

    [Fact]
    public void Activate_ReplaysAnActionThatEndedWithoutTellingAgain()
    {
        _operations.OnNext([Action("op-3", "shop", "pull", OperationState.Succeeded)]);
        Follows("op-3", new ComposeActionEvent.Output("api Pulled"), new ComposeActionEvent.Exited(0));
        var viewModel = Watching(Shop());

        viewModel.Activate();

        Assert.Equal("Pull · shop", viewModel.Run?.Title);
        Assert.Equal(["api Pulled"], viewModel.Output.Select(line => line.Text));
        _notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<InfoBarSeverity>(), Arg.Any<string?>());
    }

    [Fact]
    public void Activate_KeepsABusyProjectsCommandsOff()
    {
        var running = new TaskCompletionSource();
        _operations.OnNext([Action("op-3", "shop", "up", OperationState.Running)]);
        _sender
            .Send(Arg.Any<FollowComposeActionQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Until<ComposeActionEvent>(running.Task)));
        var viewModel = Watching(Shop());

        viewModel.Activate();

        Assert.True(viewModel.IsActing);
        Assert.False(viewModel.CanAct);
        running.SetResult();
    }

    [Fact]
    public async Task WithoutTheCapability_TheProjectsAreNotWatched()
    {
        _daemon.Identity.Returns(new DaemonIdentity("npipe:////./pipe/x", "0.1.0", 14, "Fake"));
        var viewModel = Watching(Shop());

        viewModel.Activate();

        Assert.True(viewModel.LacksOffer);
        await _sender.DidNotReceive().Send(Arg.Any<WatchComposeProjectsQuery>(), Arg.Any<CancellationToken>());
    }

    private ComposeViewModel Watching(params ComposeProjectDto[] projects)
    {
        IAsyncEnumerable<ListChange<ComposeProjectDto>> stream = TestStreams.Of<ListChange<ComposeProjectDto>>(
            new ListChange<ComposeProjectDto>.Snapshot(projects));
        _sender
            .Send(Arg.Any<WatchComposeProjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stream));

        return new ComposeViewModel(_sender, _notifications, _daemon, Substitute.For<IDaemonSettings>(), _activity);
    }

    private static DaemonOperation Action(string id, string project, string action, OperationState state)
    {
        return new DaemonOperation(
            id, new OperationWork.ComposeAction(project, action, [], false), state, DateTimeOffset.UnixEpoch);
    }

    private void Follows(string operationId, params ComposeActionEvent[] events)
    {
        _sender
            .Send(
                Arg.Is<FollowComposeActionQuery>(query => query != null && query.OperationId == operationId),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(TestStreams.Of(events)));
    }

    private static async IAsyncEnumerable<T> Until<T>(Task done)
    {
        await done;

        yield break;
    }

    private void Runs(params ComposeActionEvent[] events)
    {
        _sender
            .Send(Arg.Any<RunComposeActionCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(TestStreams.Of(events)));
    }

    private void Logs(params ComposeLogEvent[] events)
    {
        _sender
            .Send(Arg.Any<StreamComposeLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(TestStreams.Of(events)));
    }

    private static ComposeProjectDto Shop()
    {
        var api = Service("api", Container("shop-api-1", status: "Up 2 hours (healthy)", hostPort: 8080)) with {
            Image = "node:22-alpine",
        };
        var exited = Container("shop-worker-1", ContainerState.Exited, "Exited (1) 1 minute ago");
        var worker = Service("worker", exited) with { Image = "shop-worker", BuildContext = @"C:\src\shop\worker" };

        return Project("shop", api, worker);
    }

    private static ComposeProjectDto Project(string name, params ComposeServiceDto[] services)
    {
        return new ComposeProjectDto(
            name, $@"C:\src\{name}", [$@"C:\src\{name}\compose.yaml"], false, services, string.Empty);
    }

    private static ComposeProjectDto Added(string name)
    {
        return Project(name) with { IsAdded = true };
    }

    private static ComposeServiceDto Service(string name, params ContainerDto[] containers)
    {
        return new ComposeServiceDto(name, "nginx:1.29", string.Empty, containers);
    }

    private static ContainerDto Container(
        string name, ContainerState state = ContainerState.Running, string status = "Up 2 hours", int hostPort = 0)
    {
        PortDto[] ports = hostPort > 0 ? [new PortDto(3000, hostPort, "0.0.0.0", "tcp")] : [];

        return new ContainerDto(
            new ContainerId(name + "-id"),
            name,
            "nginx:1.29",
            state,
            status,
            DateTimeOffset.UnixEpoch,
            ports,
            string.Empty,
            string.Empty);
    }
}
