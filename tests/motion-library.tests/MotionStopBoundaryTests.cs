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
/// No file outside the Stop set names a Stop type. The Stop surface is internal only. The Stop
/// carrier resolves its export through the existing approved seam and does not name the native
/// export token.
/// </summary>
public sealed class MotionStopBoundaryTests
{
    private const string TemplatePlanFile = "adapters/motion-library/MotionStopCommandPlan.cs";

    private static readonly string[] StopFiles =
    {
        "adapters/motion-library/MotionStopCommandPlan.cs",
        "adapters/motion-library/MotionStopAcknowledgement.cs",
        "adapters/motion-library/MotionStopDispatchKernel.cs",
        "adapters/motion-library/MotionStopSession.cs",
        "adapters/motion-library/MotionStopOwner.cs",
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

    // A caller-supplied command text parameter on any Stop type: none may exist.
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
    public void The_Template_Plan_Holds_Exactly_The_Two_Axis_Templates_And_Nothing_Else()
    {
        var text = ReadRepositoryFile(TemplatePlanFile);

        Assert.Equal(2, Regex.Matches(text, "template = \"ST [AB]\";", RegexOptions.CultureInvariant).Count);
        Assert.Contains("template = \"ST A\";", text, StringComparison.Ordinal);
        Assert.Contains("template = \"ST B\";", text, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Stop_Source_Accepts_Caller_Command_Text_Or_A_Caller_Suffix()
    {
        foreach (var file in StopFiles)
        {
            var text = ReadRepositoryFile(file);
            Assert.False(CallerTextParameter.IsMatch(text), file);
        }
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
            if (StopFiles.Contains(relative, StringComparer.Ordinal))
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

        Assert.Equal(new[] { "adapters/motion-library/MotionControllerCommandKernel.cs" }, matches);
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
