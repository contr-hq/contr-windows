using System;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers;

/// <summary>One event of a container's log stream.</summary>
public abstract record ContainerLogEvent
{
    private ContainerLogEvent()
    {
    }

    /// <summary>Decoded output; a chunk may hold part of a line or several lines.</summary>
    /// <param name="Timestamp">When the engine recorded the output; present only when the range asked for it.</param>
    public sealed record Output(string Text, bool IsError, DateTimeOffset? Timestamp = null) : ContainerLogEvent;

    /// <summary>The last event of a stream that broke.</summary>
    public sealed record Failed(Error Error) : ContainerLogEvent;
}

/// <summary>Which part of a log to send before following new output.</summary>
/// <param name="TailLines">The lines to send from the end of the log; absent sends every line.</param>
/// <param name="Since">Sends only output written at or after this time.</param>
/// <param name="Timestamps">Whether each output carries the time the engine recorded it.</param>
public sealed record LogRange(int? TailLines, DateTimeOffset? Since, bool Timestamps);
