using Contr.Modules.Containers.Domain;
using Xunit;

namespace Contr.Modules.Containers.Domain.Tests;

public sealed class PortBindingTests
{
    [Theory]
    [InlineData("8080", "80", 8080, 80, PortProtocol.Tcp, "")]
    [InlineData("", "80/tcp", 0, 80, PortProtocol.Tcp, "")]
    [InlineData("127.0.0.1:5353", "53/udp", 5353, 53, PortProtocol.Udp, "127.0.0.1")]
    [InlineData(" 9000 ", " 9000/SCTP ", 9000, 9000, PortProtocol.Sctp, "")]
    public void Parse_ReadsBothSides(
        string host, string container, int hostPort, int containerPort, PortProtocol protocol, string hostIp)
    {
        var parsed = PortBinding.Parse(host, container);

        Assert.True(parsed.IsSuccess);
        Assert.Equal(new PortBinding(containerPort, protocol, hostPort, hostIp), parsed.Value);
    }

    [Theory]
    [InlineData("8080", "")]
    [InlineData("8080", "80/quic")]
    [InlineData("70000", "80")]
    [InlineData("web", "80")]
    [InlineData("8080", "0")]
    public void Parse_RefusesWhatTheEngineWouldNot(string host, string container)
    {
        Assert.True(PortBinding.Parse(host, container).IsFailure);
    }
}
