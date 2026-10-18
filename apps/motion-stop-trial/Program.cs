using System.Text.Json;
using Wjss.Adapters.MotionLibrary;

// Owner-invoked, single-axis C1 Stop trial harness. Invocation and local consent are checked
// inside the adapter before any native load. This program emits only canonical redacted fields;
// a dispatch is not confirmation that the axis stopped, and it says nothing about safety or
// commissioning.
var outcome = new MotionStopTrialInvocation().Run(args);

var json = JsonSerializer.Serialize(
    new
    {
        preconditionCode = outcome.PreconditionCode,
        refusalCode = string.IsNullOrEmpty(outcome.RefusalCode) ? null : outcome.RefusalCode,
        consentCode = outcome.ConsentCode,
        endpointCode = outcome.EndpointCode,
        nativeConfigurationCode = outcome.NativeConfigurationCode,
        nativeProofCode = outcome.NativeProofCode,
        axis = outcome.Axis,
        openAttempted = outcome.OpenAttempted,
        openClassification = outcome.OpenClassification,
        stopCode = outcome.StopCode,
        nativeDispatchAttempted = outcome.NativeDispatchAttempted,
        stopDispatched = outcome.StopDispatched,
        stopConfirmed = outcome.StopConfirmed,
        cleanupCode = outcome.CleanupCode,
        cleanupFailureCode = string.IsNullOrEmpty(outcome.CleanupFailureCode)
            ? null
            : outcome.CleanupFailureCode,
        stopSessionInvalidated = outcome.StopSessionInvalidated,
        controllerHandleClosed = outcome.ControllerHandleClosed,
        nativeModulesReleased = outcome.NativeModulesReleased,
        exitCode = outcome.ExitCode,
    },
    new JsonSerializerOptions { WriteIndented = true });

Console.WriteLine(json);
return outcome.ExitCode;
