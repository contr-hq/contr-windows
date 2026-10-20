using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.UI.Common.Formatting;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>One labelled value of the inspect tab.</summary>
/// <param name="IsCode">Shows the value in a monospaced font, as for a command or a path.</param>
public sealed record InspectFact(string Label, string Value, bool IsCode = false);

/// <summary>The inspect tab's summary, read from the engine's inspection document (Docker Engine API).</summary>
public sealed record InspectFacts(
    IReadOnlyList<InspectFact> Configuration,
    IReadOnlyList<InspectFact> State,
    IReadOnlyList<InspectFact> Limits)
{
    private const string _none = "—";

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

        IReadOnlyList<InspectFact> configuration = [
            new("Image", details.Container.Image),
            new("Command", Or(command), IsCode: true),
            new("Working folder", Or(details.WorkingDirectory), IsCode: true),
            new("User", Or(Text(config, "User"), "Image default")),
            new("Restart policy", RestartPolicy(Child(host, "RestartPolicy"))),
            new("Hostname", Or(Text(config, "Hostname")), IsCode: true),
        ];

        IReadOnlyList<InspectFact> state = [
            new("Started", details.StartedAt is { } started ? When(started, now) : "Never"),
            new("Finished", details.FinishedAt is { } finished ? When(finished, now) : _none),
            new("Restarts", details.RestartCount.ToString(CultureInfo.CurrentCulture)),
            new("Health check", Health(details.Health)),
        ];

        IReadOnlyList<InspectFact> limits = [
            new("CPU", CpuLimit(host)),
            new("Memory", Number(host, "Memory") is > 0 and var memory ? HumanFormat.Bytes(memory) : "No limit"),
        ];

        return new InspectFacts(configuration, state, limits);
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

    private static string Or(string value, string fallback = _none)
    {
        return value.Length > 0 ? value : fallback;
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

    private static string CpuLimit(JsonElement host)
    {
        var nanoCpus = Number(host, "NanoCpus");
        var quota = Number(host, "CpuQuota");
        var period = Number(host, "CpuPeriod");
        var cpus = nanoCpus > 0 ? nanoCpus / 1e9 : quota > 0 && period > 0 ? (double)quota / period : 0;

        return cpus > 0 ? $"{cpus.ToString("0.##", CultureInfo.CurrentCulture)} CPUs" : "No limit";
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
            : $"{local.ToString("d", CultureInfo.CurrentCulture)} at {time}";
    }
}
