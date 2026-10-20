using Contr.SharedKernel;

namespace Contr.Modules.Containers.Domain;

/// <summary>The errors the Containers module returns before asking the engine.</summary>
public static class ContainersErrors
{
    public static readonly Error CannotStart =
        new("Containers.Container.CannotStart", "Only a created or exited container can start.");

    public static readonly Error CannotStop =
        new("Containers.Container.CannotStop", "The container is not running.");

    public static readonly Error CannotRestart =
        new("Containers.Container.CannotRestart", "The container cannot restart in its current state.");

    public static readonly Error CannotRemove =
        new("Containers.Container.CannotRemove", "The container is already being removed.");
}
