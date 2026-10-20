using System;
using System.Linq;
using Xunit;

namespace Contr.App.Tests.Hosting;

/// <summary>The host reaches the daemon through UI contracts and the shared adapter, never the SDK.</summary>
public sealed class HostBoundaryTests
{
    [Fact]
    public void Host_DoesNotReferenceTheSdkOrTheWire()
    {
        var references = typeof(App)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain("Contr.Sdk", references);
        Assert.DoesNotContain(references, name => name.StartsWith("Grpc.", StringComparison.Ordinal));
        Assert.DoesNotContain("Google.Protobuf", references);
    }
}
