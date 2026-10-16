using System.Text.Json;
using Wjss.Adapters.MotionLibrary;

// WJSS one-controller identity harness (B1 first-contact software foundation; Owner-invoked,
// console only).
//
// Exit codes (mirroring the probe harness semantics):
//   0 — session accepted: Gate A proof complete, direct mode enforced, one open, the three
//       approved identity reads answered, model token matched, handle closed and native handles
//       released. Physical identity is still Owner-confirmed at the B1-H hardware gate.
//   2 — refused: configuration missing/rejected, proof refused, connection refused, identity
//       response invalid, model confirmation required, or cleanup unproven (canonical refusal
//       code printed); nothing was defaulted, corrected or retried;
//   4 — invalid invocation: this harness takes NO arguments.
//
// The output is deliberately redacted: it reports the outcome code, the redacted detail, the
// model/serial/connection-type statuses and the cleanup state. It never reports the endpoint,
// the serial number, any native path, any hash or any raw vendor error string. No raw controller
// command, axis read, motion or discovery exists in this program.

if (args.Length != 0)
{
    Console.Error.WriteLine(
        "WJSS motion identity refused [INVALID_INVOCATION]: this harness takes no arguments. " +
        "Configuration comes only from the Owner-local environment variables named in the runbook.");
    return 4;
}

if (!MotionLibraryConfigurationResolver.TryResolve(
        Environment.GetEnvironmentVariable,
        out var probeConfiguration,
        out var probeRefusalCode,
        out var probeRefusalDetail))
{
    Console.Error.WriteLine($"WJSS motion identity refused [{probeRefusalCode}]: {probeRefusalDetail}");
    return 2;
}

if (!MotionControllerEndpointResolver.TryResolve(
        Environment.GetEnvironmentVariable,
        out var endpointConfiguration,
        out var endpointRefusalCode,
        out var endpointRefusalDetail))
{
    Console.Error.WriteLine($"WJSS motion identity refused [{endpointRefusalCode}]: {endpointRefusalDetail}");
    return 2;
}

var outcome = new MotionControllerIdentitySession(probeConfiguration, endpointConfiguration).Run();

var json = JsonSerializer.Serialize(
    new
    {
        accepted = outcome.Accepted,
        code = outcome.Code,
        detail = outcome.Detail,
        expectedLogicalIdentity = outcome.ExpectedLogicalIdentity,
        logicalIdentityStatus = outcome.LogicalIdentityStatus,
        connectionTypeStatus = outcome.ConnectionTypeStatus,
        modelComparison = outcome.ModelComparison,
        revisionInformation = outcome.RevisionInformation,
        firmwareStatus = outcome.FirmwareStatus,
        serialStatus = outcome.SerialStatus,
        generation = outcome.Generation,
        controllerHandleClosed = outcome.ControllerHandleClosed,
        nativeHandlesReleased = outcome.NativeHandlesReleased,
        endpoint = MotionControllerEndpointResolver.DescribeConfiguration(endpointConfiguration),
    },
    new JsonSerializerOptions { WriteIndented = true });

if (outcome.Accepted)
{
    Console.WriteLine(json);
    return 0;
}

Console.Error.WriteLine(json);
return 2;
