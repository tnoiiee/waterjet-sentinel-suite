namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// Base of the inputs the sequencing kernel accepts. Every event carries an
/// explicit instant; the kernel never reads a clock. Each event kind is a
/// separate sealed record so that the transition table is exhaustive.
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
/// is the pump state at dispatch: when false, the new Job is created in the
/// pump-waiting gate and no water begins.
/// </summary>
public sealed record DispatchHead(
    DateTimeOffset At,
    SequencingTopology Topology,
    bool PumpReady) : SequencingEvent;

/// <summary>Records a pump-readiness observation for the Active Job.</summary>
public sealed record ObservePumpReadiness(DateTimeOffset At, bool Ready) : SequencingEvent;

/// <summary>
/// Raises the critical suspension gate. No reset event exists in CP-1: the
/// reset and resume rule is OWNER DECISION REQUIRED (D9).
/// </summary>
public sealed record RaiseCriticalSuspension(DateTimeOffset At) : SequencingEvent;

/// <summary>
/// Requests a pause. With an Active Job, the state becomes PAUSE_REQUESTED until
/// that Job is released. With no Job, the state becomes PAUSED directly.
/// </summary>
public sealed record RequestPause(DateTimeOffset At) : SequencingEvent;

/// <summary>
/// Releases the Active Job after its outcome and Mandatory Safe Return have
/// finished. The kernel does not execute Safe Return; it only accepts the
/// completion evidence that a later checkpoint will produce.
/// </summary>
public sealed record ReleaseActiveJob(DateTimeOffset At, SafeReturnReleaseEvidence Evidence) : SequencingEvent;

/// <summary>The only admission source in Stage 0.4A: explicit scenario-prepared entries.</summary>
public enum SequencingAdmissionSource
{
    SCENARIO_PREPARED,
}
