using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.Simulator;

/// <summary>
/// The eleven Stage 0.4A CP-3b SIMULATOR scenarios. Selection is by exact name only (see
/// <see cref="SimulatorScenarioCatalogue.TryParse"/>). There is no random choice and no operator command surface.
/// </summary>
public enum SimulatorScenarioId
{
    IDLE,
    NORMAL_COMPLETION,
    PUMP_WAIT_THEN_READY,
    EXPLICIT_ABORT,
    EXECUTION_FAILURE,
    PUMP_UNEXPECTED_STOP,
    PUMP_TRIP,
    VALVE_CLOSE_FAILURE,
    AXIS_STANDBY_FAILURE,
    PAUSE_AFTER_CURRENT_JOB,
    QUEUE_CAPACITY_AND_FIFO,
}

/// <summary>
/// One scenario: its identity, a one-line summary, and the kernel event schedule. Each event instant is
/// <see cref="SimulatorScenarioCatalogue.Epoch"/> plus the scheduled tick in whole seconds.
/// </summary>
public sealed record SimulatorScenario(SimulatorScenarioId Id, string Summary, IReadOnlyList<SequencingEvent> Events);

/// <summary>
/// Deterministic scenario schedules built only from synthetic identities (SYN-*). No clock is read: every
/// instant is the fixed <see cref="Epoch"/> plus a tick. Equipment identities are the SIMULATOR pairing used by
/// the kernel tests (SYN-S01 is WJ1 with IV1). Binding these identities to the SIMULATOR Sensor map is deferred
/// to CP-3c and is recorded as [OPEN].
/// </summary>
public static class SimulatorScenarioCatalogue
{
    /// <summary>The fixed scenario epoch. It is not the composition clock.</summary>
    public static readonly DateTimeOffset Epoch = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The admission source label used by every scenario entry.</summary>
    public const string AdmissionReason = "SCENARIO_PREPARED";

    /// <summary>The Isolation Valve of SYN-S01, the Sensor used by the single-Job scenarios.</summary>
    public const string PrimaryValveId = "IV1";

    private static readonly string[] SensorIds =
        ["SYN-S01", "SYN-S02", "SYN-S03", "SYN-S04", "SYN-S05", "SYN-S06", "SYN-S07", "SYN-S08", "SYN-S09", "SYN-S10"];

    private static readonly string[] NonSensorIds = ["SYN-G01"];

    /// <summary>The synthetic equipment topology: Sensor n is paired WJ(n mod 8 + 1) with IV(n mod 8 + 1).</summary>
    public static SequencingTopology Topology { get; } = BuildTopology();

    /// <summary>Every scenario, in catalogue order.</summary>
    public static IReadOnlyList<SimulatorScenario> All { get; } = BuildAll();

    /// <summary>Returns the scenario with the given identity.</summary>
    public static SimulatorScenario Get(SimulatorScenarioId id)
    {
        foreach (var scenario in All)
        {
            if (scenario.Id == id)
            {
                return scenario;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown SIMULATOR scenario.");
    }

    /// <summary>
    /// Matches a scenario by its exact identity name (case-sensitive). Numeric text, blank text and null never
    /// match.
    /// </summary>
    public static bool TryParse(string? name, out SimulatorScenarioId id)
    {
        foreach (var candidate in Enum.GetValues<SimulatorScenarioId>())
        {
            if (string.Equals(candidate.ToString(), name, StringComparison.Ordinal))
            {
                id = candidate;
                return true;
            }
        }

        id = SimulatorScenarioId.IDLE;
        return false;
    }

    /// <summary>The instant of a tick: the fixed epoch plus whole seconds.</summary>
    public static DateTimeOffset AtTick(int tick) => Epoch.AddSeconds(tick);

    private static SequencingTopology BuildTopology()
    {
        var assignments = new List<SequencingSensorAssignment>(SensorIds.Length);
        for (var index = 0; index < SensorIds.Length; index++)
        {
            var ordinal = (index % 8 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            assignments.Add(new SequencingSensorAssignment(SensorIds[index], "WJ" + ordinal, "IV" + ordinal));
        }

        return new SequencingTopology(assignments, NonSensorIds);
    }

    private static IReadOnlyList<SimulatorScenario> BuildAll() => new List<SimulatorScenario>
    {
        new SimulatorScenario(SimulatorScenarioId.IDLE, "No events. Initial OFF state, empty queue, revision 0.", Idle().AsReadOnly()),
        new SimulatorScenario(SimulatorScenarioId.NORMAL_COMPLETION, "Dispatch, clean, verify P1 to P6, complete, close, return to standby, release.", NormalCompletion().AsReadOnly()),
        new SimulatorScenario(SimulatorScenarioId.PUMP_WAIT_THEN_READY, "Dispatch with the pump waiting; ready at tick 4; then the normal path.", PumpWaitThenReady().AsReadOnly()),
        new SimulatorScenario(SimulatorScenarioId.EXPLICIT_ABORT, "Abort during cleaning; Safe Return; release as ABORTED.", ExplicitAbort().AsReadOnly()),
        new SimulatorScenario(SimulatorScenarioId.EXECUTION_FAILURE, "Execution failure during cleaning; Safe Return; release as FAILED.", ExecutionFailure().AsReadOnly()),
        new SimulatorScenario(SimulatorScenarioId.PUMP_UNEXPECTED_STOP, "Pump UNEXPECTED_STOP during cleaning; latch; Safe Return; release as ABORTED.", PumpCritical(PumpObservation.UNEXPECTED_STOP).AsReadOnly()),
        new SimulatorScenario(SimulatorScenarioId.PUMP_TRIP, "Pump TRIP during cleaning; latch; Safe Return; release as ABORTED.", PumpCritical(PumpObservation.TRIP).AsReadOnly()),
        new SimulatorScenario(SimulatorScenarioId.VALVE_CLOSE_FAILURE, "Valve close not confirmed in time; Safe Return FAILED; Job retained.", ValveCloseFailure().AsReadOnly()),
        new SimulatorScenario(SimulatorScenarioId.AXIS_STANDBY_FAILURE, "Axis reports FAULT while awaiting Standby; Safe Return FAILED; Job retained.", AxisStandbyFailure().AsReadOnly()),
        new SimulatorScenario(SimulatorScenarioId.PAUSE_AFTER_CURRENT_JOB, "Pause requested with a Job; the Job completes; release to PAUSED; queue kept.", PauseAfterCurrentJob().AsReadOnly()),
        new SimulatorScenario(SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO, "Eight admissions fill the queue; the ninth is refused; head-only FIFO dispatch.", QueueCapacityAndFifo().AsReadOnly()),
    };

    private static List<SequencingEvent> Idle() => new();

    private static List<SequencingEvent> NormalCompletion() =>
        new()
        {
            Start(0), Admit(1, "SYN-S01"), Admit(2, "SYN-S02"), Dispatch(3, pumpReady: true),
            Closed(4), Advance(5), Begin(6), Phase(7, JobPhase.P1), Open(8),
            Phase(9, JobPhase.P2), Phase(10, JobPhase.P3), Phase(11, JobPhase.P4), Phase(12, JobPhase.P5), Phase(13, JobPhase.P6),
            Complete(14), Closed(15), Standby(16),
        };

    private static List<SequencingEvent> PumpWaitThenReady() =>
        new()
        {
            Start(0), Admit(1, "SYN-S01"), Admit(2, "SYN-S02"), Dispatch(3, pumpReady: false),
            new ObservePumpReadiness(AtTick(4), Ready: true),
            Closed(5), Advance(6), Begin(7), Phase(8, JobPhase.P1), Open(9),
            Phase(10, JobPhase.P2), Phase(11, JobPhase.P3), Phase(12, JobPhase.P4), Phase(13, JobPhase.P5), Phase(14, JobPhase.P6),
            Complete(15), Closed(16), Standby(17),
        };

    private static List<SequencingEvent> ExplicitAbort() =>
        new()
        {
            Start(0), Admit(1, "SYN-S01"), Admit(2, "SYN-S02"), Dispatch(3, pumpReady: true),
            Closed(4), Advance(5), Begin(6), new RequestAbort(AtTick(7)), Closed(8), Standby(9),
        };

    private static List<SequencingEvent> ExecutionFailure() =>
        new()
        {
            Start(0), Admit(1, "SYN-S01"), Admit(2, "SYN-S02"), Dispatch(3, pumpReady: true),
            Closed(4), Advance(5), Begin(6), new ReportExecutionFailure(AtTick(7), "SYN-FAULT-1"), Closed(8), Standby(9),
        };

    private static List<SequencingEvent> PumpCritical(PumpObservation observation) =>
        new()
        {
            Start(0), Admit(1, "SYN-S01"), Admit(2, "SYN-S02"), Dispatch(3, pumpReady: true),
            Closed(4), Advance(5), Begin(6), new ObservePumpState(AtTick(7), observation), Closed(8), Standby(9),
        };

    private static List<SequencingEvent> ValveCloseFailure() =>
        new()
        {
            Start(0), Admit(1, "SYN-S01"), Admit(2, "SYN-S02"), Dispatch(3, pumpReady: true),
            Closed(4), Advance(5), Begin(6), new RequestAbort(AtTick(7)),
            new FeedbackTimeoutExpired(AtTick(8), FeedbackTarget.VALVE_CLOSED),
        };

    private static List<SequencingEvent> AxisStandbyFailure() =>
        new()
        {
            Start(0), Admit(1, "SYN-S01"), Admit(2, "SYN-S02"), Dispatch(3, pumpReady: true),
            Closed(4), Advance(5), Begin(6), new RequestAbort(AtTick(7)), Closed(8),
            new AxisFeedbackObserved(AtTick(9), AxisFeedbackState.FAULT),
        };

    private static List<SequencingEvent> PauseAfterCurrentJob() =>
        new()
        {
            Start(0), Admit(1, "SYN-S01"), Admit(2, "SYN-S02"), Dispatch(3, pumpReady: true),
            new RequestPause(AtTick(4)),
            Closed(5), Advance(6), Begin(7), Phase(8, JobPhase.P1), Open(9),
            Phase(10, JobPhase.P2), Phase(11, JobPhase.P3), Phase(12, JobPhase.P4), Phase(13, JobPhase.P5), Phase(14, JobPhase.P6),
            Complete(15), Closed(16), Standby(17),
        };

    private static List<SequencingEvent> QueueCapacityAndFifo()
    {
        var events = new List<SequencingEvent> { Start(0) };
        for (var index = 1; index <= QueueSummary.MaxEntries; index++)
        {
            events.Add(Admit(index, SensorIds[index - 1]));
        }

        events.Add(Admit(QueueSummary.MaxEntries + 1, SensorIds[QueueSummary.MaxEntries]));
        events.Add(Dispatch(QueueSummary.MaxEntries + 2, pumpReady: true));
        return events;
    }

    private static SequencingEvent Start(int tick) => new StartAutoSequence(AtTick(tick));

    private static SequencingEvent Admit(int tick, string sensorId) =>
        new AdmitQueueEntry(AtTick(tick), SequencingAdmissionSource.SCENARIO_PREPARED, sensorId, AdmissionReason, 0, Topology);

    private static SequencingEvent Dispatch(int tick, bool pumpReady) => new DispatchHead(AtTick(tick), Topology, pumpReady);

    private static SequencingEvent Advance(int tick) => new AdvanceJobPreparation(AtTick(tick));

    private static SequencingEvent Begin(int tick) => new BeginCleaning(AtTick(tick));

    private static SequencingEvent Phase(int tick, JobPhase phase) => new ExecutionPhaseVerified(AtTick(tick), phase);

    private static SequencingEvent Complete(int tick) => new RequestNormalCompletion(AtTick(tick));

    private static SequencingEvent Open(int tick) => new ValveLimitObserved(AtTick(tick), PrimaryValveId, UpperLimit: true, LowerLimit: false);

    private static SequencingEvent Closed(int tick) => new ValveLimitObserved(AtTick(tick), PrimaryValveId, UpperLimit: false, LowerLimit: true);

    private static SequencingEvent Standby(int tick) => new AxisFeedbackObserved(AtTick(tick), AxisFeedbackState.AT_STANDBY);
}
