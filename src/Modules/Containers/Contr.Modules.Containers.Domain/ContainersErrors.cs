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

    public static readonly Error CannotPause =
        new("Containers.Container.CannotPause", "Only a running container can pause.");

    public static readonly Error CannotUnpause =
        new("Containers.Container.CannotUnpause", "The container is not paused.");

    public static readonly Error CannotKill =
        new("Containers.Container.CannotKill", "The container is not running.");

    public static readonly Error NameRequired =
        new("Containers.Name.Required", "Enter a name.");

    public static readonly Error ImageRequired =
        new("Containers.Image.Required", "Enter an image.");

    public static Error NameInvalid(string name)
    {
        return new Error(
            "Containers.Name.Invalid",
            $"'{name}' is not a valid name. Use letters, digits, '_', '.' and '-', starting with a letter or digit.");
    }

    public static Error PortInvalid(string port)
    {
        return new Error(
            "Containers.Port.Invalid",
            $"'{port}' is not a valid port. Use a number from 1 to 65535, such as 8080 or 80/tcp.");
    }

    public static Error PathInvalid(string path)
    {
        return new Error(
            "Containers.Path.Invalid",
            $"'{path}' is not a path in the container. It starts with '/', such as /app.");
    }
}
