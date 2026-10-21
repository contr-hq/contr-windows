using System.Linq;
using System.Text;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Compose;
using Contr.Modules.Containers.UI.Infrastructure;
using Google.Protobuf;
using Xunit;
using ComposeV1 = Contr.Sdk.Compose.V1;
using ContainersV1 = Contr.Sdk.Containers.V1;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ComposeMappingTests
{
    [Fact]
    public void ToChange_ReadsEveryProjectAsASnapshot()
    {
        var worker = new ComposeV1.Service {
            Name = "worker",
            Image = "shop-worker",
            BuildContext = @"C:\src\shop\worker",
        };
        var api = new ComposeV1.Service { Name = "api", Image = "node:22-alpine" };
        var container = new ContainersV1.Container { Id = "c0ffee01", State = ContainersV1.ContainerState.Running };
        container.Names.Add("shop-api-1");
        api.Containers.Add(container);
        var project = new ComposeV1.Project {
            Name = "shop",
            WorkingDir = @"C:\src\shop",
            Added = true,
            ConfigError = "yaml: line 3",
        };
        project.ConfigFiles.Add(@"C:\src\shop\compose.yaml");
        project.Services.Add(api);
        project.Services.Add(worker);

        var change = ComposeMapping.ToChange(new ComposeV1.WatchProjectsResponse { Projects = { project } });

        var dto = Assert.Single(Assert.IsType<ListChange<ComposeProjectDto>.Snapshot>(change).Items);
        Assert.Equal(
            ("shop", @"C:\src\shop", true, "yaml: line 3"), (dto.Name, dto.WorkingDir, dto.IsAdded, dto.ConfigError));
        Assert.Equal([@"C:\src\shop\compose.yaml"], dto.ConfigFiles);
        Assert.Equal("shop-api-1", Assert.Single(dto.Services[0].Containers).Name);
        Assert.Equal(@"C:\src\shop\worker", dto.Services[1].BuildContext);
    }

    [Theory]
    [InlineData(ComposeAction.Up, ComposeV1.Action.Up)]
    [InlineData(ComposeAction.Down, ComposeV1.Action.Down)]
    [InlineData(ComposeAction.Restart, ComposeV1.Action.Restart)]
    public void ToWire_NamesTheAction(ComposeAction action, ComposeV1.Action expected)
    {
        Assert.Equal(expected, ComposeMapping.ToWire(action));
    }

    [Fact]
    public void ToEvent_ReadsOutputThenTheExitCode()
    {
        Assert.Equal(
            new ComposeActionEvent.Output("Pulling api"),
            ComposeMapping.ToEvent(new ComposeV1.RunActionResponse { Output = "Pulling api" }));
        Assert.Equal(
            new ComposeActionEvent.Exited(17),
            ComposeMapping.ToEvent(new ComposeV1.RunActionResponse { ExitCode = 17 }));
        Assert.Null(ComposeMapping.ToEvent(new ComposeV1.RunActionResponse()));
    }

    [Fact]
    public void Decode_KeepsEachContainersPartialCharacter()
    {
        var euro = Encoding.UTF8.GetBytes("€");
        var decoder = new ComposeLogDecoder();

        var first = decoder.Decode(Chunk("api", "a1", euro[..1]));
        var other = decoder.Decode(Chunk("db", "d1", Encoding.UTF8.GetBytes("ready")));
        var rest = decoder.Decode(Chunk("api", "a1", euro[1..]));

        var texts = new[] { first, other, rest }
            .Cast<ComposeLogEvent.Output>()
            .Select(output => output.Text);

        Assert.Equal(["", "ready", "€"], texts);
        Assert.Equal("db", ((ComposeLogEvent.Output)other).Service);
    }

    private static ComposeV1.StreamLogsResponse Chunk(string service, string container, byte[] data)
    {
        return new ComposeV1.StreamLogsResponse {
            Service = service,
            ContainerId = container,
            Stream = ContainersV1.LogStream.Stdout,
            Data = ByteString.CopyFrom(data),
        };
    }
}
