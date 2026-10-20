using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Contr.Modules.Containers.Application;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Images.Application;
using Contr.Modules.Images.Domain;
using Contr.Modules.Networks.Application;
using Contr.Modules.Networks.Domain;
using Contr.Modules.Volumes.Application;
using Contr.Modules.Volumes.Domain;
using Contr.SharedKernel;
using Xunit;

namespace Contr.ArchitectureTests;

/// <summary>
/// The modular-monolith dependency rules, checked on referenced assembly names so no WinUI layer
/// is loaded.
/// </summary>
public sealed class LayeringTests
{
    private const string _domain = "Domain";
    private const string _application = "Application";

    /// <summary>Every module's Domain and Application assembly, by module and layer.</summary>
    public static TheoryData<string, string, Assembly> Layers { get; } = new() {
        { "Containers", _domain, typeof(ContainerId).Assembly },
        { "Containers", _application, ContainersApplicationServiceCollectionExtensions.Assembly },
        { "Images", _domain, typeof(ImageReference).Assembly },
        { "Images", _application, ImagesApplicationServiceCollectionExtensions.Assembly },
        { "Volumes", _domain, typeof(VolumeName).Assembly },
        { "Volumes", _application, VolumesApplicationServiceCollectionExtensions.Assembly },
        { "Networks", _domain, typeof(NetworkId).Assembly },
        { "Networks", _application, NetworksApplicationServiceCollectionExtensions.Assembly },
    };

    [Fact]
    public void SharedKernel_DependsOnlyOnTheBaseClassLibrary()
    {
        var references = ReferencedAssemblyNames(typeof(Result).Assembly);

        Assert.DoesNotContain(
            references, name => name.StartsWith(TestConstants.AssemblyPrefix, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void Domain_DependsOnlyOnTheSharedKernel(string module, string layer, Assembly assembly)
    {
        if (layer != _domain)
        {
            return;
        }

        var references = ReferencedAssemblyNames(assembly)
            .Where(name => name.StartsWith(TestConstants.AssemblyPrefix, StringComparison.Ordinal))
            .Where(name => name != TestConstants.AssemblyPrefix + "SharedKernel")
            .ToList();

        Assert.True(references.Count == 0, $"{module}.Domain references {string.Join(", ", references)}");
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void Application_DoesNotDependOnTheUiOrHost(string module, string layer, Assembly assembly)
    {
        if (layer != _application)
        {
            return;
        }

        var references = ReferencedAssemblyNames(assembly)
            .Where(name => name.Contains(".UI", StringComparison.Ordinal)
                || name.EndsWith(".App", StringComparison.Ordinal))
            .ToList();

        Assert.True(references.Count == 0, $"{module}.Application references {string.Join(", ", references)}");
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void Layer_DoesNotDependOnAnotherModule(string module, string layer, Assembly assembly)
    {
        var own = $"{TestConstants.ModulesPrefix}{module}.";
        var references = ReferencedAssemblyNames(assembly)
            .Where(name => name.StartsWith(TestConstants.ModulesPrefix, StringComparison.Ordinal))
            .Where(name => !name.StartsWith(own, StringComparison.Ordinal))
            .ToList();

        Assert.True(references.Count == 0, $"{module}.{layer} references {string.Join(", ", references)}");
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void Layer_DoesNotDependOnTheDaemonWire(string module, string layer, Assembly assembly)
    {
        var references = ReferencedAssemblyNames(assembly)
            .Where(name => name == TestConstants.SdkAssembly
                || name == "Google.Protobuf"
                || name.StartsWith("Grpc.", StringComparison.Ordinal))
            .ToList();

        Assert.True(references.Count == 0, $"{module}.{layer} references {string.Join(", ", references)}");
    }

    private static IReadOnlyList<string> ReferencedAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToList();
    }
}
