using System.Reflection;
using Wjss.Contracts;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Pins the stage markers and the identity of the Runtime core assembly.
///
/// Stage 0.3A-1 asserted that Wjss.Runtime.Core was an empty staging area. Stage
/// 0.3A-2A replaced that authored-time statement with the state-foundation
/// identity, and Stage 0.3A-2C moves the marker to the substage that now exists
/// (state store + synthetic evolution + Snapshot/Delta foundation + read-only
/// Runtime API). The 0.3A-1 marker constants stay pinned as authored-time
/// vocabulary: the health payload no longer answers them, but nothing may reuse
/// or reinterpret them.
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
    public void Core_Assembly_Is_Wired_And_Carries_The_Runtime_Foundation()
    {
        // Loading by name proves the project is in the solution graph and its
        // output is reachable; the runtime state foundation is the core's first
        // public surface (Stage 0.3A-2A).
        var loaded = Assembly.Load(new AssemblyName("Wjss.Runtime.Core"));

        Assert.Equal("Wjss.Runtime.Core", loaded.GetName().Name);
        Assert.Equal(typeof(RuntimeStateStore).Assembly, loaded);
        Assert.Equal("STAGE_03A2C_RUNTIME_API", RuntimeStage.Marker);
    }
}
