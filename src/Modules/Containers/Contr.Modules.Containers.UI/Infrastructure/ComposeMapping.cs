using System.Collections.Generic;
using System.Linq;
using System.Text;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Compose;
using Contr.Modules.Containers.Domain;
using ComposeV1 = Contr.Sdk.Compose.V1;
using ContainersV1 = Contr.Sdk.Containers.V1;

namespace Contr.Modules.Containers.UI.Infrastructure;

/// <summary>Maps the daemon's ComposeService messages to the module's Compose types.</summary>
internal static class ComposeMapping
{
    public static ListChange<ComposeProjectDto> ToChange(ComposeV1.WatchProjectsResponse response)
    {
        return new ListChange<ComposeProjectDto>.Snapshot([.. response.Projects.Select(ToDto)]);
    }

    public static ComposeProjectDto ToDto(ComposeV1.Project project)
    {
        return new ComposeProjectDto(
            project.Name,
            project.WorkingDir,
            [.. project.ConfigFiles],
            project.Added,
            [.. project.Services.Select(ToDto)],
            project.ConfigError);
    }

    public static ComposeServiceDto ToDto(ComposeV1.Service service)
    {
        return new ComposeServiceDto(
            service.Name, service.Image, service.BuildContext, [.. service.Containers.Select(ContainerMapping.ToDto)]);
    }

    public static ComposeV1.Action ToWire(ComposeAction action)
    {
        return action switch {
            ComposeAction.Up => ComposeV1.Action.Up,
            ComposeAction.Pull => ComposeV1.Action.Pull,
            ComposeAction.Start => ComposeV1.Action.Start,
            ComposeAction.Stop => ComposeV1.Action.Stop,
            ComposeAction.Restart => ComposeV1.Action.Restart,
            ComposeAction.Down => ComposeV1.Action.Down,
            _ => ComposeV1.Action.Unspecified,
        };
    }

    /// <returns>Absent for an event this client does not know.</returns>
    public static ComposeActionEvent? ToEvent(ComposeV1.RunActionResponse response)
    {
        return response.EventCase switch {
            ComposeV1.RunActionResponse.EventOneofCase.Output => new ComposeActionEvent.Output(response.Output),
            ComposeV1.RunActionResponse.EventOneofCase.ExitCode => new ComposeActionEvent.Exited(response.ExitCode),
            _ => null,
        };
    }
}

/// <summary>Decodes the UTF-8 chunks of a project's merged logs.</summary>
/// <remarks>A chunk can end inside a multi-byte sequence; each output stream of each container keeps its own
/// decoder.</remarks>
internal sealed class ComposeLogDecoder
{
    private readonly Dictionary<(string Container, bool IsError), Decoder> _decoders = [];

    public ComposeLogEvent Decode(ComposeV1.StreamLogsResponse response)
    {
        var isError = response.Stream == ContainersV1.LogStream.Stderr;
        var key = (response.ContainerId, isError);

        if (!_decoders.TryGetValue(key, out var decoder))
        {
            decoder = Encoding.UTF8.GetDecoder();
            _decoders[key] = decoder;
        }

        var bytes = response.Data.Span;
        var chars = new char[decoder.GetCharCount(bytes, flush: false)];
        decoder.GetChars(bytes, chars, flush: false);

        return new ComposeLogEvent.Output(
            response.Service, new ContainerId(response.ContainerId), new string(chars), isError);
    }
}
