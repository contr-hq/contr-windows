using Contr.Modules.Containers.Domain;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Compose;

/// <summary>One event of a Compose project's merged logs.</summary>
public abstract record ComposeLogEvent
{
    private ComposeLogEvent()
    {
    }

    /// <summary>Decoded output of one container; a chunk may hold part of a line or several lines.</summary>
    public sealed record Output(string Service, ContainerId Container, string Text, bool IsError) : ComposeLogEvent;

    /// <summary>The last event of a stream that broke.</summary>
    public sealed record Failed(Error Error) : ComposeLogEvent;
}
