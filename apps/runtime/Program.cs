using System.Globalization;
using System.Text;
using Wjss.Adapters.TcAcquisition;
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

// 0. Acquisition mode (explicit; absent value means the existing deterministic Product behaviour).
//    The mode is read BEFORE the profile so that the profile default and the profile gate can agree
//    with it. Nothing is inferred from any other variable, and no mode is silently substituted.
if (!TcEndpointConfiguration.TryReadAcquisitionMode(
    Environment.GetEnvironmentVariable, out var acquisitionMode, out var modeCode, out var modeDetail))
{
    Console.Error.WriteLine($"WJSS startup refused [{modeCode}]: {modeDetail} Nothing was bound.");
    return 4;
}

var physicalReadOnly = acquisitionMode == TcAcquisitionMode.PHYSICAL_READ_ONLY;

// 0b. Explicit dry command-composition selector (ACCELERATED-CLEANING-INTEGRATION-01).
//     The selector is read BEFORE the profile so that the profile default and the profile gate can
//     agree with it. An absent selector means the mode was NOT chosen; only the exact literal
//     selects it. Nothing is inferred from the profile, the endpoint or any other variable.
if (!ProcessIoEndpointConfiguration.TryReadDryMode(
    Environment.GetEnvironmentVariable, out var dryCommandMode, out var dryModeCode, out var dryModeDetail))
{
    Console.Error.WriteLine($"WJSS startup refused [{dryModeCode}]: {dryModeDetail} Nothing was bound.");
    return 4;
}

if (dryCommandMode && physicalReadOnly)
{
    Console.Error.WriteLine(
        $"WJSS startup refused [{ProfileStartPolicy.ModeConflictCode}]: the dry command-composition "
        + "mode and the physical read-only mode are mutually exclusive and were both requested. "
        + "No mode is selected on the caller's behalf and nothing was bound.");
    return 2;
}

// 1. Device profile (ADR-0012). Default SIMULATOR; the explicit physical read-only mode defaults to
//    TEST_HARDWARE and nothing else. Unknown labels are a configuration error; any profile the mode
//    does not authorize is refused by the policy.
var profileLabel = Environment.GetEnvironmentVariable(ProfileEnv);
if (string.IsNullOrWhiteSpace(profileLabel))
{
    profileLabel = physicalReadOnly
        ? nameof(DeviceProfile.TEST_HARDWARE_READ_ONLY)
        : nameof(DeviceProfile.SIMULATOR);
}

if (!ProfileStartPolicy.TryParseProfile(profileLabel, out var profile))
{
    Console.Error.WriteLine(
        $"WJSS startup refused: unknown device profile '{profileLabel}' " +
        "(expected SIMULATOR | TEST_HARDWARE | PRODUCTION | TEST_HARDWARE_READ_ONLY). " +
        $"Fix {ProfileEnv}. Nothing was bound.");
    return 4;
}

// 0c. Isolated IV Hardware trial selector (ACCELERATED-CLEANING-INTEGRATION-01 two-stage evidence).
//
//     This is a SEPARATE evidence class from the integrated dry command composition below, and the
//     two are never combined: the trial refuses when the integrated dry selector is also set, and the
//     integrated dry path is not modified by this branch. The trial is selected by its own exact
//     literal (see DryIvTrial.Token / DryIvTrial.ModeVariable) plus the published single-DO consent
//     and the Owner Pump-stopped attestation, and it is validated in full by DryIvTrial itself, so
//     PRODUCTION, a non-dry profile, a missing endpoint, a missing consent or a missing attestation
//     all refuse before anything is bound.
if (!DryIvTrial.TryReadMode(Environment.GetEnvironmentVariable, out var ivTrialSelected, out var ivTrialCode, out var ivTrialDetail))
{
    Console.Error.WriteLine($"WJSS startup refused [{ivTrialCode}]: {ivTrialDetail} Nothing was bound.");
    return 4;
}

if (ivTrialSelected)
{
    return DryIvTrial.Run(profile, Environment.GetEnvironmentVariable, Console.Out, Console.Error);
}

// 0d. Isolated READ-ONLY physical Pump-ready trial selector (Trial A).
//
//     The third evidence class, and the only read-only one. It reads AI-003+ through the existing
//     Process-Io transport and applies the existing Pump-ready rule; it commands nothing. Like the IV
//     trial it is selected by its own exact literal and validated in full by its own runner, so
//     PRODUCTION, a non-authorized profile, a missing endpoint, a missing consent, a missing catalogue
//     or a missing AI-003+ binding all refuse before a transport is constructed. It also refuses when
//     either of the other two physical selectors is set, so the three are never combined.
if (!ProcessIoPumpReadyReadTrial.TryReadMode(
    Environment.GetEnvironmentVariable, out var pumpReadyReadSelected, out var pumpReadCode, out var pumpReadDetail))
{
    Console.Error.WriteLine($"WJSS startup refused [{pumpReadCode}]: {pumpReadDetail} Nothing was bound.");
    return 4;
}

if (pumpReadyReadSelected)
{
    return ProcessIoPumpReadyReadTrial.Run(profile, Environment.GetEnvironmentVariable, Console.Out, Console.Error);
}

// 0e. The Pump command ladder alone (runbook Stage D, first variant).
//
//     The fourth evidence class. It commands the Pump ladder over the existing dry write authorization and
//     nothing else: no equipment boundary is composed, so that run holds no Isolation Valve command surface
//     at all, and no motion composition exists, so no axis moves. Like the other two physical trials it is
//     selected by its own exact literal and validated in full by its own runner, and it refuses when another
//     physical evidence class is selected beside it.
if (!PumpCommandDryTrial.TryReadMode(
    Environment.GetEnvironmentVariable, out var pumpCommandSelected, out var pumpCommandCode, out var pumpCommandDetail))
{
    Console.Error.WriteLine($"WJSS startup refused [{pumpCommandCode}]: {pumpCommandDetail} Nothing was bound.");
    return 4;
}

if (pumpCommandSelected)
{
    return PumpCommandDryTrial.Run(profile, Environment.GetEnvironmentVariable, Console.Out, Console.Error);
}

if (!ProfileStartPolicy.TryRequireStartable(profile, physicalReadOnly, dryCommandMode, out var refusalCode, out var refusalReason))
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

// 3b. TEST_HARDWARE_DRY: build the ONE dry composition root, here, before any listener exists.
//
//     It is deliberately NOT routed through SimulatorRuntime. The mode has exactly one surface: the
//     bounded, single-use console trial that this branch runs and then exits. No HTTP route, no
//     command API and no Kiosk control is added, and the read-only SIMULATOR projection is never
//     served beside a physical composition (that would blur provenance).
if (dryCommandMode)
{
    return CleaningPathDryTrial.Run(profile, port, Environment.GetEnvironmentVariable, Console.Out, Console.Error);
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

// The one-read client of the selected mode. Deterministic mode keeps the socket-free client; the
// physical read-only mode uses the existing read-only adapter for the Owner-local endpoint. The
// factory is chosen from the validated mode only, so a client can never be constructed for a mode
// that was not selected, and only the Runtime owns this client lifetime. Nothing here contacts a
// device: the client is constructed at composition time and used by the one-use session.
Func<ITcReadOnlyClient> tcClientFactory = options.AcquisitionMode == TcAcquisitionMode.PHYSICAL_READ_ONLY
    ? TcReadOnlyClientFactory.Create
    : TcReadOnlyClientFactory.CreateDeterministic;

SimulatorRuntime runtime;
try
{
    runtime = SimulatorRuntime.Create(options, clock, ObserveFault, tcClientFactory);
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

// The Operator presentation (Stage 0.4C-K4) ships inside this host's output the same way the
// Inspector page does: one document plus a fixed set of browser-native modules, read from the build
// directory with no Node build pipeline, no package manager and no static-file middleware. The asset
// names below are a frozen whitelist, so a request never contributes a path segment: an unmatched
// name is answered with 404 before the file system is consulted, which makes a nested or traversed
// path impossible by construction instead of by filtering.
var uiDirectory = Path.Combine(AppContext.BaseDirectory, "Ui");
var uiIndexPath = Path.Combine(uiDirectory, "index.html");
var uiAssets = new HashSet<string>(StringComparer.Ordinal)
{
    "global.css", "app.mjs", "feed.mjs", "wallMap.mjs", "sensorDetail.mjs", "thermal.mjs",
    "dispatchPanel.mjs", "opsPanel.mjs",
};
var uiContentTypes = new Dictionary<string, string>(StringComparer.Ordinal)
{
    [".css"] = "text/css; charset=utf-8",
    [".mjs"] = "text/javascript; charset=utf-8",
};

// 5. Start only an initialized Runtime. A faulted composition has no writer or
//    Snapshot: keep the host's read-only 503 diagnostics available without claiming
//    that an evolution loop or a Delta feed exists.
if (runtime.IsInitialized) runtime.Start();

// 6. Read-only surface (GET only). Every endpoint reports observation; none
//    accepts a command, and no write route exists anywhere in this host.
IResult RuntimeUnavailable(string code, string detail) => Results.Json(
    new RuntimeUnavailablePayload
    {
        Code = code,
        Detail = detail,
        StageMarker = RuntimeStage.Marker,
    },
    ContractJson.Options,
    statusCode: StatusCodes.Status503ServiceUnavailable);

IResult SnapshotEndpoint() => runtime.IsInitialized
    ? Results.Json(runtime.ProjectSnapshot(runtime.Publication), ContractJson.Options)
    : RuntimeUnavailable(
        RuntimeReadinessCodes.StoreNotInitialized,
        "The Runtime State Store is not initialized, so there is no authoritative Snapshot to serve.");

IResult RuntimeEndpoint() => Results.Json(BuildStatus(), ContractJson.Options);

IResult DispatchWorkspaceEndpoint() => runtime.IsInitialized
    ? Results.Json(runtime.ProjectWorkspace(runtime.Publication), ContractJson.Options)
    : RuntimeUnavailable(
        RuntimeReadinessCodes.StoreNotInitialized,
        "No authoritative Runtime publication exists, so no Cleaning Job Workspace document is available.");

IResult DeltasEndpoint() => runtime.IsInitialized
    ? Results.Json(RuntimeDeltaFeed.From(runtime.Publication), ContractJson.Options)
    : RuntimeUnavailable(
        RuntimeReadinessCodes.StoreNotInitialized,
        "No authoritative Runtime publication exists, so no Delta feed is available.");

IResult InspectorEndpoint() => File.Exists(inspectorPagePath)
    ? Results.File(inspectorPagePath, "text/html; charset=utf-8")
    : Results.Text(
        "The development Inspector page is not present in this build output.",
        "text/plain",
        Encoding.UTF8,
        StatusCodes.Status500InternalServerError);

// The Operator document is a file read, not a computed projection: a build that does not carry the
// presentation says so with a 500 and a plain sentence, exactly as the Inspector route does. It
// never falls back to a stale copy and never serves a partially updated tree.
IResult UiEndpoint() => File.Exists(uiIndexPath)
    ? Results.File(uiIndexPath, "text/html; charset=utf-8")
    : Results.Text(
        "The Operator presentation is not present in this build output.",
        "text/plain",
        Encoding.UTF8,
        StatusCodes.Status500InternalServerError);

// The route value is matched against the frozen set before anything is composed from it, and the
// content type comes from the extension of an allow-listed name rather than from the request. An
// unknown name, a nested name and an unknown extension are all the same answer: 404.
IResult UiAssetEndpoint(string file)
{
    if (!uiAssets.Contains(file))
    {
        return Results.NotFound();
    }

    var assetPath = Path.Combine(uiDirectory, file);
    if (!File.Exists(assetPath))
    {
        return Results.NotFound();
    }

    return uiContentTypes.TryGetValue(Path.GetExtension(file), out var contentType)
        ? Results.File(assetPath, contentType)
        : Results.NotFound();
}

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

// 7b. The liveness route of the Kiosk shell. The shell reloads the approved route as soon as this
//     answers, so the route must exist on the same origin and port as the presentation - that is the
//     whole reason it is here. It answers 200 whenever the host is up, independent of readiness, and
//     reports the acquisition mode as the host's own redacted label: it states what the host may
//     READ and never implies a write, a command or a device action.
app.MapGet(ApiRoutes.KioskHealth, () => Results.Json(
    new KioskHealthPayload
    {
        Status = "ALIVE",
        Host = "Wjss.Runtime",
        DeviceProfile = options.Profile,
        AcquisitionMode = TcEndpointConfiguration.DescribeMode(options.AcquisitionMode),
        Ready = runtime.Readiness().Ready,
        CurrentRevision = runtime.IsInitialized ? (int?)runtime.Publication.Current.Revision : null,
        StageMarker = RuntimeStage.Marker,
    },
    ContractJson.Options));

app.MapGet(ApiRoutes.Snapshot, SnapshotEndpoint);
app.MapGet(ApiRoutes.DispatchWorkspace, DispatchWorkspaceEndpoint);
app.MapGet(ApiRoutes.Runtime, RuntimeEndpoint);
app.MapGet(ApiRoutes.Deltas, DeltasEndpoint);
// ONE registration only: ASP.NET Core matches a template with or without a
// trailing slash, so a second "/inspector/" endpoint is a second candidate for
// the same request and makes the match ambiguous (AmbiguousMatchException).
app.MapGet(ApiRoutes.Inspector, InspectorEndpoint);
// ONE registration each, for the same reason: the presentation document and its modules are read-only
// files of this host, and they carry no verb, no parameter beyond the allow-listed name and no
// request body. Serving them changes nothing the Runtime observes and authorizes nothing.
app.MapGet(ApiRoutes.Ui, UiEndpoint);
app.MapGet(ApiRoutes.UiAsset, UiAssetEndpoint);

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
            DisabledSensorIds = [],
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
        DisabledSensorIds = runtime.Sequencing.DisabledSensorIds,
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
// The acquisition mode and the REDACTED endpoint identity are observable; the endpoint value is
// never logged. DescribeEndpoint returns only ENDPOINT_CONFIGURED / ENDPOINT_NOT_CONFIGURED.
if (app.Logger.IsEnabled(LogLevel.Information))
{
    app.Logger.LogInformation(
        "WJSS Runtime host starting. profile={Profile} acquisition={Acquisition} endpoint={Endpoint} "
        + "url={Url} stage={Stage} startedUtc={StartedUtc:O}",
        options.Profile,
        TcEndpointConfiguration.DescribeMode(options.AcquisitionMode),
        TcEndpointConfiguration.DescribeEndpoint(options.Endpoint?.Endpoint),
        baseUrl,
        RuntimeStage.Marker,
        startedUtc);
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
