using System;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.Infrastructure;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using ContainersV1 = Contr.Sdk.Containers.V1;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ContainerMappingTests
{
    [Fact]
    public void ToDto_ReadsTheComposeProjectAndPorts()
    {
        var container = new ContainersV1.Container {
            Id = "c0ffee0100000000",
            Image = "nginx:1.29",
            State = ContainersV1.ContainerState.Running,
            Status = "Up 2 hours",
            CreatedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UnixEpoch),
        };
        container.Names.Add("shop-web-1");
        container.Labels.Add("com.docker.compose.project", "shop");
        container.Ports.Add(new ContainersV1.PortMapping {
            ContainerPort = 80,
            HostPort = 8080,
            HostIp = "0.0.0.0",
            Protocol = ContainersV1.Protocol.Tcp,
        });

        var dto = ContainerMapping.ToDto(container);

        Assert.Equal("shop-web-1", dto.Name);
        Assert.Equal("shop", dto.ComposeProject);
        Assert.Equal(ContainerState.Running, dto.State);
        Assert.Equal(new PortDto(80, 8080, "0.0.0.0", "tcp"), Assert.Single(dto.Ports));
    }

    [Fact]
    public void ToDto_NamesAnUnnamedContainerByItsShortId()
    {
        var dto = ContainerMapping.ToDto(new ContainersV1.Container { Id = "c0ffee0100000000" });

        Assert.Equal("c0ffee010000", dto.Name);
        Assert.Equal(string.Empty, dto.ComposeProject);
    }

    [Fact]
    public void ToChange_MapsEveryEventKind()
    {
        var snapshot = new ContainersV1.WatchContainersResponse { Snapshot = new ContainersV1.ContainerSnapshot() };
        var removed = new ContainersV1.WatchContainersResponse { RemovedId = "c0ffee01" };

        var removal = Assert.IsType<ListChange<ContainerDto>.Removed>(ContainerMapping.ToChange(removed));

        Assert.IsType<ListChange<ContainerDto>.Snapshot>(ContainerMapping.ToChange(snapshot));
        Assert.Equal("c0ffee01", removal.Key);
        Assert.Null(ContainerMapping.ToChange(new ContainersV1.WatchContainersResponse()));
    }

    [Fact]
    public void LogDecoder_JoinsACharacterSplitAcrossChunks()
    {
        var decoder = new LogDecoder();

        var first = decoder.Decode(Chunk(ContainersV1.LogStream.Stdout, 0x68, 0xC3));
        var second = decoder.Decode(Chunk(ContainersV1.LogStream.Stdout, 0xA9, 0x0A));
        var error = decoder.Decode(Chunk(ContainersV1.LogStream.Stderr, 0x21));

        Assert.Equal("h", Assert.IsType<ContainerLogEvent.Output>(first).Text);
        Assert.Equal("é\n", Assert.IsType<ContainerLogEvent.Output>(second).Text);
        Assert.True(Assert.IsType<ContainerLogEvent.Output>(error).IsError);
    }

    private static ContainersV1.StreamLogsResponse Chunk(ContainersV1.LogStream stream, params byte[] bytes)
    {
        return new ContainersV1.StreamLogsResponse { Stream = stream, Data = ByteString.CopyFrom(bytes) };
    }
}
