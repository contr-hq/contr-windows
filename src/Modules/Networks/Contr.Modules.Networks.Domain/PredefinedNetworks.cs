using System;
using System.Collections.Generic;

namespace Contr.Modules.Networks.Domain;

/// <summary>The networks the engine creates itself, which cannot be removed.</summary>
public static class PredefinedNetworks
{
    private static readonly HashSet<string> _names = new(StringComparer.Ordinal) { "bridge", "host", "none" };

    public static bool IsPredefined(string name)
    {
        return _names.Contains(name);
    }
}
