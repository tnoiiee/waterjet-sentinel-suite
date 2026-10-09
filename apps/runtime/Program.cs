using System.Globalization;
using System.Text;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime;
using Wjss.Runtime.Core;
using Wjss.Time;

// WJSS Runtime host - Stage 0.3A-2C: SIMULATOR runtime composition, read-only API
// and the development Runtime Inspector.
//
// Order of operations is part of the safety posture: device-profile validation,
// port configuration and synthetic configuration validation all happen BEFORE the
// listener exists, so a refused profile or a malformed value can never bind a
// port, and a busy port can never start a half-live host.
//
// Scope honesty: the API is READ-ONLY. No POST/PUT/PATCH/DELETE route exists, no
// command path exists, and nothing served here dispatches, commands or actuates
// anything. The synthetic values are development presentation values.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA - Owner-local build, execution
// and UI review are the validation of record
// (docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md).

const string ProfileEnv = RuntimeHostOptions.ProfileVariable;
const string PortEnv = RuntimeHostOptions.PortVariable;
const int DefaultPort = 5181;

// 1. Device profile (ADR-0012). Default SIMULATOR; unknown labels are a
//    configuration error; anything but SIMULATOR is refused by the policy.
var profileLabel = Environment.GetEnvironmentVariable(ProfileEnv);
if (string.IsNullOrWhiteSpace(profileLabel))
{
    profileLabel = "SIMULATOR";
}

if (!ProfileStartPolicy.TryParseProfile(profileLabel, out var profile))
{
    Console.Error.WriteLine(
        $"WJSS startup refused: unknown device profile '{profileLabel}' " +
        "(expected SIMULATOR | TEST_HARDWARE | PRODUCTION). " +
        $"Fix {ProfileEnv}. Nothing was bound.");
    return 4;
}

if (!ProfileStartPolicy.TryRequireStartable(profile, out var refusalCode, out var refusalReason))
{
    Console.Error.WriteLine($"WJSS startup refused [{refusalCode}]: {refusalReason}");
    return 2;
}

// 2. Loopback port (local override allowed; fail clearly if occupied).
var portLabel = Environment.GetEnvironmentVariable(PortEnv);
if (!int.TryParse(portLabel ?? DefaultPort.ToString(CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
    || port is < 1 or > 65535)
{
    Console.Error.WriteLine($"WJSS startup refused: invalid {PortEnv} '{portLabel}' (expected 1-65535). Nothing was bound.");
    return 4;
}

// 3. Synthetic configuration: determinism seed, tick interval and bounded history
//    capacities, each with a safe development default and a clear refusal.
if (!RuntimeHostOptions.TryLoadSynthetic(
    profile,
    port,
    Environment.GetEnvironmentVariable,
    out var options,
    out var configurationCode,
    out var configurationDetail))
{
    Console.Error.WriteLine($"WJSS startup refused [{configurationCode}]: {configurationDetail} Nothing was bound.");
    return 4;
}

var baseUrl = $"http://127.0.0.1:{port}";

// 4. Compose the SIMULATOR runtime BEFORE the listener exists. A composition
//    fault is retained by the runtime and reported by readiness as a structured
//    503 with its machine code - never as a silently degraded runtime.
var clock = new SystemClock();
using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole());
var faultWatchdog = bootstrapLoggerFactory.CreateLogger("Wjss.Runtime.Faults");

void ObserveFault(string code, string detail)
{
    // Synchronous and allocation-free while the warning level is disabled: a
    // fault is made observable, never used to control the Runtime.
    if (faultWatchdog.IsEnabled(LogLevel.Warning))
    {
        faultWatchdog.LogWarning("WJSS runtime fault code={Code} detail={Detail}", code, detail);
    }
}

SimulatorRuntime runtime;
try
{
    runtime = SimulatorRuntime.Create(options, clock, ObserveFault);
}
catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine($"WJSS runtime composition failed: {ex.Message}");
    runtime = SimulatorRuntime.CreateFaulted(
        options, clock, RuntimeHostFaultCodes.CompositionFault, ex.Message, ObserveFault);
}

// Every payload of this host is serialized with the shared contract settings
// (ContractJson.Options: camelCase, UPPER_SNAKE enum names, explicit nulls), so
// the host and the golden fixtures can never disagree through two JSON policies.
var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(baseUrl);

var app = builder.Build();
var startedUtc = DateTimeOffset.UtcNow;
var inspectorPagePath = Path.Combine(AppContext.BaseDirectory, "Inspector", "index.html");

// 5. Start the deterministic synthetic evolution lifecycle: one non-overlapping
//    loop, one accepted tick = one committed revision, cancelled on shutdown.
runtime.Start();

// 6. Read-only surface (GET only). Every endpoint reports observation; none
//    accepts a command, and no write route exists anywhere in this host.
IResult RuntimeUnavailable(string code, string detail) => Results.Json(
    new RuntimeUnavailablePayload
    {
        Code = code,
        Detail = detail,
        StageMarker = RuntimeStage.Marker,
    },
    statusCode: StatusCodes.Status503ServiceUnavailable);

IResult SnapshotEndpoint() => runtime.IsInitialized
    ? Results.Json(runtime.ProjectSnapshot(runtime.Publication), ContractJson.Options)
    : RuntimeUnavailable(
        RuntimeReadinessCodes.StoreNotInitialized,
        "The Runtime State Store is not initialized, so there is no authoritative Snapshot to serve.");

IResult RuntimeEndpoint() => Results.Json(BuildStatus(), ContractJson.Options);

IResult DeltasEndpoint() => Results.Json(
    runtime.IsInitialized ? RuntimeDeltaFeed.From(runtime.Publication) : RuntimeDeltaFeed.From(runtime.Deltas, null),
    ContractJson.Options);

IResult InspectorEndpoint() => File.Exists(inspectorPagePath)
    ? Results.File(inspectorPagePath, "text/html; charset=utf-8")
    : Results.Text(
        "The development Inspector page is not present in this build output.",
        "text/plain",
        Encoding.UTF8,
        StatusCodes.Status500InternalServerError);

// 7. Health: live = host alive (never a readiness claim); ready = the full
//    readiness contract, 200 only when every condition holds, else 503 with a
//    structured machine code and no silent fallback.
app.MapGet(ApiRoutes.HealthLive, () => Results.Json(
    new HealthLivePayload
    {
        Status = "ALIVE",
        Host = "Wjss.Runtime",
        DeviceProfile = options.Profile,
        RuntimeImplemented = true,
        StageMarker = RuntimeStage.Marker,
    },
    ContractJson.Options));

app.MapGet(ApiRoutes.HealthReady, () =>
{
    var readiness = runtime.Readiness();
    var payload = new HealthReadyPayload
    {
        Status = readiness.Ready ? "READY" : "NOT_READY",
        Code = readiness.Code,
        Detail = readiness.Detail,
        StageMarker = RuntimeStage.Marker,
    };

    return readiness.Ready
        ? Results.Json(payload, ContractJson.Options, statusCode: StatusCodes.Status200OK)
        : Results.Json(payload, ContractJson.Options, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapGet(ApiRoutes.Snapshot, SnapshotEndpoint);
app.MapGet(ApiRoutes.Runtime, RuntimeEndpoint);
app.MapGet(ApiRoutes.Deltas, DeltasEndpoint);
// ONE registration only: ASP.NET Core matches a template with or without a
// trailing slash, so a second "/inspector/" endpoint is a second candidate for
// the same request and makes the match ambiguous (AmbiguousMatchException).
app.MapGet(ApiRoutes.Inspector, InspectorEndpoint);

RuntimeStatus BuildStatus()
{
    var publication = runtime.IsInitialized ? runtime.Publication : null;
    var readiness = runtime.Readiness(publication);
    var counts = RuntimeStatusSensorCounts.Canonical();

    if (!runtime.IsInitialized)
    {
        return new RuntimeStatus
        {
            Ready = readiness.Ready,
            Scenario = options.Scenario.ToString(),
            LowPressureThresholdBar = options.PressureThresholds.LowPressureThresholdBar,
            HighPressureThresholdBar = options.PressureThresholds.HighPressureThresholdBar,
            PumpReadySetpointBar = options.PressureThresholds.PumpReadySetpointBar,
            ReadinessCode = readiness.Code,
            ReadinessDetail = readiness.Detail,
            Profile = options.Profile,
            StageMarker = RuntimeStage.Marker,
            CurrentRevision = null,
            GeneratedAt = null,
            LastUpdateAt = null,
            SyntheticSeed = options.SyntheticSeed,
            TickIntervalMilliseconds = options.TickIntervalMilliseconds,
            EvolutionRunning = runtime.IsRunning,
            SensorCounts = counts,
            Walls = [],
            QueueCount = 0,
            QueueCapacity = QueueSummary.MaxEntries,
            QueuePlaceholder = RuntimeStatus.QueuePlaceholderLabel,
            ActiveJobPresent = false,
            ActiveJobId = null,
            PumpState = PumpRunState.STOPPED,
            PumpPlaceholder = RuntimeStatus.PumpPlaceholderLabel,
            ActiveAlarmCount = 0,
            ClearedAlarmCount = 0,
            AcceptedTicks = runtime.AcceptedTicks,
            RejectedTransitions = runtime.RejectedTransitions,
            StateHistoryDepth = 0,
            StateHistoryCapacity = options.StateHistoryCapacity,
            DeltaHistoryDepth = publication?.Deltas.Count ?? 0,
            DeltaHistoryCapacity = publication?.HistoryCapacity ?? options.DeltaHistoryCapacity,
            NewestDeltaRevision = publication?.NewestDeltaRevision,
            UptimeSeconds = runtime.UptimeSeconds,
            ServerTimeUtc = UtcTimestamps.Format(clock.UtcNow),
            LastFaultCode = runtime.LastFaultCode,
            LastFaultDetail = runtime.LastFaultDetail,
            StartupFaultCode = runtime.StartupFaultCode,
        };
    }

    var state = publication!.Current;
    var counters = runtime.CountersFor(publication);
    var generatedAt = UtcTimestamps.Format(state.GeneratedAtUtc);

    return new RuntimeStatus
    {
        Ready = readiness.Ready,
        Scenario = options.Scenario.ToString(),
        LowPressureThresholdBar = options.PressureThresholds.LowPressureThresholdBar,
        HighPressureThresholdBar = options.PressureThresholds.HighPressureThresholdBar,
        PumpReadySetpointBar = options.PressureThresholds.PumpReadySetpointBar,
        ReadinessCode = readiness.Code,
        ReadinessDetail = readiness.Detail,
        Profile = state.DeviceProfile,
        StageMarker = RuntimeStage.Marker,
        CurrentRevision = state.Revision,
        GeneratedAt = generatedAt,
        LastUpdateAt = generatedAt,
        SyntheticSeed = options.SyntheticSeed,
        TickIntervalMilliseconds = options.TickIntervalMilliseconds,
        EvolutionRunning = runtime.IsRunning,
        SensorCounts = counts,
        Walls = [.. state.Walls.Select(RuntimeWallSummaryView.From)],
        QueueCount = state.Queue.TotalQueued,
        QueueCapacity = state.Queue.Capacity,
        QueuePlaceholder = RuntimeStatus.QueuePlaceholderLabel,
        ActiveJobPresent = state.ActiveJob is not null,
        ActiveJobId = state.ActiveJob?.JobId,
        PumpState = state.Pump.State,
        PumpPlaceholder = RuntimeStatus.PumpPlaceholderLabel,
        ActiveAlarmCount = state.Alarms.ActiveUnack + state.Alarms.ActiveAck,
        ClearedAlarmCount = state.Alarms.ClearedUnack,
        AcceptedTicks = publication.Generation,
        RejectedTransitions = runtime.RejectedTransitions,
        StateHistoryDepth = counters.HistoryDepth,
        StateHistoryCapacity = counters.HistoryCapacity,
        DeltaHistoryDepth = publication.Deltas.Count,
        DeltaHistoryCapacity = publication.HistoryCapacity,
        NewestDeltaRevision = publication.NewestDeltaRevision,
        UptimeSeconds = runtime.UptimeSeconds,
        ServerTimeUtc = UtcTimestamps.Format(clock.UtcNow),
        LastFaultCode = runtime.LastFaultCode,
        LastFaultDetail = runtime.LastFaultDetail,
        StartupFaultCode = runtime.StartupFaultCode,
    };
}

// CA1873 correction (Owner-local build round 3, 2026-10-07): the argument
// evaluation - including the {StartedUtc:O} round-trip formatting - is only
// performed when Information logging is actually enabled. Same level, same
// template, same structured payloads; no suppression, no interpolation.
if (app.Logger.IsEnabled(LogLevel.Information))
{
    app.Logger.LogInformation(
        "WJSS Runtime host starting. profile={Profile} url={Url} stage={Stage} startedUtc={StartedUtc:O}",
        options.Profile, baseUrl, RuntimeStage.Marker, startedUtc);
}

try
{
    app.Run();
}
catch (IOException ex)
{
    Console.Error.WriteLine(
        $"WJSS startup failed: could not bind {baseUrl}. The port is occupied or not permitted. " +
        $"Set {PortEnv} to a free loopback port and start again. ({ex.GetType().Name}: {ex.Message})");

    await runtime.DisposeAsync();
    return 3;
}

// Clean shutdown: cancels the evolution loop and observes it without rethrowing
// its outcome.
await runtime.DisposeAsync();
return 0;

// Test entrypoint visibility for later integration tests (no public surface added).
public partial class Program
{
}
