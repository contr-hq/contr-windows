using Contr.SharedKernel;

namespace Contr.Modules.Networks.Domain;

/// <summary>The errors the Networks module returns before asking the engine.</summary>
public static class NetworksErrors
{
    public static readonly Error Predefined =
        new("Networks.Network.Predefined", "The engine's own bridge, host and none networks cannot be removed.");

    public static Error InvalidName(string name)
    {
        return name.Length == 0
            ? new Error("Networks.Name.Empty", "Give the network a name.")
            : new Error("Networks.Name.Predefined", $"'{name}' is one of the engine's own networks.");
    }
}
