using Xunit;

namespace Wjss.Config.Examples.Tests;

/// <summary>
/// Focused source and asset coverage for the Runtime host start gate and the
/// read-only surface contract.
///
/// Stage 0.3A-1 pinned the skeleton's stub answers (health/ready always 503 +
/// RUNTIME_NOT_IMPLEMENTED, health/live implemented:false). Stage 0.3A-2C is
/// authorized to replace those stub answers with the composed SIMULATOR runtime
/// and its readiness contract, so the assertions below pin the NEW contract while
/// keeping every start-gate rule that must not change: profile validation before
/// the listener exists, the refusal exit codes, loopback binding, no suppression
/// mechanism, and no write, command or SSE route anywhere in the host.
///
/// Static source and asset assertions only - no Runtime is launched here.
/// </summary>
public sealed class RuntimeStartGateSourceTests
{
    private static string Program =>
        File.ReadAllText(Path.Combine(ConfigTestPaths.RepoRoot(), "apps", "runtime", "Program.cs"));

    private static string InspectorPage =>
        File.ReadAllText(Path.Combine(ConfigTestPaths.RepoRoot(), "apps", "runtime", "Inspector", "index.html"));

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
    public void Health_Semantics_Follow_The_Checkpoint_C_Readiness_Contract()
    {
        var src = Program;

        // live: 200 ALIVE while the host process is alive, and no readiness claim.
        Assert.Contains("app.MapGet(ApiRoutes.HealthLive", src, StringComparison.Ordinal);
        Assert.Contains("Status = \"ALIVE\"", src, StringComparison.Ordinal);
        Assert.Contains("RuntimeImplemented = true", src, StringComparison.Ordinal);
        Assert.Contains("StageMarker = RuntimeStage.Marker", src, StringComparison.Ordinal);

        // ready: 200 only when ready, else 503 with the structured readiness code.
        Assert.Contains("app.MapGet(ApiRoutes.HealthReady", src, StringComparison.Ordinal);
        Assert.Contains("runtime.Readiness()", src, StringComparison.Ordinal);
        Assert.Contains("readiness.Code", src, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status200OK", src, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", src, StringComparison.Ordinal);

        // The superseded stub answer is gone from the host.
        Assert.DoesNotContain("Stage03A1.RuntimeNotImplemented", src, StringComparison.Ordinal);
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
        Assert.Contains("return 4;", src, StringComparison.Ordinal); // unknown profile / bad configuration
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

    [Fact]
    public void Host_Exposes_Read_Only_Routes_And_No_Write_Or_Stream_Route()
    {
        var src = Program;

        // The read-only surface is registered.
        Assert.Contains("app.MapGet(ApiRoutes.Snapshot", src, StringComparison.Ordinal);
        Assert.Contains("app.MapGet(ApiRoutes.Runtime", src, StringComparison.Ordinal);
        Assert.Contains("app.MapGet(ApiRoutes.Deltas", src, StringComparison.Ordinal);
        Assert.Contains("app.MapGet(ApiRoutes.Inspector", src, StringComparison.Ordinal);

        // No write/command route exists anywhere in the host.
        foreach (var writeVerb in new[] { "MapPost", "MapPut", "MapPatch", "MapDelete", "MapMethods" })
        {
            Assert.DoesNotContain(writeVerb, src, StringComparison.Ordinal);
        }

        // No streaming surface: the Inspector polls, it does not stream.
        Assert.DoesNotContain("text/event-stream", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Inspector_Page_Is_Present_And_Polls_With_Get_Only()
    {
        var page = InspectorPage;

        // It is the development Inspector and it says so.
        Assert.Contains("Runtime Inspector", page, StringComparison.Ordinal);
        Assert.Contains("SIMULATOR", page, StringComparison.Ordinal);
        Assert.Contains("read-only", page, StringComparison.Ordinal);
        Assert.Contains("TEST_HARDWARE is not authorized", page, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION device access is not authorized", page, StringComparison.Ordinal);

        // It polls exactly the read-only endpoints.
        Assert.Contains("/api/v1/snapshot", page, StringComparison.Ordinal);
        Assert.Contains("/api/v1/runtime", page, StringComparison.Ordinal);
        Assert.Contains("/api/v1/deltas", page, StringComparison.Ordinal);

        // Every request it issues is a GET; no write verb appears in its script.
        Assert.Contains("method: \"GET\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("method: \"POST\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("method: \"PUT\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("method: \"PATCH\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("method: \"DELETE\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("XMLHttpRequest", page, StringComparison.Ordinal);

        // The bounded Delta view stays bounded in the DOM as well.
        Assert.Contains("MAX_DELTA_ROWS", page, StringComparison.Ordinal);
    }
}
