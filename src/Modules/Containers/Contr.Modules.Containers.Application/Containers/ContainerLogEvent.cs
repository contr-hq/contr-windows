using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers;

/// <summary>One event of a container's log stream.</summary>
public abstract record ContainerLogEvent
{
    private ContainerLogEvent()
    {
    }

    /// <summary>Decoded output; a chunk may hold part of a line or several lines.</summary>
    public sealed record Output(string Text, bool IsError) : ContainerLogEvent;

    /// <summary>The last event of a stream that broke.</summary>
    public sealed record Failed(Error Error) : ContainerLogEvent;
}
