using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Source-only boundaries for the new console composition: exact invocation, consent confinement,
/// approved lifecycle calls, redacted output and isolation from application/production surfaces.
/// These tests read repository files only and never load a native module or contact a Controller.
/// </summary>
public sealed class MotionStopTrialBoundaryTests
{
    private const string AppProject = "apps/motion-stop-trial/Wjss.MotionStopTrial.csproj";
    private const string ProgramSource = "apps/motion-stop-trial/Program.cs";
    private const string InvocationSource = "adapters/motion-library/MotionStopTrialInvocation.cs";
    private const string SessionSource = "adapters/motion-library/MotionStopTrialSession.cs";
    private const string VocabularySource = "adapters/motion-library/MotionStopTrialOutcome.cs";

    private static readonly string[] ExpectedMotionLibraryProjectReferences =
    {
        "../../adapters/motion-library/Wjss.Adapters.MotionLibrary.csproj",
    };

    private static readonly string[] InvocationSearchRoots =
    {
        "adapters",
        "apps",
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static int CountOccurrences(string text, string token) =>
        text.Split(token, StringSplitOptions.None).Length - 1;

    private static string NormalizeWords(string text) =>
        Regex.Replace(text, @"[^\p{L}\p{N}]+", " ").Trim();

    private static bool IsGeneratedOrDependencyFile(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return relative.Split('/').Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "node_modules", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "dist", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_Console_Project_References_Only_The_Motion_Library_Adapter()
    {
        var path = Path.Combine(RepositoryRoot(), AppProject.Replace('/', Path.DirectorySeparatorChar));
        var project = XDocument.Load(path);
        var projectReferences = project.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .ToArray();

        Assert.Equal("Exe", project.Descendants("OutputType").Single().Value);
        Assert.Equal("net10.0", project.Descendants("TargetFramework").Single().Value);
        Assert.Equal(ExpectedMotionLibraryProjectReferences, projectReferences);
        Assert.DoesNotContain(project.Descendants(), element =>
            string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal));
    }

    [Fact]
    public void Runtime_Kiosk_Preview_And_Application_Motion_Gate_Do_Not_Reference_The_Harness()
    {
        var root = RepositoryRoot();
        var applicationTrees = new[]
        {
            "apps/runtime",
            "apps/kiosk",
            "apps/ops-preview",
            "apps/ui",
            "packages/application",
        };
        var applicationSourceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".csproj", ".ts", ".tsx", ".js", ".jsx", ".json", ".html",
        };
        foreach (var tree in applicationTrees)
        {
            var directory = Path.Combine(root, tree);
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                         .Where(file => !IsGeneratedOrDependencyFile(root, file)
                                        && applicationSourceExtensions.Contains(Path.GetExtension(file) ?? string.Empty)))
            {
                var text = File.ReadAllText(file);
                Assert.DoesNotContain("MotionStopTrial", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Wjss.MotionStopTrial", text, StringComparison.Ordinal);
            }
        }

        var gate = ReadRepositoryFile("packages/application/Motion/MotionCommandGate.cs");
        Assert.Contains("public static bool IsClosed => true;", gate, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Trial_Has_One_Production_Entrypoint_And_It_Is_The_Isolated_Console()
    {
        var root = RepositoryRoot();
        var appSourceFiles = Directory.EnumerateFiles(
                Path.Combine(root, "apps/motion-stop-trial"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrDependencyFile(root, path))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { ProgramSource }, appSourceFiles);

        var invocationSites = InvocationSearchRoots
            .SelectMany(tree => Directory.EnumerateFiles(
                Path.Combine(root, tree),
                "*.cs",
                SearchOption.AllDirectories))
            .Where(path => !IsGeneratedOrDependencyFile(root, path)
                           && File.ReadAllText(path).Contains(
                               "new MotionStopTrialInvocation",
                               StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { ProgramSource }, invocationSites);
    }

    [Fact]
    public void The_Consent_Variable_Name_Is_Declared_In_One_Production_Source_File_Only()
    {
        var root = RepositoryRoot();
        var productionTrees = new[] { "adapters", "apps" };
        var matches = productionTrees
            .SelectMany(tree => Directory.EnumerateFiles(
                Path.Combine(root, tree),
                "*.cs",
                SearchOption.AllDirectories))
            .Where(path => !IsGeneratedOrDependencyFile(root, path)
                           && File.ReadAllText(path).Contains(
                               "WJSS_MOTION_STOP_TRIAL_CONSENT",
                               StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { VocabularySource }, matches);
    }

    [Fact]
    public void Consent_Is_Consumed_Exactly_And_Before_Mode_Config_Endpoint_Or_Native_Work()
    {
        var invocation = ReadRepositoryFile(InvocationSource);
        var consume = invocation.IndexOf("var consentCode = ConsumeConsent();", StringComparison.Ordinal);
        var parse = invocation.IndexOf("TryParseInvocation(arguments", StringComparison.Ordinal);
        var nativeConfiguration = invocation.IndexOf("MotionLibraryConfigurationResolver.TryResolve(", StringComparison.Ordinal);
        var endpoint = invocation.IndexOf("MotionControllerEndpointResolver.TryResolve(", StringComparison.Ordinal);
        var session = invocation.IndexOf("new MotionStopTrialSession(", StringComparison.Ordinal);

        Assert.True(consume >= 0 && consume < parse);
        Assert.True(parse < nativeConfiguration && nativeConfiguration < endpoint && endpoint < session);
        Assert.Contains("_removeEnvironment(MotionStopTrialVocabulary.ConsentVariable);", invocation, StringComparison.Ordinal);
        Assert.Contains("StringComparison.Ordinal", invocation, StringComparison.Ordinal);
        Assert.Contains("MotionStopTrialVocabulary.ExactConsentValue", invocation, StringComparison.Ordinal);
        Assert.DoesNotContain("EnvironmentVariableTarget.User", invocation, StringComparison.Ordinal);
        Assert.DoesNotContain("EnvironmentVariableTarget.Machine", invocation, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Session_Has_One_Open_One_Typed_Stop_Call_And_No_Identity_Reads_Or_Confirmation_Readback()
    {
        var session = ReadRepositoryFile(SessionSource);

        Assert.Equal(1, CountOccurrences(session, "_gateAProbe();"));
        Assert.Equal(1, CountOccurrences(session, "controllerKernel.InvokeOpen("));
        Assert.Equal(1, CountOccurrences(session, "owner.Session.Stop(_axis)"));
        Assert.Equal(1, CountOccurrences(session, "owner.Close()"));
        Assert.DoesNotContain("InvokeConnectionType", session, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeRevisionInformation", session, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeSerialNumber", session, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionObservationSession", session, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionObservationTrialSession", session, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAllAxes", session, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionCommandGate", session, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(session, "MotionControllerApi.ForceDirectModeExportName"));
        Assert.Equal(1, CountOccurrences(session, "MotionControllerApi.OpenExportName"));
        Assert.Equal(1, CountOccurrences(session, "MotionControllerApi.CloseExportName"));
    }

    [Fact]
    public void The_Single_Open_Address_Is_Built_From_The_Existing_Opaque_Endpoint_Boundary()
    {
        var session = ReadRepositoryFile(SessionSource);

        Assert.Equal(1, CountOccurrences(session, "MotionControllerApi.BuildOpenAddress(_endpointConfiguration)"));
        Assert.Contains("MotionControllerEndpointResolver.TryResolve", ReadRepositoryFile(InvocationSource), StringComparison.Ordinal);
        Assert.DoesNotContain("IPAddress.TryParse", session, StringComparison.Ordinal);
        Assert.DoesNotContain("Dns.GetHost", session, StringComparison.Ordinal);
        Assert.DoesNotContain("Ping", session, StringComparison.Ordinal);
    }

    [Fact]
    public void The_App_Program_Has_No_Read_Surface_Interactive_Input_File_Or_Network_Path()
    {
        var program = ReadRepositoryFile(ProgramSource);

        Assert.Contains("new MotionStopTrialInvocation().Run(args)", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionControllerIdentitySession", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionObservationTrialSession", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionObservationSession", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionObservationCommandPlan", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionControllerApi", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionStopTrialSession", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionStopOwner", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionStopCommandPlan", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionStopSession", program, StringComparison.Ordinal);
        Assert.DoesNotContain("IControllerKernel", program, StringComparison.Ordinal);
        Assert.DoesNotContain("IStopDispatchKernel", program, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsControllerKernel", program, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeMethods", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionLibraryConfigurationResolver", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionControllerEndpointConfiguration", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionLibraryProbeConfiguration", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionLibraryProbeRunner", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MotionCommandGate", program, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAllAxes", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.ReadLine", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.In", program, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", program, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Socket", program, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Console_Projects_Only_Redacted_Canonical_Fields()
    {
        var program = ReadRepositoryFile(ProgramSource);

        Assert.Contains("preconditionCode = outcome.PreconditionCode", program, StringComparison.Ordinal);
        Assert.Contains("consentCode = outcome.ConsentCode", program, StringComparison.Ordinal);
        Assert.Contains("endpointCode = outcome.EndpointCode", program, StringComparison.Ordinal);
        Assert.Contains("nativeConfigurationCode = outcome.NativeConfigurationCode", program, StringComparison.Ordinal);
        Assert.Contains("nativeProofCode = outcome.NativeProofCode", program, StringComparison.Ordinal);
        Assert.Contains("stopConfirmed = outcome.StopConfirmed", program, StringComparison.Ordinal);
        Assert.Contains("stopDispatched = outcome.StopDispatched", program, StringComparison.Ordinal);
        Assert.DoesNotContain("EndpointValue", program, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeDirectory", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ExpectedPrimaryHash", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ExpectedCompanionHash", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ExpectedVersion", program, StringComparison.Ordinal);
        Assert.DoesNotContain("LibraryVersion", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Detail", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplyBuffer", program, StringComparison.Ordinal);
        Assert.DoesNotContain("safe =", program, StringComparison.Ordinal);
        Assert.DoesNotContain("stopped =", program, StringComparison.Ordinal);
        Assert.DoesNotContain("commissioned =", program, StringComparison.Ordinal);
        Assert.DoesNotContain("confirmedStop =", program, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Outcome_Fixes_Stop_Confirmation_False_And_Cleanup_Failure_Is_Separate()
    {
        var outcome = ReadRepositoryFile(VocabularySource);

        Assert.Contains("private MotionStopTrialOutcome()", outcome, StringComparison.Ordinal);
        Assert.Contains("StopConfirmed = false;", outcome, StringComparison.Ordinal);
        Assert.Contains("internal bool StopConfirmed { get; }", outcome, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(outcome, "StopConfirmed ="));
        Assert.DoesNotContain("StopConfirmed = true", outcome, StringComparison.Ordinal);
        Assert.DoesNotContain("bool stopConfirmed", outcome, StringComparison.Ordinal);
        Assert.DoesNotContain("StopConfirmed { get; set;", outcome, StringComparison.Ordinal);
        Assert.Contains("internal string CleanupFailureCode", outcome, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool StopConfirmed", outcome, StringComparison.Ordinal);
        Assert.DoesNotContain("Safe => true", outcome, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopped => true", outcome, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Runbook_Separates_Software_Validation_From_The_Post_Merge_Scope_Gate()
    {
        var runbook = ReadRepositoryFile("docs/MOTION_STOP_TRIAL_RUNBOOK.md");
        var words = NormalizeWords(runbook);

        // Normalize Markdown punctuation and hyphenation so these checks pin the safety meaning,
        // not whether the runbook spells the term as "fake-kernel", "fake kernel", or a phrase.
        Assert.Contains("software only source foundation", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not hardware authorization", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no controller contact or real stop dispatch was performed while authoring this change", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("owner local software validation fake source tests only", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fake gate a results fake controller kernels fake stop dispatch kernels and source boundary reads only", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("it does not load native modules open a controller probe a network or issue a real command", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a separate post merge owner scope gate is required before any real controller invocation", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("only after merge may a separate scope gate authorize the actual no load wsb1 trial", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stopconfirmed is permanently false dispatch is not a confirmed stop stopped safe state or commissioned result no confirmation readback occurs", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0 one selected stop dispatch completed and cleanup is complete no stop is confirmed", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 a precondition lifecycle refusal occurred or cleanup could not be proven complete", words, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("4 the invocation is not exactly the fixed mode plus one uppercase a b token", words, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("GCLIB_2_6_0_API_USAGE_BASELINE.md", runbook, StringComparison.Ordinal);
    }
}
