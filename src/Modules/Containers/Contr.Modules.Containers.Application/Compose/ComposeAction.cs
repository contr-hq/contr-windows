using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Compose;

/// <summary>A Compose command run on a project.</summary>
public enum ComposeAction
{
    /// <summary>Creates and starts the services, pulling or building images they lack; needs Compose files.</summary>
    Up,

    /// <summary>Pulls the services' images; needs Compose files.</summary>
    Pull,

    Start,

    Stop,

    Restart,

    /// <summary>Stops and removes the project's containers and networks, for the whole project.</summary>
    Down,
}

/// <summary>One event of a Compose command's run.</summary>
public abstract record ComposeActionEvent
{
    private ComposeActionEvent()
    {
    }

    /// <summary>A line Compose wrote, without its line ending.</summary>
    public sealed record Output(string Line) : ComposeActionEvent;

    /// <summary>The last event of a run Compose finished; zero on success.</summary>
    public sealed record Exited(int ExitCode) : ComposeActionEvent;

    /// <summary>The last event of a run the daemon refused or that broke.</summary>
    public sealed record Failed(Error Error) : ComposeActionEvent;
}
