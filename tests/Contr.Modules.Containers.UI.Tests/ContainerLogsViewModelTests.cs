using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Queries.StreamContainerLogs;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Platform;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ContainerLogsViewModelTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();

    [Fact]
    public void Open_JoinsALineSplitAcrossChunks()
    {
        Stream(
            new ContainerLogEvent.Output("start", false),
            new ContainerLogEvent.Output("ing\nready\n", false),
            new ContainerLogEvent.Output("oops\n", true));
        var viewModel = new ContainerLogsViewModel(_sender, _clipboard);

        viewModel.Open(new ContainerId("c0ffee01"), null);

        Assert.Equal(["starting", "ready", "oops"], viewModel.Lines.Select(line => line.Text));
        Assert.True(viewModel.Lines[2].IsError);
    }

    [Fact]
    public void Open_ShowsWhyTheStreamBroke()
    {
        Stream(new ContainerLogEvent.Failed(new Error("Daemon.Unavailable", "connection closed")));
        var viewModel = new ContainerLogsViewModel(_sender, _clipboard);

        viewModel.Open(new ContainerId("c0ffee01"), null);

        Assert.Equal("connection closed", viewModel.Error);
    }

    [Fact]
    public void Search_MarksAndCountsMatchingLines()
    {
        Stream(new ContainerLogEvent.Output("Error: one\nfine\nfatal: error two\n", false));
        var viewModel = new ContainerLogsViewModel(_sender, _clipboard);
        viewModel.Open(new ContainerId("c0ffee01"), null);

        viewModel.SearchText = "error";
        viewModel.NextMatch();

        Assert.Equal([true, false, true], viewModel.Lines.Select(line => line.IsMatch));
        Assert.Equal("error", viewModel.Lines[0].Highlight);
        Assert.Equal("1 of 2", viewModel.MatchText);
    }

    [Fact]
    public void Note_AddsAClientLineAfterTheOutput()
    {
        Stream(new ContainerLogEvent.Output("bye\n", false));
        var viewModel = new ContainerLogsViewModel(_sender, _clipboard);
        viewModel.Open(new ContainerId("c0ffee01"), null);

        viewModel.Note("container exited with code 1");

        Assert.True(viewModel.Lines[^1].IsNote);
        Assert.Equal("— container exited with code 1 —", viewModel.Lines[^1].Text);
    }

    private void Stream(params ContainerLogEvent[] events)
    {
        IAsyncEnumerable<ContainerLogEvent> stream = TestStreams.Of(events);
        _sender
            .Send(Arg.Any<StreamContainerLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stream));
    }
}
