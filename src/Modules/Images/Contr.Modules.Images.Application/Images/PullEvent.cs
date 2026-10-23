using Contr.SharedKernel;

namespace Contr.Modules.Images.Application.Images;

/// <summary>One event of an image pull.</summary>
public abstract record PullEvent
{
    private PullEvent()
    {
    }

    /// <summary>The first event of a pull the daemon runs as an operation, which outlives this stream.</summary>
    /// <param name="OperationId">Cancels the pull through the daemon's operations.</param>
    public sealed record Started(string OperationId) : PullEvent;

    /// <summary>Download or extraction progress of one layer.</summary>
    /// <param name="Status">The engine's step, such as <c>Downloading</c> or <c>Pull complete</c>.</param>
    /// <param name="TotalBytes">Zero while unknown.</param>
    public sealed record Layer(string LayerId, string Status, long CurrentBytes, long TotalBytes) : PullEvent;

    /// <summary>An engine status line not tied to a layer, such as the resolved digest.</summary>
    public sealed record StatusLine(string Text) : PullEvent;

    /// <summary>The last event of a pull that stored the image.</summary>
    public sealed record Pulled(string Reference, string ImageId) : PullEvent;

    /// <summary>The last event of a pull that failed.</summary>
    public sealed record Failed(Error Error) : PullEvent;

    /// <summary>The last event of a pull that was cancelled, from this client or another.</summary>
    public sealed record Cancelled : PullEvent;
}
