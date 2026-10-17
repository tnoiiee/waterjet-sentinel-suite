using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Negative-space coverage of the ONE scoped exception to the raw-command prohibition.
///
/// The global rule is unchanged: no gate source outside the internal B2 native boundary may name
/// a raw command API. This suite proves the exception cannot be widened — it names one exact
/// repository-relative path and one exact token, rejects case variants, siblings, nested paths,
/// wildcards, build-output paths and every other token, and leaves the Runtime composition, the
/// contracts, the Preview UI and the B1 harness untouched.
/// </summary>
public sealed class B2CommandBoundaryExceptionTests
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

    private static string ApprovedFile(string root) =>
        Path.Combine(root, "adapters", "motion-library", "MotionControllerCommandKernel.cs");

    private static readonly string[] SearchExtensions = { ".cs", ".csproj", ".md", ".json" };

    private static IEnumerable<string> GateFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            if (relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                || relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (SearchExtensions.Any(extension =>
                    file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            {
                yield return file;
            }
        }
    }

    [Fact]
    public void The_Exception_Covers_Exactly_One_File_And_One_Token()
    {
        Assert.True(B2CommandTokenException.IsApprovedPair(
            B2CommandTokenException.ApprovedRelativePath,
            B2CommandTokenException.ApprovedToken));

        var root = RepoRoot();
        Assert.True(B2CommandTokenException.Covers(
            root,
            ApprovedFile(root),
            B2CommandTokenException.ApprovedToken));

        Assert.True(File.Exists(ApprovedFile(root)));
    }

    [Fact]
    public void A_Case_Variant_A_Sibling_A_Nested_Path_Or_A_Wildcard_Is_Not_Approved()
    {
        var root = RepoRoot();

        var rejectedPaths = new[]
        {
            "adapters/motion-library/motioncontrollercommandkernel.cs",
            "adapters/motion-library/MotionControllerCommandKernel.CS",
            "adapters/MotionControllerCommandKernel.cs",
            "apps/motion-identity/MotionControllerCommandKernel.cs",
            "adapters/motion-library/MotionControllerCommandKernel.cs.bak",
            "adapters/motion-library/binary/object-store/MotionControllerCommandKernel.cs",
            "adapters/motion-library/*/MotionControllerCommandKernel.cs",
            "adapters/motion-library/MotionControllerCommandKernel*.cs",
            "MotionControllerCommandKernel.cs",
            "./adapters/motion-library/MotionControllerCommandKernel.cs",
            "adapters/motion-library/../motion-library/MotionControllerCommandKernel.cs",
            "../adapters/motion-library/MotionControllerCommandKernel.cs",
        };

        foreach (var path in rejectedPaths)
        {
            Assert.False(
                B2CommandTokenException.IsApprovedPair(path, B2CommandTokenException.ApprovedToken),
                path);
        }

        Assert.False(B2CommandTokenException.Covers(
            root,
            Path.Combine(root, "apps", "motion-identity", "MotionControllerCommandKernel.cs"),
            B2CommandTokenException.ApprovedToken));
    }

    [Fact]
    public void Only_The_Approved_Token_Is_Exempt_In_The_Approved_File()
    {
        var root = RepoRoot();

        foreach (var token in new[]
                 {
                     "GCLIB_COMMAND",
                     "gclib_command ",
                     " gclib_command",
                     "gclib_commandX",
                     "gclib_commands",
                     "GCmd",
                     "GVersion",
                     "gclib_open",
                     "gclib_close",
                     "gclib_examples",
                     "gclib_gcaps_version",
                     string.Empty,
                 })
        {
            Assert.False(
                B2CommandTokenException.IsApprovedPair(B2CommandTokenException.ApprovedRelativePath, token),
                token);
            Assert.False(
                B2CommandTokenException.Covers(root, ApprovedFile(root), token),
                token);
        }
    }

    [Fact]
    public void A_Build_Output_Path_Is_Never_Approved()
    {
        var root = RepoRoot();

        var generated = Path.Combine(
            root,
            "adapters",
            "motion-library",
            "obj",
            "Release",
            "net10.0",
            "MotionControllerCommandKernel.cs");

        Assert.False(B2CommandTokenException.Covers(
            root, generated, B2CommandTokenException.ApprovedToken));
    }

    [Fact]
    public void Only_The_Internal_Native_Boundary_Names_The_Token_Anywhere_In_The_Gate_Tree()
    {
        var root = RepoRoot();
        var matches = new List<string>();

        foreach (var tree in new[] { "adapters", "apps", "packages" })
        {
            foreach (var file in GateFiles(Path.Combine(root, tree)))
            {
                if (File.ReadAllText(file).Contains(
                        B2CommandTokenException.ApprovedToken, StringComparison.Ordinal))
                {
                    matches.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
                }
            }
        }

        var match = Assert.Single(matches);
        Assert.Equal(B2CommandTokenException.ApprovedRelativePath, match);
    }

    [Fact]
    public void The_Approved_File_Contains_No_Write_Control_Or_Motion_Request()
    {
        var text = File.ReadAllText(ApprovedFile(RepoRoot()));

        foreach (var prohibited in new[]
                 {
                     "ST ", "JG", "BG", "HV", "HM", "FE", "FI", "AC=", "SP=", "DC=",
                     "PA=", "PR=", "DP", "DE=", "SB ", "CB ", "OB ", "OP ", "TI0", "@OUT[",
                 })
        {
            Assert.DoesNotContain(prohibited, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Approved_File_Carries_No_Address_No_Hash_And_No_Absolute_Path()
    {
        var text = File.ReadAllText(ApprovedFile(RepoRoot()));

        Assert.DoesNotMatch(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", text);
        Assert.DoesNotMatch(@"\b[0-9A-Fa-f]{64}\b", text);
        Assert.DoesNotMatch(@"[A-Za-z]:[\\/]", text);
        Assert.DoesNotContain("192.168", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Runtime_Contracts_Ui_And_B1_Harness_Carry_No_Command_Token()
    {
        var root = RepoRoot();

        var files = new[]
        {
            Path.Combine(root, "apps", "runtime", "Program.cs"),
            Path.Combine(root, "packages", "contracts", "MotionCommissioning.cs"),
            Path.Combine(root, "apps", "ops-preview", "server.mjs"),
            Path.Combine(root, "apps", "motion-identity", "Program.cs"),
            Path.Combine(root, "apps", "motion-probe", "Program.cs"),
            Path.Combine(root, "adapters", "motion-library", "MotionLibraryProbeVocabulary.cs"),
            Path.Combine(root, "adapters", "motion-library", "NativeKernel.cs"),
            Path.Combine(root, "adapters", "motion-library", "MotionControllerKernel.cs"),
            Path.Combine(root, "adapters", "motion-library", "MotionObservationSession.cs"),
            Path.Combine(root, "adapters", "motion-library", "MotionObservedState.cs"),
            Path.Combine(root, "adapters", "motion-library", "MotionObservedStateVocabulary.cs"),
            Path.Combine(root, "adapters", "motion-library", "README.md"),
        };

        foreach (var file in files)
        {
            Assert.True(File.Exists(file), file);
            Assert.DoesNotContain(
                B2CommandTokenException.ApprovedToken,
                File.ReadAllText(file),
                StringComparison.Ordinal);
        }
    }
}
