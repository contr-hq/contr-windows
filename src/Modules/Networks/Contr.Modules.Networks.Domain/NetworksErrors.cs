using Contr.SharedKernel;

namespace Contr.Modules.Networks.Domain;

/// <summary>The errors the Networks module returns before asking the engine.</summary>
public static class NetworksErrors
{
    public static readonly Error Predefined =
        new("Networks.Network.Predefined", "The engine's own bridge, host and none networks cannot be removed.");
}
