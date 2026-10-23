using Contr.App.Services.Platform;
using Contr.Interoperability;
using Xunit;

namespace Contr.App.Tests.Platform;

public sealed class CloseRequestsTests
{
    private readonly CloseRequests _requests = new();

    private static uint QueryEndSession => WindowInterop.WindowMessage.QueryEndSession;

    private static uint EndSession => WindowInterop.WindowMessage.EndSession;

    [Fact]
    public void OnClose_HidesAWindowTheUserClosed()
    {
        Assert.Equal(CloseAction.Hide, _requests.OnClose(isVisible: true));
    }

    [Fact]
    public void OnClose_QuitsForAHiddenWindow()
    {
        Assert.Equal(CloseAction.Quit, _requests.OnClose(isVisible: false));
    }

    [Fact]
    public void EndsApp_WhenTheSessionEnds()
    {
        Assert.False(_requests.EndsApp(QueryEndSession, 0));
        Assert.True(_requests.EndsApp(EndSession, 1));
    }

    [Fact]
    public void OnClose_QuitsOnceRestartManagerAskedToEndTheSession()
    {
        _requests.EndsApp(QueryEndSession, 0);

        Assert.Equal(CloseAction.Quit, _requests.OnClose(isVisible: true));
    }

    [Fact]
    public void OnClose_HidesAgainWhenTheSessionGoesOn()
    {
        _requests.EndsApp(QueryEndSession, 0);

        Assert.False(_requests.EndsApp(EndSession, 0));
        Assert.Equal(CloseAction.Hide, _requests.OnClose(isVisible: true));
    }

    [Fact]
    public void EndsApp_IgnoresOtherMessages()
    {
        Assert.False(_requests.EndsApp(WindowInterop.WindowMessage.Activate, 1));
        Assert.Equal(CloseAction.Hide, _requests.OnClose(isVisible: true));
    }
}
