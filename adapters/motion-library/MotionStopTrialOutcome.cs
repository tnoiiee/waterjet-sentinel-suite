namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// Fixed vocabulary and invocation shape for the isolated Owner-invoked C1 Stop trial.
/// Only these compile-time values may appear in a trial report; local endpoint, native identity
/// values, consent text and native/vendor detail are never report data.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal static class MotionStopTrialVocabulary
{
    internal const string ModeToken = "no-load-stop-trial";
    internal const string ConsentVariable = "WJSS_MOTION_STOP_TRIAL_CONSENT";
    internal const string ExactConsentValue = "AUTHORIZE_ONE_NO_LOAD_STOP_TRIAL";

    internal const string NotEvaluated = "NOT_EVALUATED";
    internal const string PreconditionAccepted = "PRECONDITION_ACCEPTED";
    internal const string PreconditionRefused = "PRECONDITION_REFUSED";
    internal const string OwnerConsentAccepted = "OWNER_CONSENT_ACCEPTED";
    internal const string OwnerConsentNotConfigured = "OWNER_CONSENT_NOT_CONFIGURED";
    internal const string OwnerConsentRejected = "OWNER_CONSENT_REJECTED";
    internal const string OwnerConsentConsumptionFailed = "OWNER_CONSENT_CONSUMPTION_FAILED";
    internal const string NativeProofNotAttempted = "NATIVE_PROOF_NOT_ATTEMPTED";
    internal const string NativeProofAccepted = "NATIVE_PROOF_ACCEPTED";
    internal const string NativeProofRefused = "NATIVE_PROOF_REFUSED";
    internal const string OpenNotAttempted = "OPEN_NOT_ATTEMPTED";
    internal const string OpenSucceeded = "GCLIB_SUCCESS";
    internal const string StopNotDispatched = "STOP_NOT_DISPATCHED";
    internal const string CleanupComplete = "CLEANUP_COMPLETE";
    internal const string CleanupIncomplete = "CLEANUP_INCOMPLETE";

    internal const string InvalidInvocation = "INVALID_INVOCATION";
    internal const string PreconditionEvaluationFailed = "TRIAL_PRECONDITION_EVALUATION_FAILED";
    internal const string TrialAlreadyRun = "STOP_TRIAL_ALREADY_RUN";
    internal const string TrialAborted = "STOP_TRIAL_ABORTED";
}

/// <summary>
/// Redacted, deterministic lifecycle state for the Owner-invoked Stop trial. It has no public
/// constructor or public members, carries no endpoint/native configuration/detail, and cannot
/// report a Stop as confirmed. A dispatched Stop is never represented as stopped, safe or
/// commissioned.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal sealed class MotionStopTrialOutcome
{
    private MotionStopTrialOutcome()
    {
        // Confirmation is not observed in this scope and is never inferred from dispatch.
        StopConfirmed = false;
    }

    internal string PreconditionCode { get; set; } = MotionStopTrialVocabulary.PreconditionRefused;
    internal string RefusalCode { get; set; } = string.Empty;
    internal string ConsentCode { get; set; } = MotionStopTrialVocabulary.NotEvaluated;
    internal string EndpointCode { get; set; } = MotionStopTrialVocabulary.NotEvaluated;
    internal string NativeConfigurationCode { get; set; } = MotionStopTrialVocabulary.NotEvaluated;
    internal string NativeProofCode { get; set; } = MotionStopTrialVocabulary.NativeProofNotAttempted;
    internal string? Axis { get; set; }
    internal bool OpenAttempted { get; set; }
    internal string OpenClassification { get; set; } = MotionStopTrialVocabulary.OpenNotAttempted;
    internal string StopCode { get; set; } = MotionStopTrialVocabulary.StopNotDispatched;
    internal bool NativeDispatchAttempted { get; set; }
    internal bool StopDispatched { get; set; }
    internal bool StopConfirmed { get; }
    internal string CleanupCode { get; set; } = MotionStopTrialVocabulary.CleanupComplete;
    internal string CleanupFailureCode { get; set; } = string.Empty;
    internal bool StopSessionInvalidated { get; set; } = true;
    internal bool ControllerHandleClosed { get; set; } = true;
    internal bool NativeModulesReleased { get; set; } = true;
    internal int ExitCode { get; set; } = 2;

    internal static MotionStopTrialOutcome RefusedBeforeNative(
        string refusalCode,
        string consentCode = MotionStopTrialVocabulary.NotEvaluated,
        string endpointCode = MotionStopTrialVocabulary.NotEvaluated,
        string nativeConfigurationCode = MotionStopTrialVocabulary.NotEvaluated,
        string? axis = null,
        int exitCode = 2) => new()
    {
        PreconditionCode = MotionStopTrialVocabulary.PreconditionRefused,
        RefusalCode = refusalCode,
        ConsentCode = consentCode,
        EndpointCode = endpointCode,
        NativeConfigurationCode = nativeConfigurationCode,
        NativeProofCode = MotionStopTrialVocabulary.NativeProofNotAttempted,
        Axis = axis,
        OpenAttempted = false,
        OpenClassification = MotionStopTrialVocabulary.OpenNotAttempted,
        StopCode = MotionStopTrialVocabulary.StopNotDispatched,
        NativeDispatchAttempted = false,
        StopDispatched = false,
        CleanupCode = MotionStopTrialVocabulary.CleanupComplete,
        CleanupFailureCode = string.Empty,
        StopSessionInvalidated = true,
        ControllerHandleClosed = true,
        NativeModulesReleased = true,
        ExitCode = exitCode,
    };

    internal static MotionStopTrialOutcome AcceptedPreconditions(MotionStopAxis axis) => new()
    {
        PreconditionCode = MotionStopTrialVocabulary.PreconditionAccepted,
        RefusalCode = string.Empty,
        ConsentCode = MotionStopTrialVocabulary.OwnerConsentAccepted,
        EndpointCode = MotionControllerIdentityVocabulary.EndpointConfigured,
        NativeConfigurationCode = MotionLibraryProbeVocabulary.ConfigurationRedacted,
        NativeProofCode = MotionStopTrialVocabulary.NativeProofNotAttempted,
        Axis = axis switch
        {
            MotionStopAxis.A => "A",
            MotionStopAxis.B => "B",
            _ => null,
        },
        OpenAttempted = false,
        OpenClassification = MotionStopTrialVocabulary.OpenNotAttempted,
        StopCode = MotionStopTrialVocabulary.StopNotDispatched,
        NativeDispatchAttempted = false,
        StopDispatched = false,
        CleanupCode = MotionStopTrialVocabulary.CleanupComplete,
        CleanupFailureCode = string.Empty,
        StopSessionInvalidated = true,
        ControllerHandleClosed = true,
        NativeModulesReleased = true,
        ExitCode = 2,
    };
}
