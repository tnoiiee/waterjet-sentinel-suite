using System.Text.RegularExpressions;
using Xunit;

namespace Wjss.Config.Examples.Tests;

/// <summary>
/// Build-graph and reference-resolution architecture tests (added by the PR #27 exact-source
/// correction pass, 2026-10-15).
///
/// Why these exist: a locked restore passed and then the full Release build failed with 51
/// CS0246 errors in exactly two projects while four other projects compiled unchanged. The defect
/// was NOT in the Source: a <c>TargetPath</c> defined in the Owner's build shell was imported by
/// MSBuild as a global property and hijacked reference/output resolution, so each direct
/// <c>ProjectReference</c> assembly could not be resolved and every type owned by it became
/// "not found". That produced exactly two MSB3245 "expected a reference file but received
/// directory" warnings - one per unresolved direct reference - and CS0246 for the types of those
/// assemblies only.
///
/// The repository must never compensate for that with a path, a reference or a workaround, and it
/// must never acquire the same class of defect on its own. These tests pin the properties that make
/// the graph genuinely resolvable:
///   - every <c>ProjectReference</c> resolves to a project that exists in the repository;
///   - the solution and the projects on disk are the same set;
///   - assembly identities are unique, so no project can shadow another;
///   - no build file sets an output or reference-resolution path, carries an absolute path, a
///     <c>HintPath</c> or an external assembly <c>Reference</c> - the mechanisms by which a
///     machine-local directory (an unrelated motion-control toolset, for example) could corrupt
///     unrelated Product reference resolution;
///   - no project disables analyzer or warning enforcement.
///
/// Static build-file and source assertions only: no build is performed and no compiler is invoked
/// here. The Owner-local build remains the authoritative gate.
/// </summary>
public sealed class ProjectGraphSourceTests
{
    private static readonly string Root = ConfigTestPaths.RepoRoot();

    private const string SolutionFileName = "WaterJetSentinelSuite.sln";

    /// <summary>Directories that never contain a Product project.</summary>
    private static readonly string[] ExcludedDirectories =
    [
        "node_modules", "bin", "obj", ".git", ".vs", "TestResults", "spikes",
    ];

    /// <summary>
    /// Properties a build shell can leak in as environment global properties (the exact root cause
    /// of the failed build) and properties a repository could set in a misguided workaround for it.
    /// </summary>
    private static readonly string[] HijackableProperties =
    [
        "TargetPath", "TargetDir", "TargetFileName", "OutDir", "OutputPath",
        "ReferencePath", "AssemblySearchPaths", "AdditionalLibPaths",
        "AssemblyFoldersEx", "AssemblyFoldersSuffix",
    ];

    /// <summary>The attributes an MSBuild item can carry a value in.</summary>
    private static readonly string[] ItemAttributes = ["Include=\"", "Remove=\""];

    /// <summary>The item kinds whose values are file-system-shaped.</summary>
    private static readonly string[] PathBearingItems =
    [
        "ProjectReference", "Compile", "EmbeddedResource", "None", "Content",
    ];

    /// <summary>
    /// Removes XML comments, so prose that teaches a rule is never mistaken for the defect it
    /// describes. Only element content is a defect; documentation is not.
    /// </summary>
    private static string Code(string xml) =>
        Regex.Replace(xml, "<!--.*?-->", string.Empty, RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    /// <summary>Every Product/test .csproj on disk, as a repository-relative path with forward slashes.</summary>
    private static List<string> ProjectFiles()
    {
        var found = new List<string>();
        Walk(new DirectoryInfo(Root));
        found.Sort(StringComparer.Ordinal);
        return found;

        void Walk(DirectoryInfo directory)
        {
            foreach (var child in directory.EnumerateDirectories())
            {
                if (ExcludedDirectories.Contains(child.Name, StringComparer.OrdinalIgnoreCase)) continue;
                Walk(child);
            }

            foreach (var file in directory.EnumerateFiles("*.csproj"))
            {
                found.Add(Path.GetRelativePath(Root, file.FullName).Replace('\\', '/'));
            }
        }
    }

    /// <summary>The build files that can influence evaluation: every project plus the shared props.</summary>
    private static List<string> BuildFiles()
    {
        var files = ProjectFiles();
        files.Add("Directory.Build.props");
        files.Add("Directory.Packages.props");
        return files;
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Absolute(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Extracts the Include/Remove attribute values of an MSBuild item across the document.</summary>
    private static List<string> ItemValues(string xml, string itemName)
    {
        var values = new List<string>();
        var open = "<" + itemName;
        var searchFrom = 0;
        while (true)
        {
            var start = xml.IndexOf(open, searchFrom, StringComparison.Ordinal);
            if (start < 0) break;
            var end = xml.IndexOf('>', start);
            if (end < 0) break;
            var element = xml[start..end];
            foreach (var attribute in ItemAttributes)
            {
                var attributeStart = element.IndexOf(attribute, StringComparison.Ordinal);
                if (attributeStart < 0) continue;
                var valueStart = attributeStart + attribute.Length;
                var valueEnd = element.IndexOf('"', valueStart);
                if (valueEnd < 0) continue;
                values.Add(element[valueStart..valueEnd]);
            }

            searchFrom = end;
        }

        return values;
    }

    [Fact]
    public void Every_ProjectReference_Resolves_To_An_Existing_Project()
    {
        var projects = ProjectFiles();
        Assert.NotEmpty(projects);

        var referenceCount = 0;
        foreach (var project in projects)
        {
            var directory = Path.GetDirectoryName(Absolute(project))!;
            foreach (var reference in ItemValues(Read(project), "ProjectReference"))
            {
                referenceCount++;

                // A property-expanded reference path cannot be checked statically. None exists, and
                // introducing one must be a deliberate decision rather than an unnoticed edit.
                Assert.DoesNotContain("$(", reference, StringComparison.Ordinal);
                Assert.EndsWith(".csproj", reference);

                var resolved = Path.GetFullPath(Path.Combine(directory, reference));
                Assert.True(
                    File.Exists(resolved),
                    $"{project} references '{reference}', which does not resolve to an existing project file ({resolved}).");
                Assert.StartsWith(Root, resolved, StringComparison.Ordinal);
            }
        }

        // A graph of zero edges would pass vacuously; the real graph carries far more than 15.
        Assert.True(referenceCount >= 15, $"expected the full ProjectReference graph, found {referenceCount} references");
    }

    [Fact]
    public void The_Solution_And_The_Projects_On_Disk_Are_The_Same_Set()
    {
        // The solution is not XML: each entry is `Project("{type}") = "name", "path", "{guid}"`.
        var inSolution = new List<string>();
        foreach (Match match in Regex.Matches(
            Read(SolutionFileName), "\"([^\"]+\\.csproj)\"", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            inSolution.Add(match.Groups[1].Value.Replace('\\', '/'));
        }

        inSolution.Sort(StringComparer.Ordinal);

        var onDisk = ProjectFiles();
        Assert.Equal(onDisk, inSolution);

        // Every entry is also a real file, so a stale or duplicated entry cannot hide behind the set
        // comparison above.
        foreach (var project in inSolution)
        {
            Assert.True(File.Exists(Absolute(project)), project);
        }

        // No project exists outside the solution, so a build of the solution covers the whole graph.
        Assert.Equal(onDisk.Count, onDisk.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Assembly_Identities_Are_Unique_So_No_Project_Can_Shadow_Another()
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in ProjectFiles())
        {
            var declared = Regex.Match(
                Read(project), @"<AssemblyName>([^<]+)</AssemblyName>",
                RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value;

            // With no explicit AssemblyName, the SDK derives it from the file name.
            var assemblyName = declared.Length > 0 ? declared : Path.GetFileNameWithoutExtension(project);

            Assert.False(
                seen.TryGetValue(assemblyName, out var owner),
                $"assembly name '{assemblyName}' is produced by both {owner} and {project}");
            seen[assemblyName] = project;
        }

        Assert.NotEmpty(seen);
    }

    [Fact]
    public void No_Build_File_Sets_An_Output_Or_Reference_Resolution_Path()
    {
        foreach (var file in BuildFiles())
        {
            var code = Code(Read(file));
            foreach (var property in HijackableProperties)
            {
                var withValue = Regex.IsMatch(
                    code, $@"<{property}\s*[^>]*>[^<]+</{property}>",
                    RegexOptions.None, TimeSpan.FromSeconds(1));
                var emptyElement = Regex.IsMatch(
                    code, $@"<{property}\s*/>",
                    RegexOptions.None, TimeSpan.FromSeconds(1));

                Assert.False(withValue || emptyElement, $"{file} sets {property}");
            }

            // The only sanctioned dependency mechanisms are a ProjectReference and a centrally
            // pinned PackageReference: no external assembly reference and no HintPath anywhere.
            Assert.DoesNotContain("<HintPath>", code, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<Reference Include=", code, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void No_Build_File_Carries_An_Absolute_Or_Vendor_Local_Path()
    {
        foreach (var file in BuildFiles())
        {
            var code = Code(Read(file));

            Assert.DoesNotMatch(@"[A-Za-z]:[\\/]", code);
            Assert.DoesNotMatch(@"\\\\[A-Za-z0-9]", code);
            Assert.DoesNotMatch(@"[""'>](?:/usr|/home|/opt|/mnt)/", code);
            // The unresolved-directory warning named a machine-local motion-control toolset. No build
            // file may name such a location, and none may name any vendor directory.
            Assert.DoesNotContain("KMotion", code, StringComparison.OrdinalIgnoreCase);

            // Any file-system-shaped item value stays relative and inside the repository.
            foreach (var item in PathBearingItems)
            {
                foreach (var value in ItemValues(code, item))
                {
                    if (value.Contains("$(", StringComparison.Ordinal)) continue;
                    if (!value.Contains('/') && !value.Contains('\\')) continue;
                    Assert.DoesNotMatch(@"[A-Za-z]:", value);
                    Assert.False(value.StartsWith('/') || value.StartsWith('\\'), $"{file}: rooted item value '{value}'");
                }
            }
        }
    }

    [Fact]
    public void No_Project_Disables_Analyzer_Or_Warning_Enforcement()
    {
        foreach (var project in ProjectFiles())
        {
            var code = Code(Read(project));
            Assert.DoesNotContain("TreatWarningsAsErrors>false", code, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<RunAnalyzers>false", code, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<EnableNETAnalyzers>false", code, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<WarningsNotAsErrors>", code, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#pragma warning disable", code, StringComparison.OrdinalIgnoreCase);
        }

        // The shared policy still enforces both, so the assertions above are not vacuous.
        var shared = Code(Read("Directory.Build.props"));
        Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("<RunAnalyzers>false", shared, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_One_Node_Physical_Read_Only_Path_Compiles_Through_The_Intended_Graph()
    {
        // The exact edges the PR #27 vertical slice depends on. A change here changes which assembly
        // a namespace can come from, which is the class this pass is about.
        Assert.Contains("../contracts/Wjss.Contracts.csproj", Read("packages/domain/Wjss.Domain.csproj"), StringComparison.Ordinal);
        Assert.Contains(
            "../../adapters/tc-acquisition/Wjss.Adapters.TcAcquisition.csproj",
            Read("apps/tc-bench/Wjss.TcBench.csproj"), StringComparison.Ordinal);
        Assert.Contains(
            "../../adapters/tc-acquisition/Wjss.Adapters.TcAcquisition.csproj",
            Read("apps/runtime/Wjss.Runtime.csproj"), StringComparison.Ordinal);

        // The adapter stays self-owned (Library Selection Gate / ADR-0018): it must never acquire a
        // ProjectReference, so the physical transport boundary cannot silently grow a dependency on
        // the contracts, the domain or the application layer.
        Assert.DoesNotContain(
            "<ProjectReference", Code(Read("adapters/tc-acquisition/Wjss.Adapters.TcAcquisition.csproj")), StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<ProjectReference", Code(Read("packages/contracts/Wjss.Contracts.csproj")), StringComparison.Ordinal);

        // The types the failed build could not resolve are declared by the assemblies above, so a
        // future move of a type out of its assembly fails here rather than in an Owner-local build.
        Assert.Contains("public enum DeviceProfile", Read("packages/contracts/Enums.cs"), StringComparison.Ordinal);
        Assert.Contains("public interface ITcReadOnlyClient", Read("adapters/tc-acquisition/ITcReadOnlyClient.cs"), StringComparison.Ordinal);

        // Both modes remain expressible on the profile contract after this pass, with the standing
        // refusals retained.
        var enums = Read("packages/contracts/Enums.cs");
        Assert.Contains("TEST_HARDWARE_READ_ONLY", enums, StringComparison.Ordinal);
        Assert.Contains("TEST_HARDWARE", enums, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION", enums, StringComparison.Ordinal);
    }
}
