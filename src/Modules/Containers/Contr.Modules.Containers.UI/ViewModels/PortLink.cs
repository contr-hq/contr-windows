using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Contr.Modules.Containers.Application.Containers;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>A published port as the list shows it: the host address and, if different, the container's port.</summary>
/// <param name="Address">The host address and port, such as <c>localhost:8080</c>.</param>
/// <param name="Target">Such as <c>→ 3000</c>; empty when the container listens on the published port number.</param>
/// <param name="Uri">An http address for a TCP port; absent for other protocols.</param>
public sealed record PortLink(string Address, string Target, Uri? Uri)
{
    public bool HasTarget => Target.Length > 0;

    public bool IsLink => Uri is not null;

    public bool IsText => Uri is null;

    /// <summary>The published ports, each once; the engine lists a binding once per address family.</summary>
    public static IReadOnlyList<PortLink> From(IReadOnlyList<PortDto> ports)
    {
        ArgumentNullException.ThrowIfNull(ports);

        return [.. ports
            .Where(port => port.HostPort > 0)
            .OrderBy(port => port.HostPort)
            .Select(ToLink)
            .DistinctBy(link => (link.Address, link.Target))];
    }

    private static PortLink ToLink(PortDto port)
    {
        var host = port.HostIp is "" or "0.0.0.0" or "::" ? "localhost" : port.HostIp;
        var hostPort = port.HostPort.ToString(CultureInfo.InvariantCulture);
        var address = host.Contains(':', StringComparison.Ordinal) ? $"[{host}]:{hostPort}" : $"{host}:{hostPort}";
        var target = port.ContainerPort == port.HostPort
            ? string.Empty
            : $"→ {port.ContainerPort.ToString(CultureInfo.InvariantCulture)}";
        var protocolSuffix = port.Protocol == "tcp" ? string.Empty : $"/{port.Protocol}";
        var uri = port.Protocol == "tcp" ? new Uri($"http://{address}") : null;

        return new PortLink(address + protocolSuffix, target, uri);
    }
}
