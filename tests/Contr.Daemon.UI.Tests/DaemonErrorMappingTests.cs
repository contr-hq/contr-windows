using Contr.Application.Abstractions.Daemon;
using Contr.Daemon.UI;
using Contr.Sdk;
using Grpc.Core;
using Xunit;

namespace Contr.Daemon.UI.Tests;

public sealed class DaemonErrorMappingTests
{
    [Fact]
    public void EngineNotRunning_MapsToItsOwnError()
    {
        Assert.Equal(DaemonErrors.EngineNotRunning, new EngineNotRunningException("stopped", null).ToError());
    }

    [Fact]
    public void NotFound_KeepsTheDaemonMessage()
    {
        var error = new NotFoundException("No such container: web", null).ToError();

        Assert.Equal(DaemonErrors.NotFoundCode, error.Code);
        Assert.Equal("No such container: web", error.Message);
    }

    [Fact]
    public void AnUnavailableStatus_MeansTheDaemonIsGone()
    {
        var error = new RpcException(new Status(StatusCode.Unavailable, "pipe closed")).ToError();

        Assert.Equal(DaemonErrors.Unavailable, error);
    }

    [Fact]
    public void ACancelledCall_MeansTheConnectionWasDropped()
    {
        var error = new RpcException(new Status(StatusCode.Cancelled, "Call canceled by the client.")).ToError();

        Assert.Equal(DaemonErrors.Unavailable, error);
    }

    [Fact]
    public void AnythingElse_IsAFailureWithTheDaemonMessage()
    {
        var error = new RpcException(new Status(StatusCode.Internal, "engine said no")).ToError();

        Assert.Equal(DaemonErrors.FailedCode, error.Code);
        Assert.Equal("engine said no", error.Message);
    }
}
