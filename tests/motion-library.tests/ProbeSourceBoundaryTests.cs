using System.Text.RegularExpressions;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Static source and asset boundary coverage of the native load / version probe gate.
/// These tests pin the PUBLIC-REPOSITORY posture of the new sources without building anything:
/// only the authorized export; no vendor import-table binding; no committed directory or real
/// hash; environment-variable names free of the S6 host rule; no coupling to the Motion
/// execution side, the Runtime composition or the Preview UI; and the native-artefact ignore
/// rules in place. (S1–S9 are separately machine-checked by tools/boundary-scan.)
/// </summary>
public sealed class ProbeSourceBoundaryTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    /// <summary>
    /// Path segments of the two probe gate directories under the repository root, allocated
    /// once: <see cref="GateFiles"/> combines each entry with the runtime root, in this order.
    /// </summary>
    private static readonly string[][] GateDirectorySegments =
    {
        new[] { "adapters", "motion-library" },
        new[] { "apps", "motion-probe" },
    };

    /// <summary>
    /// The complete committed set of gate environment-variable names, allocated once; the
    /// assertion still applies the same ordinal sort at the use site.
    /// </summary>
    private static readonly string[] GateEnvironmentVariableNames =
    {
        "WJSS_MOTION_LIB_DIR",
        "WJSS_MOTION_LIB_EXPECTED_VERSION",
        "WJSS_MOTION_LIB_GCLIB_SHA256",
        "WJSS_MOTION_LIB_GCLIBO_SHA256",
    };

    private static List<string> GateFiles()
    {
        var root = RepoRoot();
        var files = new List<string>();
        foreach (var segments in GateDirectorySegments)
        {
            var directory = Path.Combine(root, segments[0], segments[1]);
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".cs", StringComparison.Ordinal)
                    || file.EndsWith(".csproj", StringComparison.Ordinal)
                    || file.EndsWith(".md", StringComparison.Ordinal))
                {
                    files.Add(file);
                }
            }
        }

        Assert.NotEmpty(files);
        return files;
    }

    private static string ReadGateSources()
    {
        var combined = string.Empty;
        foreach (var file in GateFiles())
        {
            combined += File.ReadAllText(file) + "\n";
        }

        return combined;
    }

    [Fact]
    public void The_Committed_Vocabulary_Carries_The_Required_Refusal_Codes()
    {
        var source = ReadGateSources();
        var required = new[]
        {
            "NATIVE_LIB_DIR_NOT_CONFIGURED",
            "NATIVE_LIB_DIR_REJECTED",
            "NATIVE_FILE_NOT_FOUND",
            "NATIVE_FILE_IDENTITY_MISMATCH",
            "NATIVE_ARCHITECTURE_MISMATCH",
            "NATIVE_LOAD_FAILED",
            "NATIVE_EXPORT_NOT_FOUND",
            "NATIVE_VERSION_CALL_FAILED",
            "NATIVE_VERSION_MISMATCH",
            "NATIVE_MODULE_PATH_MISMATCH",
            "NATIVE_UNLOAD_FAILED",
        };

        foreach (var code in required)
        {
            Assert.Contains($"\"{code}\"", source);
        }
    }

    [Fact]
    public void Only_The_Authorized_Export_Is_Named_And_No_Vendor_Import_Exists()
    {
        var source = ReadGateSources();
        Assert.Contains("\"gclib_version\"", source);

        foreach (var prohibited in new[] { "gclib_open", "gclib_close", "GVersion", "gcaps_version", "gclib_command", "gclib_examples" })
        {
            Assert.DoesNotContain(prohibited, source, StringComparison.Ordinal);
        }

        // The vendor module is reachable ONLY by explicit absolute-path load; never by a static
        // import. The adapter P/Invokes kernel32 exclusively.
        Assert.DoesNotContain("[DllImport(\"gclib", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LoadLibraryExW", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeLibrary.Load", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetDllDirectory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddDllDirectory", source, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Committed_Directory_Or_Real_Hash_Appears_In_The_Gate_Sources()
    {
        foreach (var file in GateFiles())
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotMatch(@"[A-Za-z]:[\\/]", text);
            Assert.DoesNotMatch(@"\b[0-9A-Fa-f]{64}\b", text);
        }
    }

    [Fact]
    public void Environment_Variable_Names_Respect_The_Boundary_Scanner()
    {
        var source = ReadGateSources();
        var names = Regex.Matches(source, "WJSS_MOTION_LIB_[A-Z_0-9]+").Select(m => m.Value).Distinct(StringComparer.Ordinal).ToArray();

        var expected = GateEnvironmentVariableNames.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, names.OrderBy(n => n, StringComparer.Ordinal).ToArray());

        foreach (var name in names)
        {
            Assert.DoesNotContain("HOST", name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Adapter_Is_Self_Owned_And_Never_Couples_To_The_Motion_Execution_Side()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "adapters", "motion-library", "Wjss.Adapters.MotionLibrary.csproj"));
        Assert.DoesNotContain("<ProjectReference", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", csproj, StringComparison.Ordinal);

        var source = ReadGateSources();
        Assert.DoesNotContain("MotionAuthorizationGate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionCommandGate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionCommissioningSession", source, StringComparison.Ordinal);

        // The Motion Foundation itself is untouched: the gate stays CLOSED and carries no
        // reference back to this gate.
        var motionDir = Path.Combine(RepoRoot(), "packages", "application", "Motion");
        foreach (var file in Directory.EnumerateFiles(motionDir, "*.cs"))
        {
            Assert.DoesNotContain("MotionLibrary", File.ReadAllText(file), StringComparison.Ordinal);
        }

        var contracts = File.ReadAllText(Path.Combine(RepoRoot(), "packages", "contracts", "MotionCommissioning.cs"));
        Assert.DoesNotContain("MotionLibrary", contracts, StringComparison.Ordinal);
        Assert.Contains("MOTION_COMMISSIONING_GATE_NOT_OPEN", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Runtime_Composition_And_Preview_UI_Carry_No_Probe_Coupling()
    {
        var root = RepoRoot();
        Assert.DoesNotContain(
            "MotionLibrary",
            File.ReadAllText(Path.Combine(root, "apps", "runtime", "Program.cs")),
            StringComparison.Ordinal);

        var previewServer = File.ReadAllText(Path.Combine(root, "apps", "ops-preview", "server.mjs"));
        Assert.DoesNotContain("motion-library", previewServer);
        Assert.DoesNotContain("gclib", previewServer);
        Assert.DoesNotContain("motion-probe", previewServer);

        // No route carries probe-shaped traffic: the only motion route remains the static
        // read-only catalogue.
        Assert.Contains("/api/motion-commissioning/catalogue", previewServer);
    }

    [Fact]
    public void Native_Artefacts_Are_Excluded_From_Git_By_Ignore_Rules()
    {
        var gitignore = File.ReadAllText(Path.Combine(RepoRoot(), ".gitignore"));
        Assert.Contains("adapters/motion-library/**/*.dll", gitignore);
        Assert.Contains("adapters/motion-library/**/*.lib", gitignore);
        Assert.Contains("adapters/motion-library/**/*.h", gitignore);
        Assert.Contains("apps/motion-probe/**/*.dll", gitignore);
    }
}
