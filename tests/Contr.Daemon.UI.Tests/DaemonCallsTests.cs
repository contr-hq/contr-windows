using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Daemon;
using Contr.Daemon.UI;
using Contr.Sdk;
using Contr.SharedKernel;
using Grpc.Core;
using NSubstitute;
using Xunit;

namespace Contr.Daemon.UI.Tests;

public sealed class DaemonCallsTests
{
    private readonly IDaemonClient _daemon = Substitute.For<IDaemonClient>();

    [Fact]
    public async Task Call_ReportsAnUnreachableDaemon()
    {
        _daemon
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask<ContrClient>>(_ => throw new DaemonUnavailableException("no pipe", null));

        var result = await _daemon.CallAsync((_, _) => Task.FromResult(1), CancellationToken.None);

        Assert.Equal(DaemonErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task Run_ReportsAFailedCall()
    {
        _daemon.GetAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<ContrClient>(null!));

        var result = await _daemon.RunAsync(
            (_, _) => throw new RpcException(new Status(StatusCode.NotFound, "No such volume: data")),
            CancellationToken.None);

        Assert.Equal(DaemonErrors.NotFoundCode, result.Error.Code);
        Assert.Equal("No such volume: data", result.Error.Message);
    }

    [Fact]
    public async Task Stream_EndsWithOneFailedItemWhenTheDaemonIsUnreachable()
    {
        _daemon
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask<ContrClient>>(_ => throw new DaemonUnavailableException("no pipe", null));
        var items = new List<Error>();

        await foreach (var item in _daemon.StreamAsync<string, Error>(
            (_, _) => throw new RpcException(Status.DefaultSuccess),
            response => null,
            error => error,
            CancellationToken.None))
        {
            items.Add(item);
        }

        Assert.Equal([DaemonErrors.Unavailable], items);
    }
}
