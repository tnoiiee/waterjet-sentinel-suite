namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// Stable machine codes for the Stage 0.4A sequencing kernel: refusal codes,
/// applied codes, no-op codes, event kinds and synthetic identifier prefixes.
/// Codes are identities, not display text. None of them is a Queue-entry state;
/// the GlobalQueue holds dispatch-ready entries only (Owner ruling 2026-10-08).
/// </summary>
public static class SequencingCodes
{
    // Event kinds recorded in the evidence stream.
    public const string KindStartAutoSequence = "START_AUTOSEQUENCE";
    public const string KindAdmitQueueEntry = "ADMIT_QUEUE_ENTRY";
    public const string KindDispatchHead = "DISPATCH_HEAD";
    public const string KindObservePumpReadiness = "OBSERVE_PUMP_READINESS";
    public const string KindRaiseCriticalSuspension = "RAISE_CRITICAL_SUSPENSION";
    public const string KindRequestPause = "REQUEST_PAUSE";
    public const string KindReleaseActiveJob = "RELEASE_ACTIVE_JOB";

    // AutoSequence start.
    public const string AutoSequenceStarted = "AUTOSEQUENCE_STARTED";
    public const string StartNotOff = "START_NOT_OFF";

    // Admission (explicit synthetic scenario-prepared entries only).
    public const string Admitted = "ADMITTED";
    public const string AdmissionDuplicateNoOp = "ADMISSION_DUPLICATE_NOOP";
    public const string QueueFull = "QUEUE_FULL";
    public const string TargetActive = "TARGET_ACTIVE";
    public const string AdmissionSourceNotScenario = "ADMISSION_SOURCE_NOT_SCENARIO";
    public const string SensorUnknown = "SENSOR_UNKNOWN";
    public const string NotASensor = "NOT_A_SENSOR";
    public const string EntryInvalid = "ENTRY_INVALID";

    // Head dispatch and head revalidation.
    public const string Dispatched = "DISPATCHED";
    public const string RemovedByEligibility = "REMOVED_BY_ELIGIBILITY";
    public const string DispatchRefusedQueueEmpty = "DISPATCH_REFUSED_QUEUE_EMPTY";
    public const string DispatchRefusedJobActive = "DISPATCH_REFUSED_JOB_ACTIVE";
    public const string DispatchRefusedPauseRequested = "DISPATCH_REFUSED_PAUSE_REQUESTED";
    public const string DispatchRefusedNotRunning = "DISPATCH_REFUSED_NOT_RUNNING";

    // Pump readiness observed by the Active Job (waiting gate, not a Queue state).
    public const string PumpReadinessChanged = "PUMP_READINESS_CHANGED";
    public const string PumpReadinessUnchanged = "PUMP_READINESS_UNCHANGED";
    public const string NoActiveJob = "NO_ACTIVE_JOB";

    // Critical suspension (raise only in CP-1; reset is OWNER DECISION REQUIRED, D9).
    public const string CriticalSuspensionRaised = "CRITICAL_SUSPENSION_RAISED";
    public const string CriticalAlreadySuspended = "CRITICAL_ALREADY_SUSPENDED";
    public const string CriticalSuspended = "CRITICAL_SUSPENDED";

    // Pause and Job release.
    public const string PauseRequested = "PAUSE_REQUESTED";
    public const string Paused = "PAUSED";
    public const string PauseNotRunning = "PAUSE_NOT_RUNNING";
    public const string PauseAlreadyRequested = "PAUSE_ALREADY_REQUESTED";
    public const string JobReleased = "JOB_RELEASED";
    public const string SafeReturnNotComplete = "SAFE_RETURN_NOT_COMPLETE";
    public const string ReleaseJobMismatch = "RELEASE_JOB_MISMATCH";
    public const string ReleaseSeqNotAfterDispatch = "RELEASE_SEQ_NOT_AFTER_DISPATCH";

    // Synthetic identifier prefixes (deterministic counters; no clock, no random value).
    public const string EntryIdPrefix = "SYN-QE-";
    public const string JobIdPrefix = "SYN-JOB-";
    public const string DispatchIdPrefix = "SYN-DSP-";
}
