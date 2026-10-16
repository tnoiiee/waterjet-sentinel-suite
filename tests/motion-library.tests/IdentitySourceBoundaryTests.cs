using System.Text.RegularExpressions;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Static source and asset boundary coverage of the B1 one-controller identity surface. These
/// tests pin the PUBLIC-REPOSITORY posture of the new sources without building anything: the
/// complete export allow-list and not one export more; no raw-command, discovery, subscription or
/// gcaps API name anywhere in the gate sources; no vendor import-table binding; no committed
/// endpoint, directory, real hash or serial; the single endpoint variable name free of the S6
/// host rule; no coupling to the Motion execution side, the Runtime composition or the Preview
/// UI; and the native-artefact ignore rules in place. (S1–S9 are separately machine-checked by
/// tools/boundary-scan.)
/// </summary>
public sealed class IdentitySourceBoundaryTests
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

    private static bool IsGateSourceFile(string root, string file)
    {
        if (ContainsBuildOutputDirectory(Path.GetRelativePath(root, file)))
        {
            return false;
        }

        return file.EndsWith(".cs", StringComparison.Ordinal)
            || file.EndsWith(".csproj", StringComparison.Ordinal)
            || file.EndsWith(".md", StringComparison.Ordinal);
    }

    private static bool ContainsBuildOutputDirectory(string relativePath)
    {
        var segments = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (string.Equals(segments[index], "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segments[index], "obj", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> IdentityGateFiles()
    {
        var root = RepoRoot();
        var files = new List<string>();
        foreach (var segments in new[]
                 {
                     new[] { "adapters", "motion-library" },
                     new[] { "apps", "motion-identity" },
                 })
        {
            var directory = Path.Combine(root, segments[0], segments[1]);
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                if (IsGateSourceFile(root, file))
                {
                    files.Add(file);
                }
            }
        }

        Assert.NotEmpty(files);
        return files;
    }

    [Fact]
    public void Gate_Source_Filter_Excludes_Build_Output_By_Exact_Path_Segment()
    {
        var root = RepoRoot();
        var authoredSource = Path.Combine(root, "adapters", "motion-library", "MotionControllerIdentitySession.cs");
        Assert.True(IsGateSourceFile(root, authoredSource));
        Assert.Contains(authoredSource, IdentityGateFiles());

        var generatedObjAssemblyInfo = Path.Combine(
            root, "adapters", "motion-library", "obj", "Release", "net10.0", "Wjss.Adapters.MotionLibrary.AssemblyInfo.cs");
        var generatedBinSource = Path.Combine(
            root, "apps", "motion-identity", "bin", "Release", "net10.0", "GeneratedAssemblyInfo.cs");
        Assert.False(IsGateSourceFile(root, generatedObjAssemblyInfo));
        Assert.False(IsGateSourceFile(root, generatedBinSource));
        Assert.True(ContainsBuildOutputDirectory("adapters/motion-library/obj/Release/net10.0/AssemblyInfo.cs"));
        Assert.True(ContainsBuildOutputDirectory("apps\\motion-identity\\bin\\Release\\net10.0\\AssemblyInfo.cs"));

        var lookalikeSegments = Path.Combine(root, "adapters", "motion-library", "binary", "object-store", "Identity_bin.cs");
        var lookalikeFileName = Path.Combine(root, "adapters", "motion-library", "source", "obj.cs");
        Assert.True(IsGateSourceFile(root, lookalikeSegments));
        Assert.True(IsGateSourceFile(root, lookalikeFileName));
        Assert.True(IsGateSourceFile(root, Path.Combine(root, "adapters", "motion-library", "README.md")));
        Assert.True(IsGateSourceFile(root, Path.Combine(root, "adapters", "motion-library", "Gate.csproj")));
        Assert.False(IsGateSourceFile(root, Path.Combine(root, "adapters", "motion-library", "notes.txt")));

        Assert.All(
            IdentityGateFiles(),
            file => Assert.False(ContainsBuildOutputDirectory(Path.GetRelativePath(root, file))));
    }

    private static string ReadIdentityGateSources()
    {
        var combined = string.Empty;
        foreach (var file in IdentityGateFiles())
        {
            combined += File.ReadAllText(file) + "\n";
        }

        return combined;
    }

    [Fact]
    public void The_B1_Export_Allow_List_Is_Exact()
    {
        var source = ReadIdentityGateSources();

        foreach (var export in new[]
                 {
                     "gclib_force_gcaps",
                     "gclib_open",
                     "gclib_close",
                     "gclib_connection_type",
                     "gclib_revision_information",
                     "gclib_serial_number",
                 })
        {
            Assert.Contains($"\"{export}\"", source);
        }

        Assert.Contains("\"gclib_version\"", File.ReadAllText(Path.Combine(
            RepoRoot(), "adapters", "motion-library", "MotionLibraryProbeVocabulary.cs")));
    }

    [Fact]
    public void No_Raw_Command_Discovery_Subscription_Or_Gcaps_Version_Api_Is_Named()
    {
        var source = ReadIdentityGateSources();

        foreach (var prohibited in new[]
                 {
                     "gclib_command",
                     "gclib_addresses",
                     "gclib_ip_requests",
                     "gclib_assign_ip",
                     "gclib_set_baud_rate",
                     "gclib_data_record",
                     "gclib_set_data_records",
                     "gclib_subscribe_data_records",
                     "gclib_set_interrupts",
                     "gclib_list_servers",
                     "gclib_set_server",
                     "gclib_program",
                     "gclib_set_array",
                     "gclib_set_firmware",
                     "gclib_gcaps_version",
                     "gclib_examples",
                     "GVersion",
                     "GCmd",
                 })
        {
            Assert.DoesNotContain(prohibited, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Vendor_Module_Is_Reachable_Only_Through_The_Restricted_Loader()
    {
        var source = ReadIdentityGateSources();
        Assert.DoesNotContain("[DllImport(\"gclib", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LoadLibraryExW", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeLibrary.Load", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetDllDirectory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddDllDirectory", source, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Committed_Endpoint_Directory_Real_Hash_Or_Serial_Appears_In_The_Identity_Sources()
    {
        foreach (var file in IdentityGateFiles())
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotMatch(@"[A-Za-z]:[\\/]", text);
            Assert.DoesNotMatch(@"\b[0-9A-Fa-f]{64}\b", text);
            Assert.DoesNotMatch(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", text);
        }
    }

    [Fact]
    public void The_Single_Endpoint_Variable_Name_Respects_The_Boundary_Scanner()
    {
        var source = ReadIdentityGateSources();
        var names = Regex.Matches(source, "WJSS_MOTION_CONTROLLER_[A-Z_0-9]+")
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Single(names);
        Assert.Equal("WJSS_MOTION_CONTROLLER_01_ENDPOINT", names[0]);
        foreach (var name in names)
        {
            Assert.DoesNotContain("HOST", name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Harness_Is_Argument_Free_Redacted_And_Fail_Closed()
    {
        var harness = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "motion-identity", "Program.cs"));
        Assert.Contains("INVALID_INVOCATION", harness, StringComparison.Ordinal);
        Assert.Contains("return 4;", harness, StringComparison.Ordinal);
        Assert.Contains("return 2;", harness, StringComparison.Ordinal);
        Assert.Contains("return 0;", harness, StringComparison.Ordinal);
        Assert.DoesNotContain("gclib_command", harness, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", harness);
    }

    [Fact]
    public void The_Identity_Surface_Never_Couples_To_The_Motion_Execution_Side_Or_Preview()
    {
        var source = ReadIdentityGateSources();
        Assert.DoesNotContain("MotionAuthorizationGate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionCommandGate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionCommissioningSession", source, StringComparison.Ordinal);

        var previewServer = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "ops-preview", "server.mjs"));
        Assert.DoesNotContain("motion-identity", previewServer);
        Assert.DoesNotContain("Wjss.MotionIdentity", previewServer);
        Assert.DoesNotContain("gclib", previewServer);
    }

    [Fact]
    public void The_Motion_Gate_Stays_Closed_And_Candidate_Values_Stay_Uncommissioned()
    {
        var contracts = File.ReadAllText(Path.Combine(RepoRoot(), "packages", "contracts", "MotionCommissioning.cs"));
        Assert.Contains("MOTION_COMMISSIONING_GATE_NOT_OPEN", contracts, StringComparison.Ordinal);
        Assert.Contains("\"NOT_CONFIGURED\"", contracts, StringComparison.Ordinal);
        Assert.Contains("\"NOT_COMMISSIONED\"", contracts, StringComparison.Ordinal);

        var gate = File.ReadAllText(Path.Combine(RepoRoot(), "packages", "application", "Motion", "MotionCommandGate.cs"));
        Assert.Contains("IsClosed => true", gate, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Artefacts_Are_Excluded_From_Git_By_Ignore_Rules()
    {
        var gitignore = File.ReadAllText(Path.Combine(RepoRoot(), ".gitignore"));
        Assert.Contains("adapters/motion-library/**/*.dll", gitignore);
        Assert.Contains("apps/motion-probe/**/*.dll", gitignore);
        Assert.Contains("apps/motion-identity/**/*.dll", gitignore);
    }
}
