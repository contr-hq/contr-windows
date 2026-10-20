namespace Contr.Modules.Containers.Domain;

/// <summary>The result of a container's health check.</summary>
public enum ContainerHealth
{
    /// <summary>The image defines no health check.</summary>
    None,
    Starting,
    Healthy,
    Unhealthy,
}
