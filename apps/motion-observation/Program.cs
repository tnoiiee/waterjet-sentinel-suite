using System.Text.Json;
using Wjss.Adapters.MotionLibrary;

// WJSS one-shot read-only motion observation harness (B2-H composition; Owner-invoked, console
// only).
//
// Exit codes (mirroring the probe and identity harness semantics):
//   0 — trial accepted: the native proof passed, the direct/no-gcaps posture was selected, one
//       controller handle was opened with the validated controller address, the fixed sixteen
//       typed reads were accepted in plan order, the observation boundary was invalidated, and the
//       controller handle and every native module handle were released and confirmed;
//   2 — refused: configuration missing/rejected, proof refused, direct mode not enforced, the open
//       refused (canonical open-result word printed), the read boundary unavailable, a typed read
//       refused, or cleanup unproven (canonical refusal code printed). Nothing was defaulted,
//       corrected or retried;
//   4 — invalid invocation: this harness takes NO arguments.
//
// The output is deliberately redacted: it reports the outcome code, the redacted detail, the open
// classification, the generation, the cleanup state and the bounded native observations of the
// fixed plan. It never reports the endpoint, the serial number, any native path, any hash, any
// request text or any raw vendor error string. No raw controller command, no motion command, no
// output control, no configuration write and no discovery exists in this program.

if (args.Length != 0)
{
    Console.Error.WriteLine(
        "WJSS motion observation refused [INVALID_INVOCATION]: this harness takes no arguments. " +
        "Configuration comes only from the Owner-local environment variables named in the runbook.");
    return 4;
}

if (!MotionLibraryConfigurationResolver.TryResolve(
        Environment.GetEnvironmentVariable,
        out var probeConfiguration,
        out var probeRefusalCode,
        out var probeRefusalDetail))
{
    Console.Error.WriteLine($"WJSS motion observation refused [{probeRefusalCode}]: {probeRefusalDetail}");
    return 2;
}

if (!MotionControllerEndpointResolver.TryResolve(
        Environment.GetEnvironmentVariable,
        out var endpointConfiguration,
        out var endpointRefusalCode,
        out var endpointRefusalDetail))
{
    Console.Error.WriteLine($"WJSS motion observation refused [{endpointRefusalCode}]: {endpointRefusalDetail}");
    return 2;
}

var outcome = new MotionObservationTrialSession(probeConfiguration, endpointConfiguration).Run();

var json = JsonSerializer.Serialize(
    new
    {
        accepted = outcome.Accepted,
        code = outcome.Code,
        detail = outcome.Detail,
        expectedLogicalIdentity = outcome.ExpectedLogicalIdentity,
        openClassification = outcome.OpenClassification,
        generation = outcome.Generation,
        observationBoundaryInvalidated = outcome.ObservationBoundaryInvalidated,
        controllerHandleClosed = outcome.ControllerHandleClosed,
        nativeHandlesReleased = outcome.NativeHandlesReleased,
        endpoint = MotionControllerEndpointResolver.DescribeConfiguration(endpointConfiguration),
        observations = outcome.Readings.Select(reading => new
        {
            ordinal = reading.Ordinal,
            code = reading.Code,
            accepted = reading.Accepted,
            generation = reading.Generation,
            kind = reading.State?.Kind,
            axisId = reading.State?.AxisId,
            outputNumber = reading.State?.OutputNumber,
            nativeValueText = reading.State?.NativeValueText,
            nativeUnit = reading.State?.NativeUnit,
            interpretationState = reading.State?.InterpretationState,
            detailCode = reading.State?.DetailCode,
        }),
    },
    new JsonSerializerOptions { WriteIndented = true });

if (outcome.Accepted)
{
    Console.WriteLine(json);
    return 0;
}

Console.Error.WriteLine(json);
return 2;
