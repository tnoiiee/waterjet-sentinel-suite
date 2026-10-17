using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Coverage of the B2-H executable composition surface: the Owner-invoked console harness.
///
/// The harness is deliberately tiny and deliberately inflexible. These tests pin that shape:
/// no arguments, no read path from text, no second configuration source, no controller command,
/// no write or motion vocabulary, no UI, no new dependency and no Runtime/contracts coupling.
/// Nothing here executes the harness; every check is a source/assembly-shape check.
/// </summary>
public sealed class MotionObservationInvocationBoundaryTests
{
    /// <summary>The one public trial constructor: the two Owner-local configuration records.</summary>
    private static readonly Type[] ExpectedPublicConstructorParameterTypes =
    {
        typeof(MotionLibraryProbeConfiguration),
        typeof(MotionControllerEndpointConfiguration),
    };

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private static string AppDirectory() => Path.Combine(RepoRoot(), "apps", "motion-observation");

    private static string ProgramText() => File.ReadAllText(Path.Combine(AppDirectory(), "Program.cs"));

    private static string ProjectText() =>
        File.ReadAllText(Path.Combine(AppDirectory(), "Wjss.MotionObservation.csproj"));

    [Fact]
    public void The_Harness_Exists_And_Is_Registered_In_The_Solution()
    {
        Assert.True(File.Exists(Path.Combine(AppDirectory(), "Program.cs")));
        Assert.True(File.Exists(Path.Combine(AppDirectory(), "Wjss.MotionObservation.csproj")));

        var solution = File.ReadAllText(Path.Combine(RepoRoot(), "WaterJetSentinelSuite.sln"));

        Assert.Contains("Wjss.MotionObservation", solution, StringComparison.Ordinal);
        Assert.Contains(
            @"apps\motion-observation\Wjss.MotionObservation.csproj",
            solution,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_Harness_Takes_No_Argument_And_Returns_The_Documented_Exit_Codes()
    {
        var program = ProgramText();

        // Only the argument COUNT is read. No argument value is ever indexed, parsed or forwarded.
        Assert.Contains("args.Length", program, StringComparison.Ordinal);
        Assert.DoesNotContain("args[", program, StringComparison.Ordinal);

        Assert.Contains("return 4;", program, StringComparison.Ordinal);
        Assert.Contains("return 2;", program, StringComparison.Ordinal);
        Assert.Contains("return 0;", program, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Harness_Reads_No_Endpoint_Variable_Directly_And_Prints_No_Endpoint_Value()
    {
        var program = ProgramText();

        // The endpoint name is resolved inside the adapter's validated resolver only.
        Assert.DoesNotContain(
            MotionControllerEndpointResolver.EndpointVariable,
            program,
            StringComparison.Ordinal);

        // The only endpoint statement the harness may print is the redacted one.
        Assert.Contains(
            "MotionControllerEndpointResolver.DescribeConfiguration",
            program,
            StringComparison.Ordinal);

        Assert.DoesNotContain("EndpointValue", program, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168", program, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", program);
    }

    [Fact]
    public void The_Harness_Has_No_Read_Path_From_Text_And_No_Interactive_Surface()
    {
        var program = ProgramText();

        foreach (var prohibited in new[]
                 {
                     "Console.ReadLine",
                     "Console.ReadKey",
                     "string[] args => ",
                     "args.ToList",
                     "Convert.ToInt32",
                     "int.Parse",
                     "Uri(",
                     "HttpClient",
                     "Socket",
                     "Process",
                     "File.",
                     "Directory.",
                     "Registry",
                     "Reflection",
                     "Assembly.Load",
                     "Environment.Exit",
                     "Thread",
                     "Timer",
                     "Task.Delay",
                     "--direct",
                     "--timeout",
                     "--address",
                 })
        {
            Assert.DoesNotContain(prohibited, program, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Harness_Carries_No_Write_Control_Or_Motion_Request_Vocabulary()
    {
        var program = ProgramText();

        foreach (var prohibited in new[]
                 {
                     "ST ", "JG", "BG ", "HV", "HM", "FE ", "FI", "AC=", "SP=", "DC=",
                     "PA=", "PR=", "DP", "DE=", "SB ", "CB ", "OB ", "OP ", "@OUT[",
                     "Servo", "Brake", "Home", "Jog", "Move", "Configure",
                 })
        {
            Assert.DoesNotContain(prohibited, program, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Project_Is_A_Console_Exe_With_No_Package_And_Only_The_Adapter_Reference()
    {
        var project = ProjectText();

        Assert.Contains("<OutputType>Exe</OutputType>", project, StringComparison.Ordinal);
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", project, StringComparison.Ordinal);
        Assert.Contains(
            "../../adapters/motion-library/Wjss.Adapters.MotionLibrary.csproj",
            project,
            StringComparison.Ordinal);

        Assert.DoesNotContain("PackageReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("packages\\", project, StringComparison.Ordinal);
        Assert.DoesNotContain("UseWPF", project, StringComparison.Ordinal);
        Assert.DoesNotContain("UseWindowsForms", project, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetCore", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Wjss.Runtime", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Wjss.Contracts", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Wjss.Domain", project, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Adapter_Public_Surface_Exposes_No_String_Reaching_Trial_Path()
    {
        var trialType = typeof(MotionObservationTrialSession);

        Assert.True(trialType.IsSealed);

        foreach (var method in trialType.GetMethods().Where(m => m.DeclaringType == trialType))
        {
            Assert.DoesNotContain(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(string));
        }

        foreach (var constructor in trialType.GetConstructors())
        {
            Assert.DoesNotContain(
                constructor.GetParameters(),
                parameter => parameter.ParameterType == typeof(string));
        }

        // The public boundary is exactly the two configuration records plus the parameterless Run.
        var publicConstructors = trialType
            .GetConstructors()
            .Where(constructor => constructor.IsPublic)
            .ToArray();

        var constructor = Assert.Single(publicConstructors);
        Assert.Equal(
            ExpectedPublicConstructorParameterTypes,
            constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    [Fact]
    public void The_Harness_Is_Not_Reachable_From_The_Runtime_Preview_Or_Contracts()
    {
        var root = RepoRoot();

        foreach (var tree in new[] { "apps/runtime", "apps/ops-preview", "apps/kiosk", "apps/ui", "packages" })
        {
            var directory = Path.Combine(root, tree.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("node_modules/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("/dist/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Assert.DoesNotContain(
                    "Wjss.MotionObservation",
                    File.ReadAllText(file),
                    StringComparison.Ordinal);
                Assert.DoesNotContain(
                    "MotionObservationTrialSession",
                    File.ReadAllText(file),
                    StringComparison.Ordinal);
            }
        }
    }
}
