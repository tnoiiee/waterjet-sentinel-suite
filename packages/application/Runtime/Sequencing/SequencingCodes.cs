namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// Stable machine codes for the Stage 0.4A sequencing kernel: refusal codes,
/// applied codes, no-op codes, event kinds, Job triggers, data intents and
/// synthetic identifier prefixes. Codes are identities, not display text. None
/// of them is a Queue-entry state; the GlobalQueue holds dispatch-ready entries
/// only (Owner ruling 2026-10-08). Safe Return step codes are the step names
/// (<c>SR1</c> to <c>SR7</c>), taken from the accepted <c>SafeReturnStep</c> enum.
/// </summary>
public static class SequencingCodes
{
    // Event kinds recorded in the evidence stream.
    public const string KindStartAutoSequence = "START_AUTOSEQUENCE";
    public const string KindAdmitQueueEntry = "ADMIT_QUEUE_ENTRY";
    public const string KindDispatchHead = "DISPATCH_HEAD";
    public const string KindObservePumpReadiness = "OBSERVE_PUMP_READINESS";
    public const string KindObservePumpState = "OBSERVE_PUMP_STATE";
    public const string KindRequestPause = "REQUEST_PAUSE";
    public const string KindAdvanceJobPreparation = "ADVANCE_JOB_PREPARATION";
    public const string KindBeginCleaning = "BEGIN_CLEANING";
    public const string KindExecutionPhaseVerified = "EXECUTION_PHASE_VERIFIED";
    public const string KindRequestNormalCompletion = "REQUEST_NORMAL_COMPLETION";
    public const string KindRequestAbort = "REQUEST_ABORT";
    public const string KindReportExecutionFailure = "REPORT_EXECUTION_FAILURE";
    public const string KindValveLimitObserved = "VALVE_LIMIT_OBSERVED";
    public const string KindAxisFeedbackObserved = "AXIS_FEEDBACK_OBSERVED";
    public const string KindFeedbackTimeoutExpired = "FEEDBACK_TIMEOUT_EXPIRED";

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
    public const string PumpStopRequiresClassification = "PUMP_STOP_REQUIRES_CLASSIFICATION";
    public const string PumpExpectedStopNoted = "PUMP_EXPECTED_STOP_NOTED";
    public const string PumpExpectedStopNotModelled = "PUMP_EXPECTED_STOP_NOT_MODELLED";
    public const string PumpObservationNoted = "PUMP_OBSERVATION_NOTED";
    public const string PumpObservationNotedDuringSafeReturn = "PUMP_OBSERVATION_NOTED_DURING_SAFE_RETURN";

    // Critical latch. It is never cleared by the kernel (no reset in CP-2).
    public const string CriticalSuspensionRaised = "CRITICAL_SUSPENSION_RAISED";
    public const string CriticalAlreadySuspended = "CRITICAL_ALREADY_SUSPENDED";
    public const string CriticalSuspended = "CRITICAL_SUSPENDED";
    public const string CriticalEventDuringSafeReturn = "CRITICAL_EVENT_DURING_SAFE_RETURN";

    // Pause.
    public const string PauseRequested = "PAUSE_REQUESTED";
    public const string Paused = "PAUSED";
    public const string PauseNotRunning = "PAUSE_NOT_RUNNING";
    public const string PauseAlreadyRequested = "PAUSE_ALREADY_REQUESTED";

    // Job preparation, cleaning and phases.
    public const string PreparationAdvanced = "PREPARATION_ADVANCED";
    public const string PreparationAlreadyAdvanced = "PREPARATION_ALREADY_ADVANCED";
    public const string PreparationNotComplete = "PREPARATION_NOT_COMPLETE";
    public const string PumpNotReady = "PUMP_NOT_READY";
    public const string ValveNotClosed = "VALVE_NOT_CLOSED";
    public const string ValveNotOpen = "VALVE_NOT_OPEN";
    public const string CleaningStarted = "CLEANING_STARTED";
    public const string CleaningAlreadyActive = "CLEANING_ALREADY_ACTIVE";
    public const string CleaningNotActive = "CLEANING_NOT_ACTIVE";
    public const string PhaseVerified = "PHASE_VERIFIED";
    public const string PhaseOutOfOrder = "PHASE_OUT_OF_ORDER";
    public const string PhaseIncomplete = "PHASE_INCOMPLETE";
    public const string CompletionRequested = "COMPLETION_REQUESTED";

    // Abort and execution failure.
    public const string AbortRequested = "ABORT_REQUESTED";
    public const string AbortNotedDuringSafeReturn = "ABORT_NOTED_DURING_SAFE_RETURN";
    public const string ExecutionFailureNotedDuringSafeReturn = "EXECUTION_FAILURE_NOTED_DURING_SAFE_RETURN";
    public const string FailureReasonInvalid = "FAILURE_REASON_INVALID";

    // Safe Return waiting, feedback and failure.
    public const string SafeReturnInProgress = "SAFE_RETURN_IN_PROGRESS";
    public const string ValveFeedbackObserved = "VALVE_FEEDBACK_OBSERVED";
    public const string ValveFeedbackUnchanged = "VALVE_FEEDBACK_UNCHANGED";
    public const string ValveFeedbackNoEffect = "VALVE_FEEDBACK_RECORDED_NO_EFFECT";
    public const string ValveIdMismatch = "VALVE_ID_MISMATCH";
    public const string ValveNotConfirmed = "VALVE_NOT_CONFIRMED";
    public const string AxisFeedbackNoEffect = "AXIS_FEEDBACK_RECORDED_NO_EFFECT";
    public const string StandbyNotConfirmed = "STANDBY_NOT_CONFIRMED";
    public const string FeedbackTimeoutNotPending = "FEEDBACK_TIMEOUT_NOT_PENDING";

    // Safe Return failure reasons (carried by SR_FAILED evidence and FailureCode).
    public const string ValveInvalidLimitState = "VALVE_INVALID_LIMIT_STATE";
    public const string ValveCloseNotConfirmed = "VALVE_CLOSE_NOT_CONFIRMED";
    public const string AxisFault = "AXIS_FAULT";
    public const string AxisStandbyNotConfirmedFailure = "AXIS_STANDBY_NOT_CONFIRMED";

    // Job triggers (the reason the Safe Return was entered). Opaque identities.
    public const string TriggerNormalCompletion = "NORMAL_COMPLETION";
    public const string TriggerAbort = "ABORT";
    public const string TriggerExecutionFailure = "EXECUTION_FAILURE";
    public const string TriggerPumpUnexpectedStop = "PUMP_UNEXPECTED_STOP";
    public const string TriggerPumpTrip = "PUMP_TRIP";

    // Intents recorded as pure data in evidence. Never bound to an adapter.
    public const string IntentWaterOutputOn = "WATER_OUTPUT_ON";
    public const string IntentWaterOutputOff = "WATER_OUTPUT_OFF";
    public const string IntentValveClose = "VALVE_CLOSE";
    public const string IntentAxisToStandby = "AXIS_TO_STANDBY";

    // Job release and dispatch-time state.
    public const string JobReleased = "JOB_RELEASED";

    // State integrity (Owner correction 2026-10-08). The refusal code is returned by Apply;
    // projections throw InvalidOperationException whose message starts with StateInvalid.
    public const string StateInvalid = "SEQUENCING_STATE_INVALID";
    public const string CounterNotIncrementable = "COUNTER_NOT_INCREMENTABLE";

    // Synthetic identifier prefixes (deterministic counters; no clock, no random value).
    public const string EntryIdPrefix = "SYN-QE-";
    public const string JobIdPrefix = "SYN-JOB-";
    public const string DispatchIdPrefix = "SYN-DSP-";

    /// <summary>True for the five accepted trigger identities.</summary>
    public static bool IsKnownTrigger(string trigger) =>
        trigger is TriggerNormalCompletion or TriggerAbort or TriggerExecutionFailure or TriggerPumpUnexpectedStop or TriggerPumpTrip;

    /// <summary>True for a critical Pump trigger.</summary>
    public static bool IsPumpTrigger(string trigger) =>
        trigger is TriggerPumpUnexpectedStop or TriggerPumpTrip;
}
