using System.Collections.Generic;
using Contr.Modules.Containers.UI;
using Contr.Modules.Images.UI;
using Contr.Modules.Networks.UI;
using Contr.Modules.Volumes.UI;
using Contr.UI.Contracts;

namespace Contr.App.Composition;

/// <summary>The feature modules composed into the application, in navigation order.</summary>
internal static class ModuleCatalog
{
    public static IReadOnlyList<IModule> Modules { get; } = [
        new ContainersModule(),
        new ImagesModule(),
        new VolumesModule(),
        new NetworksModule(),
    ];
}
