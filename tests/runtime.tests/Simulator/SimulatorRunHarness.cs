using System.Text.Json;
using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Wjss.Time;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Shared driver for the Stage 0.4A CP-3b SIMULATOR tests. NOT EXECUTED IN ARENA: Owner-local validation required.
///
/// <para>
/// Every Sensor comes from the canonical Runtime set (<see cref="SyntheticSensorMap.BuildInitialSensors"/> through
/// <see cref="RuntimeTestFixture"/>). Every instant is the scenario epoch plus a tick. No clock is read and no value
/// is random. A candidate Runtime revision is projected after each APPLIED transition only; REFUSED and NO_OP
/// transitions change no projected content.
/// </para>
/// </summary>
internal static class SimulatorRunHarness
{
    /// <summary>The canonical synthetic seed. It selects only the deterministic synthetic initial values.</summary>
    internal const ulong CanonicalSeed = 1;

    /// <summary>The canonical Sensor set in the order the SIMULATOR composes it.</summary>
    internal static IReadOnlyList<SensorPresentationState> CanonicalSensors() =>
        SyntheticSensorMap.BuildInitialSensors(
            RuntimeTestFixture.Config(),
            new SyntheticSeed(CanonicalSeed),
            RuntimeTestFixture.Instant);

    /// <summary>The scenario catalogue bound to the canonical Sensor set.</summary>
    internal static SimulatorScenarioSet ScenarioSet() => SimulatorScenarioCatalogue.Create(CanonicalSensors());

    /// <summary>The initial revision composed from the same canonical Sensor set.</summary>
    internal static RuntimeState InitialState() => RuntimeTestFixture.ComposeInitial(new SyntheticSeed(CanonicalSeed));

    /// <summary>The deterministic instant of a tick.</summary>
    internal static DateTimeOffset At(int tick) => SimulatorScenarioCatalogue.AtTick(tick);

    /// <summary>Returns a non-null value, or fails the test when the value is absent.</summary>
    internal static T Required<T>(T? value) where T : class =>
        value ?? throw new InvalidOperationException("A required value is absent.");

    /// <summary>One scheduled step at a tick.</summary>
    internal static SimulatorScenarioStep Step(int tick, SequencingEvent sequencingEvent) => new(tick, sequencingEvent);

    /// <summary>Admits the canonical Sensor at a ScanOrder position (0 is the primary target).</summary>
    internal static SimulatorScenarioStep AdmitSensor(SimulatorScenarioSet set, int tick, int canonicalIndex) =>
        Step(
            tick,
            new AdmitQueueEntry(
                At(tick),
                SequencingAdmissionSource.SCENARIO_PREPARED,
                set.Sensors[canonicalIndex].SensorId,
                SimulatorScenarioCatalogue.AdmissionReason,
                0,
                set.Topology));

    /// <summary>Dispatches the queue head with a ready pump.</summary>
    internal static SimulatorScenarioStep Dispatch(SimulatorScenarioSet set, int tick) =>
        Step(tick, new DispatchHead(At(tick), set.Topology, true));

    /// <summary>Observes the primary target's Isolation Valve CLOSED.</summary>
    internal static SimulatorScenarioStep ValveClosed(SimulatorScenarioSet set, int tick) =>
        Step(
            tick,
            new ValveLimitObserved(At(tick), set.Sensors[0].AssignedIsolationValveId, UpperLimit: false, LowerLimit: true));

    /// <summary>
    /// The standard lead-in used by the custom schedules: start, admit the primary and the second Sensor, dispatch the
    /// primary with a ready pump, observe its valve CLOSED, prepare it at tick 5, and begin cleaning at tick 6.
    /// </summary>
    internal static List<SimulatorScenarioStep> LeadIn(SimulatorScenarioSet set) =>
        new()
        {
            Step(0, new StartAutoSequence(At(0))),
            AdmitSensor(set, 1, 0),
            AdmitSensor(set, 2, 1),
            Dispatch(set, 3),
            ValveClosed(set, 4),
            Step(5, new AdvanceJobPreparation(At(5))),
            Step(6, new BeginCleaning(At(6))),
        };

    /// <summary>
    /// Drives the schedule through the kernel, retains evidence after every transition, and projects one candidate
    /// revision per APPLIED transition. Each candidate passes <see cref="RuntimeStateInvariants.RequireValid"/> inside
    /// <see cref="SequencingRuntimeProjection.ProjectRuntimeState"/>.
    /// </summary>
    internal static SimulatorRunResult Run(IReadOnlyList<SimulatorScenarioStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var initial = InitialState();
        var state = SequencingKernel.Initial();
        var retention = SequencingRetention.Empty;
        var previous = initial;
        var transitions = new List<SequencingTransition>();
        var retentions = new List<SequencingRetention>();
        var candidates = new List<RuntimeState>();
        var candidateTicks = new List<int>();

        foreach (var step in steps)
        {
            var before = state;
            var transition = SequencingKernel.Apply(state, step.Event);
            retention = SequencingRetention.Retain(retention, before, transition);
            state = transition.State;
            transitions.Add(transition);
            retentions.Add(retention);

            if (transition.Outcome != SequencingOutcome.APPLIED)
            {
                continue;
            }

            previous = SequencingRuntimeProjection.ProjectRuntimeState(previous, state, retention, At(step.Tick));
            candidates.Add(previous);
            candidateTicks.Add(step.Tick);
        }

        return new SimulatorRunResult(
            initial,
            steps,
            transitions,
            retentions,
            candidates,
            candidateTicks,
            state,
            retention);
    }

    /// <summary>Drives one catalogue scenario.</summary>
    internal static SimulatorRunResult RunScenario(SimulatorScenarioId id) => Run(ScenarioSet().Get(id).Steps);

    /// <summary>The canonical JSON form of any contract or retention value, under the accepted contract options.</summary>
    internal static string Json(object? value) => JsonSerializer.Serialize(value, ContractJson.Options);

    /// <summary>
    /// The sections a Delta can carry, plus the revision identity. Two states with equal sections agree for every
    /// Delta purpose. The Runtime health block is outside the deterministic chain and is not compared here.
    /// </summary>
    internal static string Sections(RuntimeState state) => Json(new
    {
        state.Revision,
        state.GeneratedAtUtc,
        state.Sensors,
        state.Walls,
        state.ActiveJob,
        state.Queue,
        state.Sequence,
        state.Pump,
        state.Alarms,
        state.Communication,
        state.Trend,
    });

    /// <summary>The outcome recorded by the Safe Return SR6 record of the release, or null when no Job released.</summary>
    internal static CleaningJobOutcome? ReleasedOutcome(IEnumerable<SequencingTransition> transitions) =>
        transitions
            .SelectMany(transition => transition.Records)
            .LastOrDefault(record => record.Step == SafeReturnStep.SR6)?
            .JobOutcome;

    /// <summary>The record of a transition with the given code. Fails the test when the code is absent.</summary>
    internal static SequencingEvidence RecordWithCode(SequencingTransition transition, string code)
    {
        foreach (var record in transition.Records)
        {
            if (string.Equals(record.Code, code, StringComparison.Ordinal))
            {
                return record;
            }
        }

        throw new InvalidOperationException($"The transition has no record with code {code}.");
    }
}

/// <summary>
/// The result of one driven schedule. <see cref="Steps"/>, <see cref="Transitions"/> and <see cref="Retentions"/> are
/// index-aligned: element i belongs to step i. <see cref="Candidates"/> and <see cref="CandidateTicks"/> are
/// index-aligned: element i is the revision projected at tick i, for APPLIED steps only.
/// </summary>
internal sealed record SimulatorRunResult(
    RuntimeState Initial,
    IReadOnlyList<SimulatorScenarioStep> Steps,
    IReadOnlyList<SequencingTransition> Transitions,
    IReadOnlyList<SequencingRetention> Retentions,
    IReadOnlyList<RuntimeState> Candidates,
    IReadOnlyList<int> CandidateTicks,
    SequencingState FinalState,
    SequencingRetention FinalRetention)
{
    /// <summary>The index of the step scheduled at a tick. Fails the test when no step is scheduled there.</summary>
    internal int IndexOfStep(int tick)
    {
        for (var index = 0; index < Steps.Count; index++)
        {
            if (Steps[index].Tick == tick)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"No step is scheduled at tick {tick}.");
    }

    /// <summary>The transition of the step scheduled at a tick.</summary>
    internal SequencingTransition TransitionAt(int tick) => Transitions[IndexOfStep(tick)];

    /// <summary>The kernel state after the step scheduled at a tick.</summary>
    internal SequencingState StateAt(int tick) => Transitions[IndexOfStep(tick)].State;

    /// <summary>The retention after the step scheduled at a tick.</summary>
    internal SequencingRetention RetentionAt(int tick) => Retentions[IndexOfStep(tick)];

    /// <summary>The candidate revision projected at a tick. Fails the test when no revision was projected there.</summary>
    internal RuntimeState CandidateAt(int tick)
    {
        for (var index = 0; index < CandidateTicks.Count; index++)
        {
            if (CandidateTicks[index] == tick)
            {
                return Candidates[index];
            }
        }

        throw new InvalidOperationException($"No candidate revision was projected at tick {tick}.");
    }

    /// <summary>The revision in force immediately before a tick's candidate: the previous candidate, or the initial revision.</summary>
    internal RuntimeState PreviousTo(int tick)
    {
        var previous = Initial;
        for (var index = 0; index < CandidateTicks.Count; index++)
        {
            if (CandidateTicks[index] == tick)
            {
                return previous;
            }

            previous = Candidates[index];
        }

        throw new InvalidOperationException($"No candidate revision was projected at tick {tick}.");
    }
}
