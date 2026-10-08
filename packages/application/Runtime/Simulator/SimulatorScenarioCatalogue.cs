using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.Simulator;

/// <summary>
/// The eleven Stage 0.4A SIMULATOR scenarios. Selection is by exact name only (see
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

/// <summary>One kernel event scheduled at a deterministic tick: its instant is the fixed epoch plus the tick in seconds.</summary>
public sealed record SimulatorScenarioStep(int Tick, SequencingEvent Event);

/// <summary>One scenario: its identity, a one-line summary, and its ordered schedule.</summary>
public sealed record SimulatorScenario(SimulatorScenarioId Id, string Summary, IReadOnlyList<SimulatorScenarioStep> Steps);

/// <summary>
/// The scenarios bound to one canonical Runtime Sensor set. <see cref="Sensors"/> is that set in ScanOrder, exactly as
/// supplied by the caller (for the SIMULATOR, the output of SyntheticSensorMap.BuildInitialSensors). <see cref="Topology"/>
/// is built from the same records, so every admitted Sensor and its WJn/IVn pairing come from the canonical map.
/// </summary>
public sealed record SimulatorScenarioSet
{
    /// <summary>The canonical Sensor records in ScanOrder. Every scenario Sensor is one of these.</summary>
    public required IReadOnlyList<SensorPresentationState> Sensors { get; init; }

    /// <summary>The kernel topology derived from <see cref="Sensors"/>: each Sensor with its assigned WJn and IVn.</summary>
    public required SequencingTopology Topology { get; init; }

    /// <summary>The eleven scenarios, in catalogue order.</summary>
    public required IReadOnlyList<SimulatorScenario> Scenarios { get; init; }

    /// <summary>Returns the scenario with the given identity.</summary>
    public SimulatorScenario Get(SimulatorScenarioId id)
    {
        foreach (var scenario in Scenarios)
        {
            if (scenario.Id == id)
            {
                return scenario;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown SIMULATOR scenario.");
    }
}

/// <summary>
/// Deterministic scenario schedules. No clock is read: every instant is <see cref="Epoch"/> plus a tick. The Sensors
/// come from the canonical Runtime set passed to <see cref="Create"/>: the primary target is the first Sensor in
/// ScanOrder, the second scenario Sensor is the next one, and the capacity scenario uses the first nine.
/// </summary>
public static class SimulatorScenarioCatalogue
{
    /// <summary>The fixed scenario epoch. It is not the composition clock.</summary>
    public static readonly DateTimeOffset Epoch = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The admission source label used by every scenario entry.</summary>
    public const string AdmissionReason = "SCENARIO_PREPARED";

    /// <summary>The number of distinct Sensors the capacity scenario needs: a full queue of eight, plus one refused entry.</summary>
    public const int RequiredSensorCount = QueueSummary.MaxEntries + 1;

    /// <summary>Returns the scenarios bound to the canonical Sensor set.</summary>
    public static SimulatorScenarioSet Create(IReadOnlyList<SensorPresentationState> canonicalSensors)
    {
        ArgumentNullException.ThrowIfNull(canonicalSensors);

        var ordered = canonicalSensors.OrderBy(sensor => sensor.ScanOrder).ToArray();
        if (ordered.Length < RequiredSensorCount)
        {
            throw new ArgumentException(
                $"The canonical Sensor set must hold at least {RequiredSensorCount} Sensors; got {ordered.Length}.",
                nameof(canonicalSensors));
        }

        var topology = new SequencingTopology(
            ordered
                .Select(sensor => new SequencingSensorAssignment(sensor.SensorId, sensor.AssignedWaterJetId, sensor.AssignedIsolationValveId))
                .ToArray(),
            Array.Empty<string>());

        var plan = new Plan(topology, ordered);
        return new SimulatorScenarioSet
        {
            Sensors = Array.AsReadOnly(ordered),
            Topology = topology,
            Scenarios = BuildAll(plan).AsReadOnly(),
        };
    }

    /// <summary>
    /// Matches a scenario by its exact identity name (case-sensitive). Numeric text, blank text and null never match.
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

    private sealed record Plan(SequencingTopology Topology, SensorPresentationState[] Ordered)
    {
        public SensorPresentationState Primary => Ordered[0];

        public SensorPresentationState Second => Ordered[1];

        public string ValveId => Primary.AssignedIsolationValveId;
    }

    private static List<SimulatorScenario> BuildAll(Plan plan) => new()
    {
        new(SimulatorScenarioId.IDLE, "No events. Initial OFF state, empty queue, revision 0.", new List<SimulatorScenarioStep>().AsReadOnly()),
        new(SimulatorScenarioId.NORMAL_COMPLETION, "Dispatch, clean, verify P1 to P6, complete, close, return to standby, release.", NormalCompletion(plan).AsReadOnly()),
        new(SimulatorScenarioId.PUMP_WAIT_THEN_READY, "Dispatch with the pump waiting; ready at tick 4; then the normal path.", PumpWaitThenReady(plan).AsReadOnly()),
        new(SimulatorScenarioId.EXPLICIT_ABORT, "Abort during cleaning; Safe Return; release as ABORTED.", ExplicitAbort(plan).AsReadOnly()),
        new(SimulatorScenarioId.EXECUTION_FAILURE, "Execution failure during cleaning; Safe Return; release as FAILED.", ExecutionFailure(plan).AsReadOnly()),
        new(SimulatorScenarioId.PUMP_UNEXPECTED_STOP, "Pump UNEXPECTED_STOP during cleaning; latch; Safe Return; release as ABORTED.", PumpCritical(plan, PumpObservation.UNEXPECTED_STOP).AsReadOnly()),
        new(SimulatorScenarioId.PUMP_TRIP, "Pump TRIP during cleaning; latch; Safe Return; release as ABORTED.", PumpCritical(plan, PumpObservation.TRIP).AsReadOnly()),
        new(SimulatorScenarioId.VALVE_CLOSE_FAILURE, "Valve close not confirmed in time; Safe Return FAILED; Job retained.", ValveCloseFailure(plan).AsReadOnly()),
        new(SimulatorScenarioId.AXIS_STANDBY_FAILURE, "Axis reports FAULT while awaiting Standby; Safe Return FAILED; Job retained.", AxisStandbyFailure(plan).AsReadOnly()),
        new(SimulatorScenarioId.PAUSE_AFTER_CURRENT_JOB, "Pause requested with a Job; the Job completes; release to PAUSED; queue kept.", PauseAfterCurrentJob(plan).AsReadOnly()),
        new(SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO, "Eight admissions fill the queue; the ninth is refused; head-only FIFO dispatch.", QueueCapacityAndFifo(plan).AsReadOnly()),
    };

    private static List<SimulatorScenarioStep> NormalCompletion(Plan plan) =>
        new()
        {
            Start(0), Admit(plan, 1, plan.Primary), Admit(plan, 2, plan.Second), Dispatch(plan, 3, pumpReady: true),
            Closed(plan, 4), Advance(5), Begin(6), Phase(7, JobPhase.P1), Open(plan, 8),
            Phase(9, JobPhase.P2), Phase(10, JobPhase.P3), Phase(11, JobPhase.P4), Phase(12, JobPhase.P5), Phase(13, JobPhase.P6),
            Complete(14), Closed(plan, 15), Standby(16),
        };

    private static List<SimulatorScenarioStep> PumpWaitThenReady(Plan plan) =>
        new()
        {
            Start(0), Admit(plan, 1, plan.Primary), Admit(plan, 2, plan.Second), Dispatch(plan, 3, pumpReady: false),
            Readiness(4, ready: true),
            Closed(plan, 5), Advance(6), Begin(7), Phase(8, JobPhase.P1), Open(plan, 9),
            Phase(10, JobPhase.P2), Phase(11, JobPhase.P3), Phase(12, JobPhase.P4), Phase(13, JobPhase.P5), Phase(14, JobPhase.P6),
            Complete(15), Closed(plan, 16), Standby(17),
        };

    private static List<SimulatorScenarioStep> ExplicitAbort(Plan plan) =>
        new()
        {
            Start(0), Admit(plan, 1, plan.Primary), Admit(plan, 2, plan.Second), Dispatch(plan, 3, pumpReady: true),
            Closed(plan, 4), Advance(5), Begin(6), Abort(7), Closed(plan, 8), Standby(9),
        };

    private static List<SimulatorScenarioStep> ExecutionFailure(Plan plan) =>
        new()
        {
            Start(0), Admit(plan, 1, plan.Primary), Admit(plan, 2, plan.Second), Dispatch(plan, 3, pumpReady: true),
            Closed(plan, 4), Advance(5), Begin(6), Fail(7, "SYN-FAULT-1"), Closed(plan, 8), Standby(9),
        };

    private static List<SimulatorScenarioStep> PumpCritical(Plan plan, PumpObservation observation) =>
        new()
        {
            Start(0), Admit(plan, 1, plan.Primary), Admit(plan, 2, plan.Second), Dispatch(plan, 3, pumpReady: true),
            Closed(plan, 4), Advance(5), Begin(6), Pump(7, observation), Closed(plan, 8), Standby(9),
        };

    private static List<SimulatorScenarioStep> ValveCloseFailure(Plan plan) =>
        new()
        {
            Start(0), Admit(plan, 1, plan.Primary), Admit(plan, 2, plan.Second), Dispatch(plan, 3, pumpReady: true),
            Closed(plan, 4), Advance(5), Begin(6), Abort(7), ValveTimeout(8),
        };

    private static List<SimulatorScenarioStep> AxisStandbyFailure(Plan plan) =>
        new()
        {
            Start(0), Admit(plan, 1, plan.Primary), Admit(plan, 2, plan.Second), Dispatch(plan, 3, pumpReady: true),
            Closed(plan, 4), Advance(5), Begin(6), Abort(7), Closed(plan, 8), AxisFault(9),
        };

    private static List<SimulatorScenarioStep> PauseAfterCurrentJob(Plan plan) =>
        new()
        {
            Start(0), Admit(plan, 1, plan.Primary), Admit(plan, 2, plan.Second), Dispatch(plan, 3, pumpReady: true),
            Pause(4),
            Closed(plan, 5), Advance(6), Begin(7), Phase(8, JobPhase.P1), Open(plan, 9),
            Phase(10, JobPhase.P2), Phase(11, JobPhase.P3), Phase(12, JobPhase.P4), Phase(13, JobPhase.P5), Phase(14, JobPhase.P6),
            Complete(15), Closed(plan, 16), Standby(17),
        };

    private static List<SimulatorScenarioStep> QueueCapacityAndFifo(Plan plan)
    {
        var steps = new List<SimulatorScenarioStep> { Start(0) };
        for (var index = 0; index < QueueSummary.MaxEntries; index++)
        {
            steps.Add(Admit(plan, index + 1, plan.Ordered[index]));
        }

        steps.Add(Admit(plan, QueueSummary.MaxEntries + 1, plan.Ordered[QueueSummary.MaxEntries]));
        steps.Add(Dispatch(plan, QueueSummary.MaxEntries + 2, pumpReady: true));
        return steps;
    }

    private static SimulatorScenarioStep Start(int tick) => new(tick, new StartAutoSequence(AtTick(tick)));

    private static SimulatorScenarioStep Admit(Plan plan, int tick, SensorPresentationState sensor) =>
        new(tick, new AdmitQueueEntry(AtTick(tick), SequencingAdmissionSource.SCENARIO_PREPARED, sensor.SensorId, AdmissionReason, 0, plan.Topology));

    private static SimulatorScenarioStep Dispatch(Plan plan, int tick, bool pumpReady) =>
        new(tick, new DispatchHead(AtTick(tick), plan.Topology, pumpReady));

    private static SimulatorScenarioStep Readiness(int tick, bool ready) =>
        new(tick, new ObservePumpReadiness(AtTick(tick), ready));

    private static SimulatorScenarioStep Pump(int tick, PumpObservation observation) =>
        new(tick, new ObservePumpState(AtTick(tick), observation));

    private static SimulatorScenarioStep Pause(int tick) => new(tick, new RequestPause(AtTick(tick)));

    private static SimulatorScenarioStep Advance(int tick) => new(tick, new AdvanceJobPreparation(AtTick(tick)));

    private static SimulatorScenarioStep Begin(int tick) => new(tick, new BeginCleaning(AtTick(tick)));

    private static SimulatorScenarioStep Phase(int tick, JobPhase phase) => new(tick, new ExecutionPhaseVerified(AtTick(tick), phase));

    private static SimulatorScenarioStep Complete(int tick) => new(tick, new RequestNormalCompletion(AtTick(tick)));

    private static SimulatorScenarioStep Abort(int tick) => new(tick, new RequestAbort(AtTick(tick)));

    private static SimulatorScenarioStep Fail(int tick, string reasonCode) => new(tick, new ReportExecutionFailure(AtTick(tick), reasonCode));

    private static SimulatorScenarioStep Open(Plan plan, int tick) =>
        new(tick, new ValveLimitObserved(AtTick(tick), plan.ValveId, UpperLimit: true, LowerLimit: false));

    private static SimulatorScenarioStep Closed(Plan plan, int tick) =>
        new(tick, new ValveLimitObserved(AtTick(tick), plan.ValveId, UpperLimit: false, LowerLimit: true));

    private static SimulatorScenarioStep Standby(int tick) => new(tick, new AxisFeedbackObserved(AtTick(tick), AxisFeedbackState.AT_STANDBY));

    private static SimulatorScenarioStep AxisFault(int tick) => new(tick, new AxisFeedbackObserved(AtTick(tick), AxisFeedbackState.FAULT));

    private static SimulatorScenarioStep ValveTimeout(int tick) =>
        new(tick, new FeedbackTimeoutExpired(AtTick(tick), FeedbackTarget.VALVE_CLOSED));
}
