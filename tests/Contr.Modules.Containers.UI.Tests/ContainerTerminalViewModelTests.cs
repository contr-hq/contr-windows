using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Queries.OpenTerminal;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.SharedKernel;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ContainerTerminalViewModelTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ITerminalSession _session = Substitute.For<ITerminalSession>();

    [Fact]
    public async Task Connect_StartsTheChosenShellAtTheViewSize()
    {
        Open(TestStreams.Of<TerminalOutput>());
        var viewModel = new ContainerTerminalViewModel(_sender) { ShellIndex = 1, UserIndex = 1 };
        await viewModel.ResizeAsync(120, 32);

        await viewModel.ConnectAsync(new ContainerId("c0ffee01"));

        await _sender.Received(1).Send(
            Arg.Is<OpenTerminalQuery>(query => query != null
                && query.Shell == "/bin/bash"
                && query.User == "root"
                && query.Columns == 120
                && query.Rows == 32),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Connect_ShowsOutputThenTheExitCode()
    {
        var output = new List<string>();
        Open(TestStreams.Of<TerminalOutput>(
            new TerminalOutput.Data(Encoding.UTF8.GetBytes("/app # ")),
            new TerminalOutput.Exited(0)));
        var viewModel = new ContainerTerminalViewModel(_sender);
        viewModel.OutputReceived += (_, bytes) => output.Add(Encoding.UTF8.GetString(bytes));

        await viewModel.ConnectAsync(new ContainerId("c0ffee01"));

        Assert.Equal(["/app # "], output);
        Assert.Equal("Exited with code 0", viewModel.StatusText);
        Assert.False(viewModel.IsConnected);
    }

    [Fact]
    public async Task Connect_ShowsWhyTheShellDidNotStart()
    {
        _sender
            .Send(Arg.Any<OpenTerminalQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ITerminalSession>(new Error("Daemon.NotFound", "no such file: /bin/bash")));
        var viewModel = new ContainerTerminalViewModel(_sender);

        await viewModel.ConnectAsync(new ContainerId("c0ffee01"));

        Assert.Equal("no such file: /bin/bash", viewModel.StatusText);
        Assert.False(viewModel.IsConnected);
    }

    private void Open(IAsyncEnumerable<TerminalOutput> output)
    {
        _session.ReadAsync(Arg.Any<CancellationToken>()).Returns(output);
        _sender
            .Send(Arg.Any<OpenTerminalQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(_session));
    }
}
