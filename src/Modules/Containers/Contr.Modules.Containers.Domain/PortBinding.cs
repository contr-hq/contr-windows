using System;
using System.Globalization;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Domain;

/// <summary>The transport a published port carries.</summary>
public enum PortProtocol
{
    Tcp,
    Udp,
    Sctp,
}

/// <summary>A container port published on the host, as <c>docker run -p</c> takes it.</summary>
/// <param name="HostPort">Zero lets the engine choose a free port.</param>
/// <param name="HostIp">Empty publishes on every host address.</param>
public sealed record PortBinding(int ContainerPort, PortProtocol Protocol, int HostPort, string HostIp)
{
    private const int _maxPort = 65535;

    /// <summary>Reads a host side such as <c>8080</c>, <c>127.0.0.1:8080</c> or nothing, and a container
    /// side such as <c>80</c> or <c>53/udp</c>.</summary>
    public static Result<PortBinding> Parse(string? host, string? container)
    {
        var target = container?.Trim() ?? string.Empty;
        var slash = target.IndexOf('/', StringComparison.Ordinal);
        var portText = slash >= 0 ? target[..slash] : target;
        var protocolText = slash >= 0 ? target[(slash + 1)..] : "tcp";

        if (ParsePort(portText) is not { } containerPort || ParseProtocol(protocolText) is not { } protocol)
        {
            return Result.Failure<PortBinding>(ContainersErrors.PortInvalid(target));
        }

        var source = host?.Trim() ?? string.Empty;
        var colon = source.LastIndexOf(':');
        var hostIp = colon >= 0 ? source[..colon].Trim('[', ']') : string.Empty;
        var hostPortText = colon >= 0 ? source[(colon + 1)..] : source;

        if (hostPortText.Length == 0)
        {
            return new PortBinding(containerPort, protocol, 0, hostIp);
        }

        return ParsePort(hostPortText) is { } hostPort
            ? new PortBinding(containerPort, protocol, hostPort, hostIp)
            : Result.Failure<PortBinding>(ContainersErrors.PortInvalid(source));
    }

    private static int? ParsePort(string text)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && port is > 0 and <= _maxPort
                ? port
                : null;
    }

    private static PortProtocol? ParseProtocol(string text)
    {
        return text.ToUpperInvariant() switch {
            "TCP" => PortProtocol.Tcp,
            "UDP" => PortProtocol.Udp,
            "SCTP" => PortProtocol.Sctp,
            _ => null,
        };
    }
}
