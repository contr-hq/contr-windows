using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>The <c>docker run</c> command that creates the same container as a <see cref="ContainerSpec"/>.</summary>
public static class DockerRunCommand
{
    public static string Format(ContainerSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        List<string> words = ["docker", "run", "-d"];

        if (spec.Name is { } name)
        {
            words.Add("--name");
            words.Add(name.Value);
        }

        foreach (var port in spec.Ports)
        {
            words.Add("-p");
            words.Add(Publish(port));
        }

        foreach (var mount in spec.Mounts)
        {
            words.Add(mount.Kind == MountSource.Tmpfs ? "--tmpfs" : "-v");
            words.Add(Mount(mount));
        }

        foreach (var variable in spec.Environment)
        {
            words.Add("-e");
            words.Add(variable);
        }

        if (spec.Network.Length > 0)
        {
            words.Add("--network");
            words.Add(spec.Network);
        }

        if (Restart(spec.RestartPolicy) is { } restart)
        {
            words.Add("--restart");
            words.Add(restart);
        }

        if (spec.AutoRemove)
        {
            words.Add("--rm");
        }

        words.Add(spec.Image.Length > 0 ? spec.Image : "IMAGE");
        words.AddRange(spec.Command);

        return string.Join(' ', words.Select(CommandLine.Quote));
    }

    private static string Publish(PortBinding port)
    {
        var protocol = port.Protocol switch {
            PortProtocol.Udp => "/udp",
            PortProtocol.Sctp => "/sctp",
            _ => string.Empty,
        };
        var target = Number(port.ContainerPort) + protocol;
        var host = port.HostPort > 0 ? Number(port.HostPort) : string.Empty;

        return (port.HostIp.Length > 0, host.Length > 0) switch {
            (true, _) => $"{port.HostIp}:{host}:{target}",
            (false, true) => $"{host}:{target}",
            _ => target,
        };
    }

    private static string Mount(MountRequest mount)
    {
        var path = mount.Kind == MountSource.Tmpfs || mount.Source.Length == 0
            ? mount.Destination
            : $"{mount.Source}:{mount.Destination}";

        return mount.ReadOnly && mount.Kind != MountSource.Tmpfs ? $"{path}:ro" : path;
    }

    private static string? Restart(RestartPolicy policy)
    {
        return policy.Mode switch {
            RestartMode.Always => "always",
            RestartMode.UnlessStopped => "unless-stopped",
            RestartMode.OnFailure when policy.MaxRetries > 0 => $"on-failure:{Number(policy.MaxRetries)}",
            RestartMode.OnFailure => "on-failure",
            _ => null,
        };
    }

    private static string Number(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
