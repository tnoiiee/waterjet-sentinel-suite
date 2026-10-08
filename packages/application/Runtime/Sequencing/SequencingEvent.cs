using Wjss.Contracts;

namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// Base of the inputs the sequencing kernel accepts. Every event carries an
/// explicit instant; the kernel never reads a clock. Each event kind is a
/// separate sealed record so that the transition table is exhaustive. There is
/// no external release input: release is the kernel's own Safe Return step SR7.
/// </summary>
public abstract record SequencingEvent;

/// <summary>Starts the AutoSequence from OFF into RUNNING.</summary>
public sealed record StartAutoSequence(DateTimeOffset At) : SequencingEvent;

/// <summary>
/// Admits one explicit synthetic entry at the end of the queue. The
/// <paramref name="SecondsSinceLastClean"/> value is a scenario-supplied
/// presentation field; it is never an admission criterion.
/// </summary>
public sealed record AdmitQueueEntry(
    DateTimeOffset At,
    SequencingAdmissionSource Source,
    string SensorId,
    string SourceReason,
    int SecondsSinceLastClean,
    SequencingTopology Topology) : SequencingEvent;

/// <summary>
/// Dispatches the queue head atomically. <paramref name="Topology"/> is the
/// equipment eligibility snapshot used to revalidate the head. <paramref name="PumpReady"/>
/// is the pump state at dispatch: when false, the new Job waits in the
/// pump-waiting gate and no water begins.
/// </summary>
public sealed record DispatchHead(
    DateTimeOffset At,
    SequencingTopology Topology,
    bool PumpReady) : SequencingEvent;

/// <summary>
/// Records a pump-readiness observation for the Active Job while it is not yet
/// cleaning. During Cleaning a not-ready observation is refused: it must be
/// classified through <see cref="ObservePumpState"/>.
/// </summary>
public sealed record ObservePumpReadiness(DateTimeOffset At, bool Ready) : SequencingEvent;

/// <summary>
/// Observes a synthetic Main Pump classification. UNEXPECTED_STOP and TRIP are
/// critical: they set the latch and, during a Job, begin Mandatory Safe Return in
/// the same transition. READY and EXPECTED_STOP are non-critical.
/// </summary>
public sealed record ObservePumpState(DateTimeOffset At, PumpObservation Observation) : SequencingEvent;

/// <summary>Requests a pause. With an Active Job, the state becomes PAUSE_REQUESTED until that Job is released.</summary>
public sealed record RequestPause(DateTimeOffset At) : SequencingEvent;

/// <summary>Advances a PREPARING Job to READY_TO_CLEAN once the valve is observed CLOSED.</summary>
public sealed record AdvanceJobPreparation(DateTimeOffset At) : SequencingEvent;

/// <summary>Begins Cleaning: requires READY_TO_CLEAN, a ready pump and the valve observed CLOSED.</summary>
public sealed record BeginCleaning(DateTimeOffset At) : SequencingEvent;

/// <summary>Records one verified execution phase. Phases must arrive strictly in order P1 to P6.</summary>
public sealed record ExecutionPhaseVerified(DateTimeOffset At, JobPhase Phase) : SequencingEvent;

/// <summary>Requests normal completion. Allowed only after P6 is verified. Starts Safe Return with pending COMPLETED.</summary>
public sealed record RequestNormalCompletion(DateTimeOffset At) : SequencingEvent;

/// <summary>
/// Synthetic internal abort trigger (O-12). It has no route and no caller in the
/// Product tree in CP-2. Starts Safe Return with pending ABORTED.
/// </summary>
public sealed record RequestAbort(DateTimeOffset At) : SequencingEvent;

/// <summary>
/// Reports an execution failure with a deterministic reason code. Starts Safe
/// Return with pending FAILED.
/// </summary>
public sealed record ReportExecutionFailure(DateTimeOffset At, string ReasonCode) : SequencingEvent;

/// <summary>Observes the two Isolation Valve limits for the named valve.</summary>
public sealed record ValveLimitObserved(
    DateTimeOffset At,
    string ValveId,
    bool UpperLimit,
    bool LowerLimit) : SequencingEvent;

/// <summary>Observes abstract Axis Standby feedback.</summary>
public sealed record AxisFeedbackObserved(DateTimeOffset At, AxisFeedbackState Feedback) : SequencingEvent;

/// <summary>
/// A feedback timeout expired. The timeout is an input, not a kernel timer (O-5).
/// It fails the named wait only if that wait is pending.
/// </summary>
public sealed record FeedbackTimeoutExpired(DateTimeOffset At, FeedbackTarget Target) : SequencingEvent;

/// <summary>The only admission source in Stage 0.4A: explicit scenario-prepared entries.</summary>
public enum SequencingAdmissionSource
{
    SCENARIO_PREPARED,
}
