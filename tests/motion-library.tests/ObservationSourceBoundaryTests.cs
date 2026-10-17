using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Static boundary coverage of the B2 read-only observed-state surface. These tests pin the
/// PUBLIC-REPOSITORY posture of the new sources without building anything: the four candidate
/// dispositions are recorded as VERIFIED_FOR_B2 in the evidence baseline, the evidence document
/// carries no address and no hash, the foundation document states the closed gate and the C1
/// deferral, the three model sources stay isolated from the native boundary and contain no
/// command text, and the Motion application model keeps no reference back to this surface.
/// </summary>
public sealed class ObservationSourceBoundaryTests
{
    private static readonly string[] ObservationSources =
    {
        "MotionObservedStateVocabulary.cs",
        "MotionObservedState.cs",
        "MotionObservationSession.cs",
    };

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

    private static string ReadObservationSource(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot(), "adapters", "motion-library", fileName));

    private static string EvidenceBaseline() =>
        File.ReadAllText(Path.Combine(
            RepoRoot(), "docs", "evidence", "LEGACY_MOTION_COMMISSIONING_BASELINE.md"));

    [Fact]
    public void The_Evidence_Baseline_Records_Every_Verified_Candidate_And_Its_Interpretation_Limit()
    {
        var lines = EvidenceBaseline().Split('\n');

        foreach (var candidate in MotionObservedStateVocabulary.VerifiedForB2Candidates)
        {
            var row = lines.FirstOrDefault(line =>
                line.StartsWith($"| {candidate} |", StringComparison.Ordinal));

            Assert.NotNull(row);
            Assert.Contains("VERIFIED_FOR_B2", row, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Evidence_Baseline_Has_All_Seven_Sections()
    {
        var document = EvidenceBaseline();

        foreach (var heading in new[] { "## A.", "## B.", "## C.", "## D.", "## E.", "## F.", "## G." })
        {
            Assert.Contains(heading, document, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Evidence_Baseline_Carries_No_Address_And_No_Hash()
    {
        var document = EvidenceBaseline();

        Assert.DoesNotMatch(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", document);
        Assert.DoesNotMatch(@"\b[0-9A-Fa-f]{64}\b", document);
        Assert.DoesNotContain("192.168", document, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"[A-Za-z]:[\\/]", document);
    }

    [Fact]
    public void The_Foundation_Document_States_The_Closed_Gate_And_The_C1_Deferral()
    {
        var document = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "MOTION_OBSERVED_STATE_FOUNDATION.md"));

        Assert.Contains("MOTION_COMMISSIONING_GATE_NOT_OPEN", document, StringComparison.Ordinal);
        Assert.Contains("NOT_CONFIGURED", document, StringComparison.Ordinal);
        Assert.Contains("NOT_COMMISSIONED", document, StringComparison.Ordinal);
        Assert.Contains("B2-H", document, StringComparison.Ordinal);
        Assert.Contains("C1", document, StringComparison.Ordinal);
        Assert.Contains("NOT IMPLEMENTED", document, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", document);
    }

    [Fact]
    public void The_Adapter_Readme_Documents_The_Observed_State_Surface()
    {
        var readme = File.ReadAllText(Path.Combine(RepoRoot(), "adapters", "motion-library", "README.md"));

        Assert.Contains("B2", readme, StringComparison.Ordinal);
        Assert.Contains("MotionObservedStateVocabulary.cs", readme, StringComparison.Ordinal);
        Assert.Contains("MotionObservedState.cs", readme, StringComparison.Ordinal);
        Assert.Contains("MotionObservationSession.cs", readme, StringComparison.Ordinal);
        Assert.Contains("VERIFIED_FOR_B2", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void The_B2_Sources_Are_Isolated_From_The_Native_Boundary()
    {
        foreach (var fileName in ObservationSources)
        {
            var source = ReadObservationSource(fileName);

            foreach (var prohibited in new[]
                     {
                         "MotionLibraryProbeConfiguration",
                         "MotionControllerEndpointConfiguration",
                         "MotionControllerApi",
                         "IControllerKernel",
                         "NativeMethods",
                         "DllImport",
                         "LoadLibrary",
                         "GetProcAddress",
                         "Socket",
                         "TcpClient",
                         "HttpClient",
                         "Environment.",
                         "File.",
                         "Process",
                     })
            {
                Assert.DoesNotContain(prohibited, source, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void The_B2_Sources_Carry_No_Command_Text_Or_Command_Builder()
    {
        var combined = string.Concat(ObservationSources.Select(ReadObservationSource));

        var prohibited = new[]
        {
            "gclib",
            "gc" + "lib_command",
            "G" + "Cmd",
            "M" + "G ",
            "T" + "P ",
            "R" + "P ",
            "@" + "OUT[",
            "T" + "I0",
            "S" + "B ",
            "C" + "B ",
            "H" + "V",
            "H" + "M",
            "A" + "C=",
            "S" + "P=",
            "D" + "C=",
            "P" + "A=",
            "J" + "G=",
            "D" + "E=",
            "D" + "P",
            "B" + "G",
            "S" + "T ",
        };

        foreach (var token in prohibited)
        {
            Assert.DoesNotContain(token, combined, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_B2_Surface_Is_Not_Referenced_By_The_Motion_Application_Model()
    {
        var root = RepoRoot();
        var motionDirectory = Path.Combine(root, "packages", "application", "Motion");

        foreach (var file in Directory.EnumerateFiles(motionDirectory, "*.cs"))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("MotionObserved", text, StringComparison.Ordinal);
            Assert.DoesNotContain("MotionObservation", text, StringComparison.Ordinal);
        }

        var contracts = File.ReadAllText(Path.Combine(root, "packages", "contracts", "MotionCommissioning.cs"));
        Assert.DoesNotContain("MotionObserved", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionObservation", contracts, StringComparison.Ordinal);

        var adapterReadme = File.ReadAllText(Path.Combine(root, "adapters", "motion-library", "README.md"));
        Assert.DoesNotContain("MotionCommandGate", adapterReadme, StringComparison.Ordinal);
    }
}
