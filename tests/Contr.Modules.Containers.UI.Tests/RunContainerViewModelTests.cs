using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Commands.CreateContainer;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class RunContainerViewModelTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly RecordingNavigation _navigation = new();

    [Fact]
    public void Preview_ReadsAsTheDockerRunCommand()
    {
        var viewModel = CreateViewModel();

        viewModel.Image = "nginx:1.27-alpine";
        viewModel.Ports[0].Host = "8080";
        viewModel.Ports[0].Container = "80/tcp";
        viewModel.Mounts[0].Source = @"C:\src\site";
        viewModel.Mounts[0].Destination = "/usr/share/nginx/html";
        viewModel.Mounts[0].ReadOnly = true;

        Assert.Equal(
            @"docker run -d -p 8080:80 -v C:\src\site:/usr/share/nginx/html:ro nginx:1.27-alpine", viewModel.Preview);
        Assert.True(viewModel.HasFolder);
    }

    [Fact]
    public async Task Run_KeepsTheDialogOpenOnAPortTheEngineWouldRefuse()
    {
        var viewModel = CreateViewModel();
        viewModel.Image = "nginx";
        viewModel.Ports[0].Host = "80800";
        viewModel.Ports[0].Container = "80";

        var ran = await viewModel.RunAsync();

        Assert.False(ran);
        Assert.Equal(ContainersErrors.PortInvalid("80800").Message, viewModel.Error);
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<CreateContainerCommand>(), default);
    }

    [Fact]
    public async Task Run_ShowsTheEnginesRefusal()
    {
        _sender
            .Send(Arg.Any<CreateContainerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CreatedContainer>(new Error("Daemon.NotFound", "No such image: nginx:nope")));
        var viewModel = CreateViewModel();
        viewModel.Image = "nginx:nope";

        Assert.False(await viewModel.RunAsync());
        Assert.Equal("No such image: nginx:nope", viewModel.Error);
    }

    [Fact]
    public async Task Run_OpensTheNewContainersLogs()
    {
        var id = new ContainerId("c0ffee01");
        _sender
            .Send(Arg.Any<CreateContainerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CreatedContainer(id, [])));
        var viewModel = CreateViewModel();
        viewModel.Image = "nginx";
        viewModel.Name = "web";
        viewModel.Command = "nginx -g 'daemon off;'";

        Assert.True(await viewModel.RunAsync());

        await _sender.Received(1).Send(
            Arg.Is<CreateContainerCommand>(command => command != null
                && command.Spec.Name!.Value == "web"
                && command.Spec.Command.Count == 3
                && command.Spec.Command[2] == "daemon off;"),
            Arg.Any<CancellationToken>());
        var (pageKey, arguments) = Assert.Single(_navigation.Calls);
        Assert.Equal("ContainerDetail", pageKey);
        Assert.Equal(new object[] { id, ContainerDetailTab.Logs }, arguments);
    }

    private RunContainerViewModel CreateViewModel()
    {
        return new RunContainerViewModel(
            _sender, _navigation, Substitute.For<INotificationService>(), Substitute.For<IClipboardService>());
    }

    /// <summary>Records navigation; a substitute cannot match the span its arguments arrive in.</summary>
    private sealed class RecordingNavigation : INavigationService
    {
        public List<(string PageKey, object[] Arguments)> Calls { get; } = [];

        public IObservable<NavigationEvent> OnNavigated => Observable.Empty<NavigationEvent>();

        public bool CanGoBack => false;

        public void Initialize(Frame frame)
        {
        }

        public void Register<TPage>()
            where TPage : Page
        {
        }

        public void Register(params ReadOnlySpan<Type> pageTypes)
        {
        }

        public string GetPageKey(Type pageType)
        {
            return pageType.Name;
        }

        public bool NavigateTo(string pageKey, params ReadOnlySpan<object> args)
        {
            Calls.Add((pageKey, args.ToArray()));

            return true;
        }

        public void GoBack()
        {
        }

        public void ClearBackStack()
        {
        }
    }
}
