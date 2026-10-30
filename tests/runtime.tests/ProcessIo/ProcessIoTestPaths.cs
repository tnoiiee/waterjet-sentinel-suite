namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stable assembly anchor for the Process-I/O focused tests. A renamable test class must never
/// be a structural dependency, so repository-root discovery is routed through this marker type
/// instead of a test class name. Internal to this project; no Product usage.
/// </summary>
internal sealed class ProcessIoTestAssemblyMarker;

/// <summary>
/// Shared repository-root discovery for the Process-I/O focused tests, following the established
/// pattern of the config test project: walk up from the directory of the running test assembly
/// until the solution file exists, so tests always inspect the repository SOURCE evidence and
/// never a copied build output. No absolute paths, no fixed relative segment count, no
/// environment assumptions.
/// </summary>
internal static class ProcessIoTestPaths
{
    private const string SolutionFileName = "WaterJetSentinelSuite.sln";

    private const string CatalogueRelativePath = "docs/evidence/process-io-inventory-2026-10-22.csv";

    /// <summary>Full path of the repository root (directory containing WaterJetSentinelSuite.sln).</summary>
    /// <exception cref="InvalidOperationException">the test is running outside a WJSS checkout</exception>
    public static string RepoRoot()
    {
        var location = typeof(ProcessIoTestAssemblyMarker).Assembly.Location;
        var startDirectory = string.IsNullOrEmpty(location)
            ? AppContext.BaseDirectory // single-file or loaded-from-memory safety net; same walk applies
            : Path.GetDirectoryName(location)!;

        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Repository root (WaterJetSentinelSuite.sln) not found above the test assembly directory.");
    }

    /// <summary>Relative path of the committed, generator-owned Process-I/O inventory evidence file.</summary>
    public static string CommittedCatalogueRelativePath => CatalogueRelativePath;

    /// <summary>Full path of the committed, generator-owned Process-I/O inventory evidence file.</summary>
    public static string CommittedCataloguePath =>
        Path.Combine(RepoRoot(), "docs", "evidence", "process-io-inventory-2026-10-22.csv");
}
