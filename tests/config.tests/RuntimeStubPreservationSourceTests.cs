using Xunit;

namespace Wjss.Config.Examples.Tests;

/// <summary>
/// Focused source coverage for the CA1873 logging correction (Owner-local
/// build round 3). The correction must not change any Stage 0.3A-1 Runtime
/// stub behaviour, so this test asserts the invariants directly against the
/// authored source of <c>apps/runtime/Program.cs</c>: health/live semantics,
/// health/ready semantics, profile validation ordering (refusals before the
/// listener exists), the refusal exit codes, loopback binding, and the
/// presence of the IsEnabled guard instead of any suppression mechanism.
/// Static source assertions only - no Runtime is launched here.
/// </summary>
public sealed class RuntimeStubPreservationSourceTests
{
    private static string Program =>
        File.ReadAllText(Path.Combine(ConfigTestPaths.RepoRoot(), "apps", "runtime", "Program.cs"));

    [Fact]
    public void Logging_Is_Guarded_Not_Suppressed()
    {
        var src = Program;
        Assert.Contains("app.Logger.IsEnabled(LogLevel.Information)", src, StringComparison.Ordinal);
        var guard = src.IndexOf("app.Logger.IsEnabled(LogLevel.Information)", StringComparison.Ordinal);
        var log = src.IndexOf("app.Logger.LogInformation(", StringComparison.Ordinal);
        Assert.True(guard >= 0 && log >= 0 && guard < log, "guard must precede the call it protects");
        // No suppression or severity tricks were introduced anywhere:
        Assert.DoesNotContain("#pragma warning", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SuppressMessage", src, StringComparison.Ordinal);
        Assert.DoesNotContain("LogLevel.None", src, StringComparison.Ordinal);
        // Structured template preserved verbatim (no interpolation rewrite):
        Assert.Contains("profile={Profile} url={Url} stage={Stage} startedUtc={StartedUtc:O}", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Health_Live_And_Ready_Behavior_Unchanged()
    {
        var src = Program;
        // live: 200 ALIVE payload, implemented:false, stage marker present.
        Assert.Contains("app.MapGet(ApiRoutes.HealthLive", src, StringComparison.Ordinal);
        Assert.Contains("Status = \"ALIVE\"", src, StringComparison.Ordinal);
        Assert.Contains("RuntimeImplemented = false", src, StringComparison.Ordinal);
        Assert.Contains("StageMarker = Stage03A1.Marker", src, StringComparison.Ordinal);
        // ready: 503 with the explicit not-implemented code.
        Assert.Contains("app.MapGet(ApiRoutes.HealthReady", src, StringComparison.Ordinal);
        Assert.Contains("Stage03A1.RuntimeNotImplemented", src, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Profile_Start_Refusals_Unchanged_And_Before_Listener()
    {
        var src = Program;
        // SIMULATOR default + unknown-label parse (exit 4) + non-startable
        // refusal (exit 2) all run BEFORE any MapGet/listener exists.
        Assert.Contains("profileLabel = \"SIMULATOR\"", src, StringComparison.Ordinal);
        Assert.Contains("ProfileStartPolicy.TryParseProfile", src, StringComparison.Ordinal);
        Assert.Contains("ProfileStartPolicy.TryRequireStartable", src, StringComparison.Ordinal);
        var parse = src.IndexOf("TryParseProfile", StringComparison.Ordinal);
        var require = src.IndexOf("TryRequireStartable", StringComparison.Ordinal);
        var bind = src.IndexOf("app.MapGet", StringComparison.Ordinal);
        Assert.True(parse >= 0 && require >= 0 && bind >= 0 && parse < bind && require < bind,
            "profile validation must precede the listener (refusals never bind a port)");
        Assert.Contains("return 4;", src, StringComparison.Ordinal); // unknown profile
        Assert.Contains("return 2;", src, StringComparison.Ordinal); // TEST_HARDWARE / PRODUCTION refusal
        Assert.Contains("return 3;", src, StringComparison.Ordinal); // occupied port
    }

    [Fact]
    public void Loopback_Binding_And_Port_Default_Unchanged()
    {
        var src = Program;
        Assert.Contains("const int DefaultPort = 5181;", src, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1", src, StringComparison.Ordinal);
        // No environment override mechanisms were introduced by any correction:
        Assert.DoesNotContain("TargetPath", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ReferencePath", src, StringComparison.Ordinal);
    }
}
