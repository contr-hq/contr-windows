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
