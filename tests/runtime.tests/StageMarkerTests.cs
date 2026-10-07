using System.Reflection;
using Wjss.Contracts;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Pins the Stage 0.3A-1 markers. If 0.3A-2 lands and someone removes the
/// marker while /health/ready still answers it, these tests must fail.
/// </summary>
public sealed class StageMarkerTests
{
    [Fact]
    public void Runtime_Not_Implemented_Code_Is_Exact()
    {
        Assert.Equal("RUNTIME_NOT_IMPLEMENTED", Stage03A1.RuntimeNotImplemented);
        Assert.Equal("STAGE_03A1_SKELETON", Stage03A1.Marker);
    }

    [Fact]
    public void Core_Assembly_Is_Empty_Wired_Staging_Area()
    {
        // Wjss.Runtime.Core has no public types at 0.3A-1 by design (0.3A-2
        // adds the single-writer engine). Loading by name proves the project
        // is in the solution graph and its output is reachable; the compiler
        // does not bake a static reference to an assembly no type is used
        // from, so we resolve it through the default load context instead.
        var loaded = Assembly.Load(new AssemblyName("Wjss.Runtime.Core"));
        Assert.Equal("Wjss.Runtime.Core", loaded.GetName().Name);
        Assert.Empty(loaded.GetExportedTypes());
    }
}
