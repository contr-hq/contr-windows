namespace Contr.Modules.Containers.Domain;

/// <summary>A container's lifecycle state as the engine reports it.</summary>
public enum ContainerState
{
    Unknown,
    Created,
    Running,
    Paused,
    Restarting,
    Removing,
    Exited,
    Dead,
    Stopping,
}
