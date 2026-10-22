using System.Text.RegularExpressions;
using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Source-boundary coverage of the C1 Stop-only software foundation. These tests read the
/// repository source; they never load a module, never open a handle and never contact a controller.
///
/// They establish five facts. Exactly one production file carries a quoted Stop command text, and
/// that file is the closed template plan. The Stop files name no read or other command literal.
/// No file outside the exact Stop-file set names a Stop type, except the one PR #34 Manual Motion
/// controller owner that reuses this typed Stop session by approved design. The Stop surface is internal only.
/// The Stop carrier resolves its export through the existing approved seam and does not name the
/// native export token.
/// </summary>
public sealed class MotionStopBoundaryTests
{
    private const string TemplatePlanFile = "adapters/motion-library/MotionStopCommandPlan.cs";
    private const string TrialOutcomeFile = "adapters/motion-library/MotionStopTrialOutcome.cs";

    // The single file allowed to name the native command export. Expected value only; never mutated.
    private static readonly string[] ExpectedCommandExportCarrierFiles =
    [
        "adapters/motion-library/MotionControllerCommandKernel.cs",
    ];

    private static readonly string[] StopFiles =
    {
        "adapters/motion-library/MotionStopCommandPlan.cs",
        "adapters/motion-library/MotionStopAcknowledgement.cs",
        "adapters/motion-library/MotionStopDispatchKernel.cs",
        "adapters/motion-library/MotionStopSession.cs",
        "adapters/motion-library/MotionStopOwner.cs",
        "adapters/motion-library/MotionStopTrialInvocation.cs",
        "adapters/motion-library/MotionStopTrialSession.cs",
        "adapters/motion-library/MotionStopTrialOutcome.cs",
        "adapters/motion-library/Wjss.Adapters.MotionLibrary.csproj",
        "apps/motion-stop-trial/Program.cs",
        "apps/motion-stop-trial/Wjss.MotionStopTrial.csproj",
    };

    // PR #34 scoped reuse: the Manual Motion Commissioning controller owner is the ONE additional
    // production file allowed to name a Stop type, because its approved design reuses the existing
    // typed A/B Stop session (and its acknowledgement classifier) instead of adding a second Stop
    // path. It is not a Stop source file: it carries no Stop template and no Stop dispatch of its
    // own, and every other Stop assertion below still excludes it.
    private static readonly string[] StopReusingFiles =
    {
        "adapters/motion-library/ManualMotionControllerOwner.cs",
    };

    private static readonly string[] ProductionTrees = { "adapters", "apps", "packages" };

    private static readonly string[] SourceExtensions = { ".cs", ".csproj", ".mjs", ".js", ".ts", ".json", ".html" };

    // A quoted text that begins with the Stop mnemonic. The quote and the mnemonic must be adjacent, so
    // identifiers and words such as STATUS, STAGE and STOP never match.
    private static readonly Regex RawStopText = new("[\"']ST(?: [A-Za-z]*|[A-D]+)?[\"']", RegexOptions.CultureInvariant);

    // Quoted read, control or motion mnemonics that are not permitted in any Stop source file.
    private static readonly Regex ForeignMnemonicText = new(
        "[\"'](?:TS|SC|PT|GA|GM|MO|MG|TP|RP|TC|AB|JG|BG|SB|CB|DP|DE|HM|HV|AC|SP|DC|PA|PR|AM|MC|SH|TB|TV|OB|OP)(?=[ \"'=\\[\\d])",
        RegexOptions.CultureInvariant);

    // Candidate caller-text parameters across Stop files. A report-only axis label is classified
    // separately; every other match is forbidden, especially on the typed command path.
    private static readonly Regex CallerTextParameter = new(
        "[,(]\\s*string\\??\\s+(text|command|template|suffix|prefix|separator|axis|value|input|raw)\\b",
        RegexOptions.CultureInvariant);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static IEnumerable<string> ProductionSourceFiles(string root)
    {
        foreach (var tree in ProductionTrees)
        {
            var directory = Path.Combine(root, tree);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.Ordinal)
                    || relative.Contains("/obj/", StringComparison.Ordinal)
                    || relative.Contains("node_modules/", StringComparison.Ordinal)
                    || relative.Contains("/dist/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (SourceExtensions.Any(extension => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return relative;
                }
            }
        }
    }

    [Fact]
    public void Quoted_Stop_Command_Text_Exists_Only_In_The_Approved_Template_Plan()
    {
        var root = RepositoryRoot();

        var matches = ProductionSourceFiles(root)
            .Where(path => RawStopText.IsMatch(File.ReadAllText(Path.Combine(root, path))))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { TemplatePlanFile }, matches);
    }

    [Fact]
    public void The_Template_Plan_Holds_Exactly_The_Four_Axis_Templates_And_Nothing_Else()
    {
        var text = ReadRepositoryFile(TemplatePlanFile);

        Assert.Equal(4, Regex.Count(text, "template = \"ST [ABCD]\";", RegexOptions.CultureInvariant));
        Assert.Contains("template = \"ST A\";", text, StringComparison.Ordinal);
        Assert.Contains("template = \"ST B\";", text, StringComparison.Ordinal);
        Assert.Contains("template = \"ST C\";", text, StringComparison.Ordinal);
        Assert.Contains("template = \"ST D\";", text, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Stop_Source_Accepts_Caller_Command_Text_Or_A_Caller_Suffix()
    {
        Assert.Contains(TrialOutcomeFile, StopFiles);

        foreach (var file in StopFiles)
        {
            var text = ReadRepositoryFile(file);
            var matches = CallerTextParameter.Matches(text);

            if (string.Equals(file, TrialOutcomeFile, StringComparison.Ordinal))
            {
                // This one string axis parameter is report-only: it copies an axis label into
                // MotionStopTrialOutcome.Axis and has no route to the command plan or dispatch.
                Assert.Single(matches);

                Assert.Equal("axis", matches[0].Groups[1].Value);

                var factoryStart = text.IndexOf(
                    "internal static MotionStopTrialOutcome RefusedBeforeNative(",
                    StringComparison.Ordinal);
                Assert.True(factoryStart >= 0, file);
                if (factoryStart < 0)
                {
                    continue;
                }

                var factoryEnd = text.IndexOf(
                    "internal static MotionStopTrialOutcome AcceptedPreconditions(",
                    factoryStart,
                    StringComparison.Ordinal);
                Assert.True(factoryEnd > factoryStart, file);
                if (factoryEnd <= factoryStart)
                {
                    continue;
                }

                Assert.InRange(matches[0].Index, factoryStart, factoryEnd - 1);
                var refusalFactory = text[factoryStart..factoryEnd];
                Assert.Contains("string? axis = null,", refusalFactory, StringComparison.Ordinal);
                Assert.Contains("Axis = axis,", refusalFactory, StringComparison.Ordinal);
                Assert.Contains("internal string? Axis { get; set; }", text, StringComparison.Ordinal);
                Assert.DoesNotContain("MotionStopCommandPlan", text, StringComparison.Ordinal);
                Assert.DoesNotContain("MotionStopSession", text, StringComparison.Ordinal);
                Assert.DoesNotContain("MotionStopDispatchKernel", text, StringComparison.Ordinal);
                Assert.DoesNotContain("IStopDispatchKernel", text, StringComparison.Ordinal);
                Assert.DoesNotContain("MotionControllerCommandApi", text, StringComparison.Ordinal);
                continue;
            }

            Assert.True(matches.Count == 0, file);
        }
    }

    [Fact]
    public void Only_The_Closed_Typed_Axis_Plan_Can_Reach_The_Native_Stop_Buffer()
    {
        var invocation = ReadRepositoryFile("adapters/motion-library/MotionStopTrialInvocation.cs").ReplaceLineEndings("\n");
        var trialSession = ReadRepositoryFile("adapters/motion-library/MotionStopTrialSession.cs");
        var plan = ReadRepositoryFile(TemplatePlanFile).ReplaceLineEndings("\n");
        var session = ReadRepositoryFile("adapters/motion-library/MotionStopSession.cs");
        var dispatch = ReadRepositoryFile("adapters/motion-library/MotionStopDispatchKernel.cs");

        // Raw process arguments are accepted only as the exact mode plus one exact A/B token,
        // then converted to MotionStopAxis before entering the trial session.
        Assert.Contains("TryParseInvocation(arguments, out var axis)", invocation, StringComparison.Ordinal);
        Assert.Contains("internal static bool TryParseInvocation(string[]? arguments, out MotionStopAxis axis)", invocation, StringComparison.Ordinal);
        Assert.Contains("arguments is null || arguments.Length != 2", invocation, StringComparison.Ordinal);
        Assert.Contains("!string.Equals(arguments[0], MotionStopTrialVocabulary.ModeToken, StringComparison.Ordinal)", invocation, StringComparison.Ordinal);
        Assert.Contains("string.Equals(arguments[1], \"A\", StringComparison.Ordinal)", invocation, StringComparison.Ordinal);
        Assert.Contains("string.Equals(arguments[1], \"B\", StringComparison.Ordinal)", invocation, StringComparison.Ordinal);
        Assert.Contains("axis: AxisToken(axis)", invocation, StringComparison.Ordinal);
        Assert.Contains("            endpointConfiguration,\n            axis,\n            () => _gateAProbe(nativeConfiguration),", invocation, StringComparison.Ordinal);
        Assert.Contains("MotionStopAxis axis,", trialSession, StringComparison.Ordinal);
        Assert.Contains("private readonly MotionStopAxis _axis;", trialSession, StringComparison.Ordinal);
        Assert.Contains("owner.Session.Stop(_axis)", trialSession, StringComparison.Ordinal);

        // Every command-capable boundary takes the closed enum; only the plan returns a template,
        // and the native byte buffer is built from that returned value.
        Assert.Contains("internal static bool TryGetTemplate(MotionStopAxis axis, out string template)", plan, StringComparison.Ordinal);
        Assert.Contains("case MotionStopAxis.A:\n                template = \"ST A\";", plan, StringComparison.Ordinal);
        Assert.Contains("case MotionStopAxis.B:\n                template = \"ST B\";", plan, StringComparison.Ordinal);
        Assert.Contains("case MotionStopAxis.C:\n                template = \"ST C\";", plan, StringComparison.Ordinal);
        Assert.Contains("case MotionStopAxis.D:\n                template = \"ST D\";", plan, StringComparison.Ordinal);
        Assert.Contains("internal MotionStopOutcome Stop(MotionStopAxis axis)", session, StringComparison.Ordinal);
        Assert.Contains("reply = _kernel.Dispatch(axis);", session, StringComparison.Ordinal);
        Assert.Contains("MotionStopNativeReply Dispatch(MotionStopAxis axis);", dispatch, StringComparison.Ordinal);
        Assert.Contains("if (!MotionStopCommandPlan.TryGetTemplate(axis, out var template))", dispatch, StringComparison.Ordinal);
        Assert.Contains("var commandBuffer = CreateAsciiBuffer(template);", dispatch, StringComparison.Ordinal);
        Assert.Contains("private static byte[] CreateAsciiBuffer(string planTemplate)", dispatch, StringComparison.Ordinal);
        Assert.Contains("var export = Marshal.GetDelegateForFunctionPointer<StopCommandExport>(_export);", dispatch, StringComparison.Ordinal);
        Assert.Contains("var result = export(_handle, commandBuffer, replyBuffer, (UIntPtr)replyBuffer.Length);", dispatch, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(dispatch, @"CreateAsciiBuffer\("));
        Assert.Single(Regex.Matches(dispatch, @"\bexport\("));
        Assert.Single(Regex.Matches(dispatch, @"CreateAsciiBuffer\(template\)"));
        Assert.Equal(2, Regex.Count(dispatch, @"(?m)^\s*(?:public\s+)?MotionStopNativeReply Dispatch\("));
    }

    [Fact]
    public void No_Stop_Source_Names_A_Read_Or_Other_Command_Literal()
    {
        foreach (var file in StopFiles)
        {
            var text = ReadRepositoryFile(file);
            Assert.False(ForeignMnemonicText.IsMatch(text), file);
        }
    }

    [Fact]
    public void The_B2_Plan_Still_Carries_Exactly_Sixteen_Read_Literals_And_No_Stop_Literal()
    {
        Assert.Equal(16, MotionObservationCommandPlan.Templates.Count);
        Assert.DoesNotContain(
            MotionObservationCommandPlan.Templates.Values,
            template => template.StartsWith("ST", StringComparison.Ordinal));
    }

    [Fact]
    public void Only_The_Stop_Files_Name_A_Stop_Type_And_No_Other_Source_Does()
    {
        var root = RepositoryRoot();
        var stopTypes = new[] { "MotionStop", "StopDispatch", "StopAcknowledgement", "BoundStopDispatchKernel" };

        foreach (var relative in ProductionSourceFiles(root))
        {
            if (StopFiles.Contains(relative, StringComparer.Ordinal)
                || StopReusingFiles.Contains(relative, StringComparer.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(Path.Combine(root, relative));
            foreach (var token in stopTypes)
            {
                Assert.DoesNotContain(token, text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void The_Stop_Surface_Declares_No_Public_Type()
    {
        // Public members of the internal dispatch carrier exist only to implement the internal
        // IStopDispatchKernel interface. Only the type declarations are required to stay non-public.
        foreach (var file in StopFiles)
        {
            var text = ReadRepositoryFile(file);
            Assert.False(
                Regex.IsMatch(text, "(?m)^\\s*public\\s+(sealed\\s+|static\\s+|abstract\\s+)*(class|struct|enum|interface|record)\\b"),
                file);
        }
    }

    [Fact]
    public void The_Stop_Carrier_And_Owner_Do_Not_Name_The_Native_Command_Export_Token()
    {
        foreach (var file in StopFiles)
        {
            var text = ReadRepositoryFile(file);
            Assert.DoesNotContain("gclib_command", text, StringComparison.Ordinal);
        }

        var owner = ReadRepositoryFile("adapters/motion-library/MotionStopOwner.cs");
        Assert.Contains("MotionControllerCommandApi.ResolveCommandExport", owner, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Native_Command_Export_Token_Remains_Only_In_The_Approved_Carrier()
    {
        var root = RepositoryRoot();
        var matches = ProductionSourceFiles(root)
            .Where(path => File.ReadAllText(Path.Combine(root, path)).Contains("gclib_command", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedCommandExportCarrierFiles, matches);
    }

    [Fact]
    public void The_Gate_Stays_Closed_And_The_Commissioning_Session_Stays_Refusing()
    {
        var gate = ReadRepositoryFile("packages/application/Motion/MotionCommandGate.cs");
        var session = ReadRepositoryFile("packages/application/Motion/MotionCommissioningSession.cs");

        Assert.Contains("public static bool IsClosed => true;", gate, StringComparison.Ordinal);
        Assert.Contains("EvaluateForAllAxes(MotionVocabulary.IntentStop)", session, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Adapter_README_Names_The_Stop_Path_Files()
    {
        var readme = ReadRepositoryFile("adapters/motion-library/README.md");

        foreach (var file in StopFiles)
        {
            Assert.Contains(Path.GetFileName(file), readme, StringComparison.Ordinal);
        }
    }
}
