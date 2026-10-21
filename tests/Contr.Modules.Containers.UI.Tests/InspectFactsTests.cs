using System;
using System.Collections.Generic;
using System.Linq;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class InspectFactsTests
{
    private const string _inspection = """
        {
          "Path": "node",
          "Args": ["dist/server.js"],
          "Config": { "User": "node", "Hostname": "3f9a1c2b7d04" },
          "HostConfig": {
            "RestartPolicy": { "Name": "unless-stopped", "MaximumRetryCount": 0 },
            "NanoCpus": 1500000000,
            "Memory": 0
          }
        }
        """;

    [Fact]
    public void From_ReadsTheInspectionDocument()
    {
        var facts = InspectFacts.From(Details(_inspection), DateTimeOffset.UnixEpoch);

        Assert.Equal("node dist/server.js", Value(facts.Configuration, "Command"));
        Assert.Equal("node", Value(facts.Configuration, "User"));
        Assert.Equal("Unless stopped", Value(facts.Configuration, "Restart policy"));
        Assert.Equal("3f9a1c2b7d04", Value(facts.Configuration, "Hostname"));
        Assert.Equal("1.5 CPUs", Value(facts.Limits, "CPU"));
        Assert.Equal("No limit", Value(facts.Limits, "Memory"));
        Assert.Equal("Never", Value(facts.State, "Started"));
    }

    [Fact]
    public void From_ShowsDefaultsWithoutADocument()
    {
        var facts = InspectFacts.From(Details(string.Empty), DateTimeOffset.UnixEpoch);

        Assert.Equal("Image default", Value(facts.Configuration, "User"));
        Assert.Equal("Never", Value(facts.Configuration, "Restart policy"));
    }

    [Fact]
    public void From_MarksIdentifiersAndMissingValues()
    {
        var facts = InspectFacts.From(Details(_inspection), DateTimeOffset.UnixEpoch);

        Assert.Equal(FactKind.Code, Kind(facts.Configuration, "Image"));
        Assert.Equal(FactKind.Text, Kind(facts.Configuration, "Restart policy"));
        Assert.Equal(FactKind.Muted, Kind(facts.Limits, "Memory"));
        Assert.Equal(FactKind.Muted, Kind(facts.State, "Health check"));
    }

    [Fact]
    public void From_DatesTheStartAndLeavesOutARunningContainersFinish()
    {
        var started = DateTimeOffset.UnixEpoch.AddHours(1);
        var details = Details(_inspection) with {
            StartedAt = started,
            FinishedAt = DateTimeOffset.UnixEpoch,
        };

        var facts = InspectFacts.From(details, started.AddHours(2));

        Assert.Equal("2h ago", facts.State.Single(fact => fact.Label == "Started").Note);
        Assert.DoesNotContain(facts.State, fact => fact.Label == "Finished");
    }

    [Fact]
    public void JsonTree_FindKeepsThePathToAMatchingKey()
    {
        var nodes = JsonTreeNode.Parse(_inspection);

        var found = JsonTreeNode.Find(nodes, "Restart");

        var hostConfig = Assert.Single(found);
        Assert.Equal("\"HostConfig\"", hostConfig.Key);
        Assert.True(hostConfig.IsExpanded);
        Assert.Equal("\"RestartPolicy\"", Assert.Single(hostConfig.Children).Key);
    }

    [Fact]
    public void JsonTree_SummarisesContainers()
    {
        var nodes = JsonTreeNode.Parse(_inspection);

        Assert.Equal("[ 1 item ]", nodes.Single(node => node.Key == "\"Args\"").Value);
        Assert.Equal("{ 2 keys }", nodes.Single(node => node.Key == "\"Config\"").Value);
    }

    private static string Value(IReadOnlyList<InspectFact> facts, string label)
    {
        return facts.Single(fact => fact.Label == label).Value;
    }

    private static FactKind Kind(IReadOnlyList<InspectFact> facts, string label)
    {
        return facts.Single(fact => fact.Label == label).Kind;
    }

    private static ContainerDetailsDto Details(string rawJson)
    {
        var container = new ContainerDto(
            new ContainerId("c0ffee01"),
            "api",
            "node:22",
            ContainerState.Running,
            "Up 2 hours",
            DateTimeOffset.UnixEpoch,
            [],
            string.Empty,
            string.Empty);

        return new ContainerDetailsDto(
            container, null, null, 0, 0, ContainerHealth.None, "/app", [], [], rawJson);
    }
}
