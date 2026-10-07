using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wjss.Contracts;
using Wjss.Domain;

// WJSS Runtime host - Stage 0.3A-1 skeleton.
// Order of operations is part of the safety posture: profile validation and
// port configuration happen BEFORE the listener exists, so a refused profile
// can never bind a port, and a busy port can never start a half-live host.

const string ProfileEnv = "WJSS_DEVICE_PROFILE";
const string PortEnv = "WJSS_API_PORT";
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

var baseUrl = $"http://127.0.0.1:{port}";

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(baseUrl);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();
var startedUtc = DateTimeOffset.UtcNow;

// 3. Health only. Live = host alive; Ready = explicit RUNTIME_NOT_IMPLEMENTED
//    for the whole of Stage 0.3A-1 (the authoritative state engine does not
//    exist yet and is NOT claimed by anything in this checkpoint).
app.MapGet(ApiRoutes.HealthLive, () => TypedResults.Json(new HealthLivePayload
{
    Status = "ALIVE",
    Host = "Wjss.Runtime",
    DeviceProfile = profile,
    RuntimeImplemented = false,
    StageMarker = Stage03A1.Marker,
}));

app.MapGet(ApiRoutes.HealthReady, () => TypedResults.Json(
    new HealthReadyPayload
    {
        Status = "NOT_READY",
        Code = Stage03A1.RuntimeNotImplemented,
        Detail = "Stage 0.3A-1 source skeleton: the authoritative Equipment Runtime is not implemented and not authorized in this checkpoint.",
        StageMarker = Stage03A1.Marker,
    },
    statusCode: StatusCodes.Status503ServiceUnavailable));

// CA1873 correction (Owner-local build round 3, 2026-10-07): the argument
// evaluation - including the {StartedUtc:O} round-trip formatting - is only
// performed when Information logging is actually enabled. Same level, same
// template, same structured payloads; no suppression, no interpolation.
if (app.Logger.IsEnabled(LogLevel.Information))
{
    app.Logger.LogInformation(
        "WJSS Runtime host skeleton starting. profile={Profile} url={Url} stage={Stage} startedUtc={StartedUtc:O}",
        profile, baseUrl, Stage03A1.Marker, startedUtc);
}

try
{
    app.Run();
    return 0;
}
catch (IOException ex)
{
    Console.Error.WriteLine(
        $"WJSS startup failed: could not bind {baseUrl}. The port is occupied or not permitted. " +
        $"Set {PortEnv} to a free loopback port and start again. ({ex.GetType().Name}: {ex.Message})");
    return 3;
}

// Test entrypoint visibility for later integration tests (no public surface added).
public partial class Program
{
}
