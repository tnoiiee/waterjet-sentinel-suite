namespace Wjss.Config.Examples.Tests;

/// <summary>
/// Stable assembly anchor for <c>Wjss.Config.Examples.Tests</c> (Owner-local
/// build round 4, 2026-10-07). The source-preservation tests previously routed
/// repository-root discovery through the <c>ExampleConfigTests</c> class name,
/// which lives in a different namespace - a renamable test class must never be
/// a structural dependency. This marker type carries no behaviour; it only
/// identifies the test assembly. Internal to this project; no Product usage.
/// </summary>
internal sealed class ConfigTestAssemblyMarker;

/// <summary>
/// Shared repository-root discovery for the config test project. Behaviour is
/// identical to the original <c>ExampleConfigTests.RepoRoot</c>: walk up from
/// the directory of the running test assembly until the solution file exists,
/// so the tests always inspect the repository's Product SOURCE files and
/// never copied build output. No absolute paths, no environment assumptions.
/// </summary>
internal static class ConfigTestPaths
{
    /// <summary>Full path of the repository root (directory containing WaterJetSentinelSuite.sln).</summary>
    /// <exception cref="InvalidOperationException">the test is running outside a WJSS checkout</exception>
    public static string RepoRoot()
    {
        var location = typeof(ConfigTestAssemblyMarker).Assembly.Location;
        var startDirectory = string.IsNullOrEmpty(location)
            ? AppContext.BaseDirectory // single-file/loaded-from-memory safety net; same walk applies
            : Path.GetDirectoryName(location)!;

        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WaterJetSentinelSuite.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Repository root (WaterJetSentinelSuite.sln) not found above the test assembly directory.");
    }
}
