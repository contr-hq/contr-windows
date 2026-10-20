using Contr.SharedKernel;

namespace Contr.Modules.Volumes.Domain;

/// <summary>The errors the Volumes module returns before asking the engine.</summary>
public static class VolumesErrors
{
    public static readonly Error NameRequired = new("Volumes.Name.Required", "Enter a name for the volume.");

    public static Error NameInvalid(string name)
    {
        return new Error(
            "Volumes.Name.Invalid",
            $"'{name}' is not a valid volume name. Use at least two letters, digits, '_', '.' or '-', "
                + "starting with a letter or digit.");
    }
}
