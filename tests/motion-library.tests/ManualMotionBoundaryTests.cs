using System.Text.RegularExpressions;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Source-boundary checks for the Manual Motion Commissioning vertical slice (PR #34). They pin the
/// structural facts that behavioural tests cannot: the adapter slice is internal only, the closed
/// plan file is the sole carrier of command-language templates, no forbidden command shape (drive
/// enable/disable, relative position, output-bank write, burn, or a second Stop template) can be
/// expressed, the Application and the isolated app carry no raw command text or native token, the
/// simulated port performs no native or network work, and nothing in Runtime/API/Kiosk/Preview
/// composition references the commissioning application. Assertions are line-ending invariant.
/// </summary>
public sealed class ManualMotionBoundaryTests
{
    private static readonly string[] AdapterFiles =
    {
        "adapters/motion-library/ManualMotionCommandKernel.cs",
        "adapters/motion-library/ManualMotionCommandPlan.cs",
        "adapters/motion-library/ManualMotionControllerInvocation.cs",
        "adapters/motion-library/ManualMotionControllerOutcome.cs",
        "adapters/motion-library/ManualMotionControllerOwner.cs",
    };

    private static readonly string[] ApplicationFiles =
    {
        "packages/application/Motion/Manual/ManualMotionPort.cs",
        "packages/application/Motion/Manual/ManualMotionProfile.cs",
        "packages/application/Motion/Manual/ManualMotionSession.cs",
        "packages/application/Motion/Manual/ManualMotionSimulatedPort.cs",
        "packages/application/Motion/Manual/ManualMotionSnapshot.cs",
        "packages/application/Motion/Manual/ManualMotionVocabulary.cs",
    };

    private static readonly string[] AppFiles =
    {
        "apps/motion-commissioning/ManualMotionForm.cs",
        "apps/motion-commissioning/ManualMotionProfileFile.cs",
        "apps/motion-commissioning/ManualMotionSessionRunner.cs",
        "apps/motion-commissioning/MotionLibraryManualMotionPort.cs",
        "apps/motion-commissioning/Program.cs",
    };

    private const string PlanFile = "adapters/motion-library/ManualMotionCommandPlan.cs";

    private static readonly Regex TemplateLiteral = new(
        "\"(?:JG|BG|HV|HM|DP|DE|AC|SP|DC|PA)\"|\"(?:SB|CB) ",
        RegexOptions.CultureInvariant);

    // A quoted two-letter mnemonic immediately closed, spaced, interpolated or assigned: the shapes
    // a command template takes, without matching outcome codes such as "STALE_..." or "PROFILE_...".
    private static readonly Regex ForbiddenTemplate = new(
        "\"(?:SH|MO|PR|OP|BN|ST)(?:\"| |\\{|=)",
        RegexOptions.CultureInvariant);

    private static readonly Regex PublicTypeDeclaration = new(
        @"^\s*public\s+(?:sealed\s+|static\s+|readonly\s+|abstract\s+)*(?:class|interface|enum|record|struct)\b",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static IEnumerable<string> AllSliceFiles() =>
        AdapterFiles.Concat(ApplicationFiles).Concat(AppFiles);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .ReplaceLineEndings("\n");

    [Fact]
    public void The_Adapter_Slice_Declares_No_Public_Type()
    {
        foreach (var file in AdapterFiles)
        {
            Assert.False(
                PublicTypeDeclaration.IsMatch(Read(file)),
                $"a public type is declared in {file}");
        }
    }

    [Fact]
    public void Command_Language_Templates_Exist_Only_In_The_Closed_Plan_File()
    {
        var matches = AllSliceFiles()
            .Append("adapters/motion-library/Wjss.Adapters.MotionLibrary.csproj")
            .Where(file => TemplateLiteral.IsMatch(Read(file)))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { PlanFile }, matches);
    }

    [Fact]
    public void No_Forbidden_Command_Shape_Is_Expressed_Anywhere_In_The_Slice()
    {
        foreach (var file in AllSliceFiles())
        {
            Assert.False(
                ForbiddenTemplate.IsMatch(Read(file)),
                $"a forbidden command template appears in {file}");
        }

        // The plan kind names themselves stay free of the excluded operations.
        var plan = Read(PlanFile);
        foreach (var kindName in new[] { "ServoOn", "MotorOff", "PositionRelative", "OutputBank", "Burn", "Stop" })
        {
            Assert.DoesNotContain("ManualMotionPlanCommandKind." + kindName, plan, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Application_And_App_Carry_No_Native_Token_And_No_Raw_Command_Surface()
    {
        var nativeCommandExport = "gclib" + "_" + "command";

        foreach (var file in ApplicationFiles.Concat(AppFiles).Concat(AdapterFiles))
        {
            Assert.DoesNotContain(nativeCommandExport, Read(file), StringComparison.Ordinal);
        }

        foreach (var file in ApplicationFiles.Concat(AppFiles))
        {
            var text = Read(file);
            Assert.DoesNotContain("string command", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DllImport", text, StringComparison.Ordinal);
            Assert.DoesNotContain("NativeLibrary", text, StringComparison.Ordinal);
        }

        // The form offers no free-text command entry of any kind.
        var form = Read("apps/motion-commissioning/ManualMotionForm.cs");
        Assert.DoesNotContain("TextBox", form, StringComparison.Ordinal);
        Assert.DoesNotContain("RichTextBox", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ComboBox", form, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Simulated_Port_Performs_No_Native_Network_Or_File_Work()
    {
        var simulated = Read("packages/application/Motion/Manual/ManualMotionSimulatedPort.cs");

        foreach (var token in new[]
        {
            "DllImport",
            "NativeLibrary",
            "Socket",
            "TcpClient",
            "HttpClient",
            "File.",
            "Thread.Sleep",
            "Task.Delay",
            "DateTime.Now",
            "DateTimeOffset.Now",
        })
        {
            Assert.DoesNotContain(token, simulated, StringComparison.Ordinal);
        }

        var session = Read("packages/application/Motion/Manual/ManualMotionSession.cs");
        Assert.DoesNotContain("Thread.Sleep", session, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", session, StringComparison.Ordinal);
    }

    [Fact]
    public void Stop_Confirmed_Has_No_Assignment_Path_In_The_Slice()
    {
        foreach (var file in AllSliceFiles())
        {
            var text = Read(file);
            Assert.DoesNotContain("StopConfirmed = true", text, StringComparison.Ordinal);
            Assert.DoesNotContain("StopConfirmed { get; set", text, StringComparison.Ordinal);
            Assert.DoesNotContain("StopConfirmed { get; init", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Isolated_App_References_Only_The_Adapter_And_Application()
    {
        var project = Read("apps/motion-commissioning/Wjss.MotionCommissioning.csproj");

        Assert.Equal(2, Regex.Count(project, "<ProjectReference ", RegexOptions.CultureInvariant));
        Assert.Contains("Wjss.Adapters.MotionLibrary.csproj", project, StringComparison.Ordinal);
        Assert.Contains("Wjss.Runtime.Core.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);

        // The pure Application slice never references the vendor adapter.
        var application = Read("packages/application/Wjss.Runtime.Core.csproj");
        Assert.DoesNotContain("MotionLibrary", application, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Other_Project_References_The_Commissioning_Application()
    {
        var root = RepositoryRoot();
        var referencing = new List<string>();
        foreach (var tree in new[] { "adapters", "apps", "packages" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, tree), "*.csproj", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.Ordinal)
                    || relative.Contains("/obj/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (File.ReadAllText(file).Contains("motion-commissioning", StringComparison.Ordinal))
                {
                    referencing.Add(relative);
                }
            }
        }

        Assert.Empty(referencing);
    }

    [Fact]
    public void Only_The_Approved_Owner_Names_The_Existing_Typed_Stop_Types()
    {
        // The commissioning adapter adds no second Stop path: within this slice, only the controller
        // owner may name the existing typed Stop session, because it reuses it by approved design.
        var stopTypeCarriers = AllSliceFiles()
            .Where(file => Read(file).Contains("MotionStop", StringComparison.Ordinal)
                || Read(file).Contains("BoundStopDispatchKernel", StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "adapters/motion-library/ManualMotionControllerOwner.cs" }, stopTypeCarriers);
    }
}
