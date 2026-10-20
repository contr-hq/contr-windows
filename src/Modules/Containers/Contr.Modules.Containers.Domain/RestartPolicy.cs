namespace Contr.Modules.Containers.Domain;

/// <summary>What the engine does when a container exits.</summary>
public enum RestartMode
{
    No,
    Always,
    UnlessStopped,
    OnFailure,
}

/// <param name="MaxRetries">Restarts before giving up; only <see cref="RestartMode.OnFailure"/> uses it, and zero
/// means no limit.</param>
public sealed record RestartPolicy(RestartMode Mode, int MaxRetries = 0)
{
    public static RestartPolicy Never { get; } = new(RestartMode.No);
}
