namespace Contr.Modules.Containers.Domain;

/// <summary>Which lifecycle actions the engine accepts in each <see cref="ContainerState"/>.</summary>
public static class ContainerLifecycle
{
    public static bool CanStart(this ContainerState state)
    {
        return state is ContainerState.Created or ContainerState.Exited;
    }

    public static bool CanStop(this ContainerState state)
    {
        return state is ContainerState.Running or ContainerState.Paused or ContainerState.Restarting;
    }

    public static bool CanRestart(this ContainerState state)
    {
        return state is ContainerState.Running or ContainerState.Paused
            or ContainerState.Created or ContainerState.Exited;
    }

    public static bool CanPause(this ContainerState state)
    {
        return state is ContainerState.Running;
    }

    public static bool CanUnpause(this ContainerState state)
    {
        return state is ContainerState.Paused;
    }

    /// <summary>Whether there is a main process to send a signal to.</summary>
    public static bool CanKill(this ContainerState state)
    {
        return state is ContainerState.Running or ContainerState.Paused or ContainerState.Restarting;
    }

    public static bool CanRemove(this ContainerState state)
    {
        return state is not ContainerState.Removing;
    }

    /// <summary>Whether removal must kill the container first; the engine refuses it otherwise.</summary>
    public static bool RemovalNeedsForce(this ContainerState state)
    {
        return state is ContainerState.Running or ContainerState.Paused
            or ContainerState.Restarting or ContainerState.Stopping;
    }
}
