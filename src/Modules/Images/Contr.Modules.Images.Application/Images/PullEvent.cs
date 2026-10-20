using Contr.SharedKernel;

namespace Contr.Modules.Images.Application.Images;

/// <summary>One event of an image pull.</summary>
public abstract record PullEvent
{
    private PullEvent()
    {
    }

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
}
