using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.UI.Common.Formatting;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>How a fact's value reads.</summary>
public enum FactKind
{
    /// <summary>A plain value, such as a policy or a count.</summary>
    Text,

    /// <summary>An identifier, a command or a path, in a monospaced font.</summary>
    Code,

    /// <summary>The absence of a value, such as "No limit".</summary>
    Muted,
}

/// <summary>One labelled value of the inspect tab.</summary>
/// <param name="Note">A relative time after the value, such as "2h ago"; may be empty.</param>
public sealed record InspectFact(string Label, string Value, FactKind Kind = FactKind.Text, string Note = "")
{
    public bool HasNote => Note.Length > 0;
}

/// <summary>The inspect tab's summary, read from the engine's inspection document (Docker Engine API).</summary>
public sealed record InspectFacts(
    IReadOnlyList<InspectFact> Configuration,
    IReadOnlyList<InspectFact> State,
    IReadOnlyList<InspectFact> Limits)
{
    private const string _none = "—";

    /// <summary>Each mount as its kind and <c>source → destination</c>, paths as the engine sees them.</summary>
    public IReadOnlyList<InspectFact> Mounts { get; init; } = [];

    /// <summary>Each network the container is attached to, then its address there.</summary>
    public IReadOnlyList<InspectFact> Networks { get; init; } = [];

    /// <summary>The <c>VAR=value</c> entries the container runs with.</summary>
    public IReadOnlyList<string> Environment { get; init; } = [];

    /// <summary>The Compose project and service that run the container, then its restart policy.</summary>
    public IReadOnlyList<InspectFact> Setup { get; init; } = [];

    public static InspectFacts From(ContainerDetailsDto details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        using var document = Parse(details.RawJson);
        var root = document?.RootElement ?? default;
        var config = Child(root, "Config");
        var host = Child(root, "HostConfig");

        var command = string.Join(
            ' ',
            new[] { Text(root, "Path") }
                .Concat(Strings(root, "Args"))
                .Where(part => part.Length > 0));

        var memory = Number(host, "Memory");
        var restart = new InspectFact("Restart policy", RestartPolicy(Child(host, "RestartPolicy")));
        IReadOnlyList<InspectFact> configuration = [
            Code("Image", details.Container.Image),
            Code("Command", command),
            Code("Working folder", details.WorkingDirectory),
            Code("User", Text(config, "User"), "Image default"),
            restart,
            Code("Hostname", Text(config, "Hostname")),
        ];

        IReadOnlyList<InspectFact> state = [
            Moment("Started", details.StartedAt, now),
            .. details.Container.State == ContainerState.Running || details.FinishedAt is null
                ? Array.Empty<InspectFact>()
                : [Moment("Finished", details.FinishedAt, now)],
            new("Restarts", details.RestartCount.ToString(CultureInfo.CurrentCulture)),
            new("Health check", Health(details.Health), HealthKind(details.Health)),
        ];

        IReadOnlyList<InspectFact> limits = [
            CpuLimit(host),
            memory > 0 ? new("Memory", HumanFormat.Bytes(memory)) : new("Memory", "No limit", FactKind.Muted),
        ];

        return new InspectFacts(configuration, state, limits) {
            Mounts = [.. details.Mounts.Select(Mount)],
            Networks = ReadNetworks(Child(Child(root, "NetworkSettings"), "Networks")),
            Environment = details.Environment,
            Setup = [.. ReadCompose(Child(config, "Labels")), restart with { Label = "Restart" }],
        };
    }

    private static InspectFact Code(string label, string value, string fallback = _none)
    {
        return value.Length > 0 ? new(label, value, FactKind.Code) : new(label, fallback, FactKind.Muted);
    }

    /// <summary>The date and time, then how long ago while that is under a week.</summary>
    private static InspectFact Moment(string label, DateTimeOffset? moment, DateTimeOffset now)
    {
        if (moment is not { } at)
        {
            return new(label, "Never", FactKind.Muted);
        }

        var note = now - at < TimeSpan.FromDays(7) ? HumanFormat.Ago(at, now) : string.Empty;

        return new(label, When(at, now), Note: note);
    }

    private static InspectFact Mount(MountDto mount)
    {
        var kind = mount.Kind.Length > 0 ? char.ToUpperInvariant(mount.Kind[0]) + mount.Kind[1..] : _none;
        var source = mount.Kind == "volume" && mount.Name.Length > 0 ? mount.Name : mount.Source;

        return new InspectFact(kind, $"{source} → {mount.Destination}", FactKind.Code);
    }

    private static List<InspectFact> ReadNetworks(JsonElement networks)
    {
        return networks.ValueKind == JsonValueKind.Object
            ? [.. networks
                .EnumerateObject()
                .SelectMany(network => new[] {
                    Code("Network", network.Name),
                    Code("Address", Text(network.Value, "IPAddress"), "No address"),
                })]
            : [];
    }

    private static IEnumerable<InspectFact> ReadCompose(JsonElement labels)
    {
        var project = Text(labels, "com.docker.compose.project");
        var service = Text(labels, "com.docker.compose.service");

        if (project.Length == 0)
        {
            return [];
        }

        return service.Length > 0 ? [Code("Project", project), Code("Service", service)] : [Code("Project", project)];
    }

    private static JsonDocument? Parse(string json)
    {
        try
        {
            return json.Length > 0 ? JsonDocument.Parse(json) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement Child(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var child)
            ? child
            : default;
    }

    private static string Text(JsonElement element, string name)
    {
        var child = Child(element, name);

        return child.ValueKind == JsonValueKind.String ? child.GetString() ?? string.Empty : string.Empty;
    }

    private static long Number(JsonElement element, string name)
    {
        var child = Child(element, name);

        return child.ValueKind == JsonValueKind.Number && child.TryGetInt64(out var value) ? value : 0;
    }

    private static IEnumerable<string> Strings(JsonElement element, string name)
    {
        var child = Child(element, name);

        return child.ValueKind == JsonValueKind.Array
            ? child
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : "")
            : [];
    }

    private static string RestartPolicy(JsonElement policy)
    {
        var retries = Number(policy, "MaximumRetryCount");

        return Text(policy, "Name") switch {
            "always" => "Always",
            "unless-stopped" => "Unless stopped",
            "on-failure" when retries > 0 => $"On failure, up to {retries} times",
            "on-failure" => "On failure",
            _ => "Never",
        };
    }

    private static InspectFact CpuLimit(JsonElement host)
    {
        var nanoCpus = Number(host, "NanoCpus");
        var quota = Number(host, "CpuQuota");
        var period = Number(host, "CpuPeriod");
        var cpus = nanoCpus > 0 ? nanoCpus / 1e9 : quota > 0 && period > 0 ? (double)quota / period : 0;

        return cpus > 0
            ? new("CPU", $"{cpus.ToString("0.##", CultureInfo.CurrentCulture)} CPUs")
            : new("CPU", "No limit", FactKind.Muted);
    }

    private static FactKind HealthKind(ContainerHealth health)
    {
        return health == ContainerHealth.None ? FactKind.Muted : FactKind.Text;
    }

    private static string Health(ContainerHealth health)
    {
        return health switch {
            ContainerHealth.Healthy => "Healthy",
            ContainerHealth.Unhealthy => "Unhealthy",
            ContainerHealth.Starting => "Starting",
            _ => "None defined",
        };
    }

    private static string When(DateTimeOffset moment, DateTimeOffset now)
    {
        var local = moment.ToLocalTime();
        var time = local.ToString("t", CultureInfo.CurrentCulture);

        return local.Date == now.ToLocalTime().Date
            ? $"Today at {time}"
            : $"{local.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)} at {time}";
    }
}
