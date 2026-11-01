using System.Reflection;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime;
using Wjss.Runtime.Core;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// The H3-A TimeDelay surface as the Runtime host composes it: the optional SIMULATION configuration,
/// the frozen next-dispatch countdown that gates the automatic dispatch, and the read-only Cleaning
/// Job Workspace document. No listener, no socket and no device is used; the tests exercise the same
/// non-overlapping writer path the evolution loop owns.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local Release build and the Full .NET
/// matrix are the validation of record).
/// </summary>
public sealed class TimeDelayWorkspaceTests
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);

    private static SimulationTimeDelaySettings Manual(int seconds) => new()
    {
        Configuration = TimeDelayConfiguration.LegacySeededSimulationDefaults(TimeDelayMode.MANUAL_FIXED, seconds),
        SourceLabel = SimulationTimeDelaySettings.TestSourceLabel,
    };

    private static SimulationTimeDelaySettings Auto(
        BoilerCondition condition = BoilerCondition.VERY_CLEAN,
        bool ramp = false,
        RhTimeDelayInput? fixedRhInput = null) => new()
    {
        Configuration = TimeDelayConfiguration.LegacySeededSimulationDefaults(
            TimeDelayMode.AUTO_RH_LINEAR_FROZEN, activeBoilerCondition: condition),
        SourceLabel = fixedRhInput is null
            ? SimulationTimeDelaySettings.TestSourceLabel
            : SimulationTimeDelaySettings.DemoSourceLabel,
        RampRhTemperature = ramp,
        FixedRhInput = fixedRhInput,
    };

    private static RuntimeHostOptions Options(
        SimulatorScenarioId scenario,
        SimulationTimeDelaySettings? timeDelay = null) => new()
    {
        Profile = DeviceProfile.SIMULATOR,
        Port = 5181,
        Scenario = scenario,
        SyntheticSeed = 20261009,
        TickIntervalMilliseconds = 1000,
        StateHistoryCapacity = 32,
        DeltaHistoryCapacity = 32,
        TimeDelay = timeDelay,
    };

    private static SimulatorRuntime Create(
        SimulatorScenarioId scenario,
        SimulationTimeDelaySettings? timeDelay = null) =>
        SimulatorRuntime.Create(Options(scenario, timeDelay), new FixedClock(Origin));

    // The private non-overlapping host tick path, without starting the timer.
    private static void Tick(SimulatorRuntime runtime) =>
        typeof(SimulatorRuntime).GetMethod("ExecuteTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(runtime, null);

    /// <summary>Ticks until the first Job is released, returning the ordinal of the release tick.</summary>
    private static int TickUntilFirstRelease(SimulatorRuntime runtime)
    {
        var hadJob = false;
        for (var tick = 0; tick < 400; tick++)
        {
            Tick(runtime);
            if (runtime.State.ActiveJob is not null)
            {
                hadJob = true;
                continue;
            }

            if (hadJob) return tick;
        }

        throw new InvalidOperationException("The scenario never released its first Job.");
    }

    // ---------------------------------------------------------------- the inert default

    [Fact]
    public async Task Without_a_configuration_no_countdown_exists_and_the_pre_existing_dispatch_is_unchanged()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION);
        try
        {
            TickUntilFirstRelease(runtime);
            Assert.Equal(CountdownLifecycleState.NONE, runtime.Countdown.Lifecycle);
            Assert.Null(runtime.Countdown.Decision);

            // The very next tick dispatches, exactly as before H3-A.
            Tick(runtime);
            Assert.NotNull(runtime.State.ActiveJob);
            Assert.Equal(CountdownLifecycleState.NONE, runtime.Countdown.Lifecycle);

            var workspace = runtime.ProjectWorkspace();
            Assert.Null(workspace.Configuration);
            Assert.False(workspace.Preview.Available);
            Assert.False(workspace.Preview.AffectsExecution);
            Assert.Equal(CountdownRefusalCode.CONFIGURATION_MISSING, workspace.Preview.RefusalCode);
            Assert.Null(workspace.Preview.AppliedDelaySeconds);
        }
        finally { await runtime.DisposeAsync(); }
    }

    // ---------------------------------------------------------------- MANUAL_FIXED

    [Fact]
    public async Task A_manual_fixed_delay_freezes_the_countdown_and_holds_the_dispatch_until_it_expires()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Manual(5));
        try
        {
            TickUntilFirstRelease(runtime);

            // ONE detached reader generation for every projection of this revision. Each Publication
            // acquisition freezes its own RuntimeState copy (Queue = state.Queue with { ... }), so two
            // acquisitions can never share instances even when their values are identical: the
            // reference identity proved below is only meaningful within one publication.
            var publication = runtime.Publication;
            var workspace = runtime.ProjectWorkspace(publication);
            Assert.Equal(SchemaIds.DispatchWorkspace, workspace.Schema);
            Assert.Equal(SchemaIds.DispatchWorkspaceKind, workspace.Kind);
            Assert.Equal(DispatchWorkspace.SimulationNote, workspace.Simulation);
            Assert.Equal(publication.Current.Revision, workspace.Revision);

            var countdown = workspace.Countdown;
            Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, countdown.Lifecycle);
            Assert.Equal(1, countdown.Generation);
            Assert.True(countdown.Frozen);
            Assert.False(countdown.DispatchAdmissible);
            Assert.Equal(5, countdown.RemainingSeconds);

            var decision = Assert.IsType<NextDispatchDelayDecision>(countdown.Decision);
            Assert.Equal("SYN-TD-1", decision.DecisionId);
            Assert.Equal(TimeDelayMode.MANUAL_FIXED, decision.Mode);
            Assert.Null(decision.RhInput);
            Assert.Equal(1.00, decision.ZoneFactor);
            Assert.Equal(5, decision.BaseDelaySeconds);
            Assert.Equal(5, decision.AppliedDelaySeconds);
            Assert.Equal(workspace.Queue.Revision, decision.QueueGeneration);
            Assert.Equal(workspace.Queue.Entries[0].EntryId, decision.QueueEntryId);
            Assert.Equal(workspace.Queue.Entries[0].SensorId, decision.NextSensorId);
            Assert.Equal(TimeDelayConfiguration.LegacySeededSimulationVersion, decision.ConfigurationVersion);

            // The workspace reuses the Snapshot's own Queue and Active Job projections, so the two
            // documents of one revision cannot disagree: they carry the SAME instances, not merely
            // equal values. Both are projected from the publication captured above.
            var snapshot = runtime.ProjectSnapshot(publication);
            Assert.Same(snapshot.Queue, workspace.Queue);
            Assert.Same(snapshot.ActiveJob, workspace.ActiveJob);
            Assert.Same(publication.Current.Queue, workspace.Queue);
            Assert.Same(publication.Current.ActiveJob, workspace.ActiveJob);
            Assert.Equal(DispatchTimelineStage.NEXT_DISPATCH_COUNTDOWN, workspace.Timeline.Stage);
            Assert.Equal(13, workspace.Timeline.Steps.Count);
            Assert.Equal(
                DispatchWorkspaceProjectorSteps(),
                workspace.Timeline.Steps.Select(step => step.Stage));
            Assert.Single(workspace.Timeline.Steps, step => step.State == DispatchTimelineStepState.CURRENT);

            // The frozen delay holds the dispatch for every tick before zero.
            for (var held = 1; held <= 4; held++)
            {
                Tick(runtime);
                Assert.Null(runtime.State.ActiveJob);
                Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, runtime.Countdown.Lifecycle);
                Assert.Equal(5 - held, runtime.Countdown.RemainingSeconds);
                Assert.Equal(decision, runtime.Countdown.Decision);
            }

            // Exactly at zero the countdown completes and the single dispatch follows.
            Tick(runtime);
            Assert.Equal(CountdownLifecycleState.DISPATCHED, runtime.Countdown.Lifecycle);
            var job = Assert.IsType<ActiveCleaningJobState>(runtime.State.ActiveJob);
            Assert.Equal(decision.NextSensorId, job.TargetSensorId);
            Assert.Equal(decision.QueueEntryId, runtime.State.Queue.LastDispatch?.QueueEntryId);
            Assert.Equal(job.JobId, runtime.State.Queue.LastDispatch?.JobId);

            // A dispatched countdown stays dispatched: the Job now blocks any second dispatch.
            var dispatchId = job.Dispatch.DispatchId;
            Tick(runtime);
            Tick(runtime);
            Assert.Equal(CountdownLifecycleState.DISPATCHED, runtime.Countdown.Lifecycle);
            Assert.Equal(dispatchId, Assert.IsType<ActiveCleaningJobState>(runtime.State.ActiveJob).Dispatch.DispatchId);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_manual_delay_of_zero_still_creates_and_dispatch_through_the_frozen_decision()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Manual(0));
        try
        {
            // A configured zero delay is an explicit decision that completes immediately, so the
            // release tick itself dispatches through a real frozen decision of generation one.
            for (var tick = 0; tick < 400 && runtime.Countdown.Lifecycle != CountdownLifecycleState.DISPATCHED; tick++)
            {
                Tick(runtime);
            }

            Assert.Equal(CountdownLifecycleState.DISPATCHED, runtime.Countdown.Lifecycle);
            Assert.Equal(0, runtime.Countdown.RemainingSeconds);
            Assert.Equal(1, runtime.Countdown.Generation);
            var decision = Assert.IsType<NextDispatchDelayDecision>(runtime.Countdown.Decision);
            Assert.Equal(0, decision.BaseDelaySeconds);
            Assert.Equal(0, decision.AppliedDelaySeconds);
            Assert.Equal(TimeDelayMode.MANUAL_FIXED, decision.Mode);
            Assert.Null(decision.RhInput);

            var job = Assert.IsType<ActiveCleaningJobState>(runtime.State.ActiveJob);
            Assert.Equal(decision.NextSensorId, job.TargetSensorId);
        }
        finally { await runtime.DisposeAsync(); }
    }

    // ---------------------------------------------------------------- AUTO_RH_LINEAR_FROZEN

    [Fact]
    public async Task An_auto_decision_freezes_the_RH_reading_and_the_preview_moves_without_touching_it()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Auto(ramp: true));
        try
        {
            TickUntilFirstRelease(runtime);

            var frozen = Assert.IsType<NextDispatchDelayDecision>(runtime.Countdown.Decision);
            var frozenRh = Assert.IsType<RhTimeDelayInput>(frozen.RhInput);
            Assert.Equal(TimeDelayMode.AUTO_RH_LINEAR_FROZEN, frozen.Mode);
            Assert.Equal(BoilerCondition.VERY_CLEAN, frozen.BoilerCondition);
            Assert.Equal(Quality.GOOD, frozenRh.Quality);
            Assert.True(frozenRh.Generation > 0);
            Assert.InRange(frozenRh.RhTemperature, frozen.RhTempMin, frozen.RhTempMax);
            Assert.Equal(
                TimeDelayPlanner.ApplyZoneFactor(frozen.BaseDelaySeconds, frozen.ZoneFactor),
                frozen.AppliedDelaySeconds);
            Assert.Equal(
                frozen.Zone == DelayZone.UPPER ? 0.75 : 1.00,
                frozen.ZoneFactor);

            var before = runtime.ProjectWorkspace();
            Assert.True(before.Preview.Available);
            Assert.False(before.Preview.AffectsExecution);
            // The preview is self-consistent: its base delay is the truncation of its own mapping.
            Assert.Equal(
                before.Preview.BaseDelaySeconds,
                TimeDelayPlanner.TruncateToWholeSeconds(Assert.IsType<double>(before.Preview.MappedDelaySeconds)));

            // The live preview follows the ramp; the frozen decision does not move.
            Tick(runtime);
            var after = runtime.ProjectWorkspace();
            Assert.NotEqual(before.Preview.RhTemperature, after.Preview.RhTemperature);
            Assert.Equal(frozen, after.Countdown.Decision);
            Assert.False(after.Preview.AffectsExecution);
            Assert.Equal(frozen.AppliedDelaySeconds, after.Countdown.Decision!.AppliedDelaySeconds);
            Assert.Equal(frozenRh.RhTemperature, after.Countdown.Decision.RhInput!.RhTemperature);
        }
        finally { await runtime.DisposeAsync(); }
    }

    // ---------------------------------------------------------------- fail-closed refusals

    [Fact]
    public async Task A_bad_quality_RH_reading_faults_the_countdown_and_no_dispatch_ever_follows()
    {
        var reading = new RhTimeDelayInput
        {
            RhTemperature = 520,
            Quality = Quality.BAD,
            ObservedAt = UtcTimestamps.Format(Origin),
            Generation = 1,
        };

        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Auto(fixedRhInput: reading));
        try
        {
            TickUntilFirstRelease(runtime);

            Assert.Equal(CountdownLifecycleState.FAULTED, runtime.Countdown.Lifecycle);
            Assert.Equal(CountdownRefusalCode.RH_BAD_QUALITY, runtime.Countdown.RefusalCode);
            Assert.Null(runtime.Countdown.Decision);
            Assert.False(runtime.Countdown.Frozen);

            var head = runtime.State.Queue.Entries[0].EntryId;
            for (var tick = 0; tick < 6; tick++) Tick(runtime);
            Assert.Null(runtime.State.ActiveJob);
            Assert.Equal(CountdownLifecycleState.FAULTED, runtime.Countdown.Lifecycle);
            Assert.Equal(head, runtime.State.Queue.Entries[0].EntryId);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_stale_RH_reading_faults_the_countdown_without_a_zero_delay()
    {
        var reading = new RhTimeDelayInput
        {
            RhTemperature = 525,
            Quality = Quality.GOOD,
            ObservedAt = UtcTimestamps.Format(Origin.AddHours(-2)),
            Generation = 3,
        };

        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Auto(fixedRhInput: reading));
        try
        {
            TickUntilFirstRelease(runtime);

            Assert.Equal(CountdownLifecycleState.FAULTED, runtime.Countdown.Lifecycle);
            Assert.Equal(CountdownRefusalCode.RH_STALE, runtime.Countdown.RefusalCode);
            Assert.Null(runtime.Countdown.Decision);

            var workspace = runtime.ProjectWorkspace();
            Assert.False(workspace.Preview.Available);
            Assert.Equal(CountdownRefusalCode.RH_STALE, workspace.Preview.RefusalCode);
            Assert.Null(workspace.Preview.AppliedDelaySeconds);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_paused_AutoSequence_creates_no_countdown_and_dispatches_nothing()
    {
        var runtime = Create(SimulatorScenarioId.PAUSE_AFTER_CURRENT_JOB, Manual(0));
        try
        {
            TickUntilFirstRelease(runtime);

            // Pause was requested while the Job was active: the release lands in PAUSED, so no
            // decision is created and the configured countdown can never authorise a dispatch.
            Assert.Equal(AutoSequenceMode.PAUSED, runtime.State.Sequence.Mode);
            Assert.Equal(CountdownLifecycleState.NONE, runtime.Countdown.Lifecycle);
            Assert.Null(runtime.Countdown.Decision);
            var head = runtime.State.Queue.Entries[0].EntryId;
            for (var tick = 0; tick < 5; tick++) Tick(runtime);
            Assert.Null(runtime.State.ActiveJob);
            Assert.Equal(CountdownLifecycleState.NONE, runtime.Countdown.Lifecycle);
            Assert.Equal(head, runtime.State.Queue.Entries[0].EntryId);
            // The first (scripted) dispatch still happened; the paused sequence added no second one.
            Assert.Equal("SYN-JOB-1", runtime.State.Queue.LastDispatch?.JobId);
        }
        finally { await runtime.DisposeAsync(); }
    }

    // ---------------------------------------------------------------- refusal propagation

    // The deterministic catalogue dispatches its first Job as a SCRIPTED event, so a test that needs
    // "a countdown whose own dispatch already happened, followed by a further release" cannot reach
    // that boundary by ticking alone. These tests therefore seed a GENUINE coordinator-produced
    // countdown AFTER that scripted dispatch and then let the real tick path do everything else: the
    // seed decides only WHERE the state machine stands, never what the Runtime does with it.

    /// <summary>Places a coordinator-produced countdown in the Runtime's single countdown field.</summary>
    private static void SeedCountdown(SimulatorRuntime runtime, NextDispatchCountdown countdown) =>
        typeof(SimulatorRuntime)
            .GetField("_countdown", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(runtime, countdown);

    private static RhTimeDelayInput Reading(
        double temperature,
        DateTimeOffset observedAt,
        Quality quality = Quality.GOOD,
        long generation = 1) => new()
        {
            RhTemperature = temperature,
            Quality = quality,
            ObservedAt = UtcTimestamps.Format(observedAt),
            Generation = generation,
        };

    /// <summary>
    /// An AUTO configuration with an explicit delay range and RH window, so a reading can be made
    /// unusable for the NEXT generation while a previous generation stays valid.
    /// </summary>
    private static SimulationTimeDelaySettings AutoRange(
        int minimumSeconds,
        int maximumSeconds,
        int windowSeconds,
        RhTimeDelayInput? fixedRhInput = null) => new()
        {
            Configuration = TimeDelayConfiguration.LegacySeededSimulationDefaults(
                TimeDelayMode.AUTO_RH_LINEAR_FROZEN,
                activeBoilerCondition: BoilerCondition.VERY_CLEAN) with
            {
                RhInputWindowSeconds = windowSeconds,
                BoilerRanges =
                [
                    new BoilerConditionDelayRange
                    {
                        Condition = BoilerCondition.VERY_CLEAN,
                        AutoDelayMinSeconds = minimumSeconds,
                        AutoDelayMaxSeconds = maximumSeconds,
                    },
                    new BoilerConditionDelayRange
                    {
                        Condition = BoilerCondition.NORMAL_CLEAN,
                        AutoDelayMinSeconds = 500,
                        AutoDelayMaxSeconds = 550,
                    },
                    new BoilerConditionDelayRange
                    {
                        Condition = BoilerCondition.MEDIUM_DIRTY,
                        AutoDelayMinSeconds = 340,
                        AutoDelayMaxSeconds = 380,
                    },
                    new BoilerConditionDelayRange
                    {
                        Condition = BoilerCondition.VERY_DIRTY,
                        AutoDelayMinSeconds = 240,
                        AutoDelayMaxSeconds = 280,
                    },
                ],
            },
            SourceLabel = fixedRhInput is null
                ? SimulationTimeDelaySettings.TestSourceLabel
                : SimulationTimeDelaySettings.DemoSourceLabel,
            FixedRhInput = fixedRhInput,
        };

    /// <summary>One decision planned exactly as the Runtime plans it, from the same published head.</summary>
    private static NextDispatchDelayDecision PlannedDecision(
        SimulatorRuntime runtime,
        TimeDelayConfiguration configuration,
        QueueEntry head,
        RhTimeDelayInput reading,
        DateTimeOffset atUtc,
        int generation = 1)
    {
        var sensor = runtime.State.Sensors.First(sensor => sensor.SensorId == head.SensorId);
        Assert.True(TimeDelayPlanner.TryResolveSensorIdentity(sensor, out var identity, out var identityRefusal));
        Assert.Equal(CountdownRefusalCode.NONE, identityRefusal);

        var plan = TimeDelayPlanner.Plan(
            configuration, identity, reading, atUtc, $"SYN-TD-{generation}",
            generation, runtime.State.Queue.Revision, head.EntryId);
        Assert.True(plan.Accepted, $"The seeded decision must be a real accepted decision [{plan.RefusalCode}].");
        return Assert.IsType<NextDispatchDelayDecision>(plan.Decision);
    }

    /// <summary>A generation-one decision for the current head, frozen at the given instant.</summary>
    private static NextDispatchCountdown FrozenCountdown(
        SimulatorRuntime runtime,
        TimeDelayConfiguration configuration,
        QueueEntry head,
        DateTimeOffset createdAtUtc)
    {
        var decision = PlannedDecision(runtime, configuration, head, Reading(520, Origin), createdAtUtc) with
        {
            CreatedAt = UtcTimestamps.Format(createdAtUtc),
        };
        var frozen = NextDispatchCountdownCoordinator.Begin(NextDispatchCountdown.None, decision, out var refusal);
        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.Equal(decision.AppliedDelaySeconds, frozen.RemainingSeconds);
        return frozen;
    }

    /// <summary>A generation-one countdown whose single dispatch already happened.</summary>
    private static NextDispatchCountdown DispatchedCountdown(
        SimulatorRuntime runtime,
        SimulationTimeDelaySettings settings)
    {
        var decision = PlannedDecision(
            runtime, settings.Configuration, runtime.State.Queue.Entries[0], Reading(520, Origin), Origin);
        var begun = NextDispatchCountdownCoordinator.Begin(NextDispatchCountdown.None, decision, out var beginRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, beginRefusal);
        var completed = NextDispatchCountdownCoordinator.Advance(
            begun, Origin.AddSeconds(decision.AppliedDelaySeconds), out var advanceRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, advanceRefusal);
        var dispatched = NextDispatchCountdownCoordinator.MarkDispatched(
            completed, Origin.AddSeconds(decision.AppliedDelaySeconds), out var dispatchRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, dispatchRefusal);
        Assert.Equal(CountdownLifecycleState.DISPATCHED, dispatched.Lifecycle);
        return dispatched;
    }

    /// <summary>Ticks until the scripted first Job is active, and returns the head of that moment.</summary>
    private static QueueEntry TickUntilActiveJob(SimulatorRuntime runtime)
    {
        for (var tick = 0; tick < 60; tick++)
        {
            Tick(runtime);
            if (runtime.State.ActiveJob is not null) return runtime.State.Queue.Entries[0];
        }

        throw new InvalidOperationException("The scenario never activated its first Job.");
    }

    /// <summary>The published evidence of a failed next generation: the same values in Runtime and workspace.</summary>
    private static void AssertFailedNextGeneration(
        SimulatorRuntime runtime,
        int attemptedGeneration,
        CountdownRefusalCode refusal)
    {
        var countdown = runtime.Countdown;
        Assert.Equal(CountdownLifecycleState.FAULTED, countdown.Lifecycle);
        Assert.Equal(attemptedGeneration, countdown.Generation);
        Assert.Equal(refusal, countdown.RefusalCode);
        Assert.Null(countdown.Decision);
        Assert.Equal(0, countdown.RemainingSeconds);
        Assert.Null(countdown.CreatedAtUtc);
        Assert.Null(countdown.LastObservedAtUtc);
        Assert.Null(countdown.CompletedAtUtc);
        Assert.Null(countdown.DispatchedAtUtc);
        Assert.False(countdown.Frozen);
        Assert.False(countdown.DispatchAdmissible);

        // The same failure through the read-only workspace projection: the exact refusal is visible and
        // no fabricated remaining delay, frozen decision or admissibility is presented.
        var workspace = runtime.ProjectWorkspace();
        Assert.Equal(CountdownLifecycleState.FAULTED, workspace.Countdown.Lifecycle);
        Assert.Equal(attemptedGeneration, workspace.Countdown.Generation);
        Assert.Equal(refusal, workspace.Countdown.RefusalCode);
        Assert.Null(workspace.Countdown.Decision);
        Assert.Null(workspace.Countdown.CreatedAt);
        Assert.Null(workspace.Countdown.CompletedAt);
        Assert.Null(workspace.Countdown.DispatchedAt);
        Assert.Equal(0, workspace.Countdown.RemainingSeconds);
        Assert.False(workspace.Countdown.Frozen);
        Assert.False(workspace.Countdown.DispatchAdmissible);
        // SIMULATION identity is explicit and no command surface is added to a faulted document.
        Assert.Equal(DispatchWorkspace.SimulationNote, workspace.Simulation);
        // The next head stays visible and ready: waiting and fault state never enter a QueueEntry.
        Assert.NotEmpty(workspace.Queue.Entries);
    }

    /// <summary>
    /// No later tick ever retries, re-plans, re-samples or clears a fault: the very same record is
    /// still in place, still FAULTED and still admitting no dispatch. The window also contains a Job
    /// RELEASE whenever one is still pending, because a release is the one ordinary lifecycle event
    /// that starts a new generation from a non-faulted countdown.
    /// </summary>
    private static void AssertFaultIsStable(SimulatorRuntime runtime, CountdownRefusalCode refusal)
    {
        var faulted = runtime.Countdown;
        for (var tick = 0; tick < 20; tick++) Tick(runtime);
        Assert.Same(faulted, runtime.Countdown);
        Assert.Equal(CountdownLifecycleState.FAULTED, runtime.Countdown.Lifecycle);
        Assert.Equal(refusal, runtime.Countdown.RefusalCode);
        Assert.False(runtime.Countdown.Frozen);
        Assert.False(runtime.Countdown.DispatchAdmissible);
        Assert.Null(runtime.Countdown.DispatchedAtUtc);
    }

    [Fact]
    public async Task A_refused_advance_faults_the_runtime_countdown_and_is_never_retried()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Manual(30));
        try
        {
            var head = TickUntilActiveJob(runtime);

            // A frozen generation whose own accepted observation lies in the FUTURE of the tick clock.
            // Every coordinator invariant holds; the tick instant simply runs behind it, which is what a
            // clock that steps backwards looks like.
            var frozen = FrozenCountdown(runtime, Manual(30).Configuration, head, Origin.AddHours(1));
            Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, frozen.Lifecycle);
            Assert.Equal(30, frozen.RemainingSeconds);
            Assert.Null(frozen.RefusalCode);
            SeedCountdown(runtime, frozen);

            Tick(runtime);

            // The refused Advance faults the countdown with its exact code instead of being discarded.
            Assert.Equal(CountdownLifecycleState.FAULTED, runtime.Countdown.Lifecycle);
            Assert.Equal(CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION, runtime.Countdown.RefusalCode);
            // A refused transition preserves the frozen evidence it could not accept.
            Assert.Equal(30, runtime.Countdown.RemainingSeconds);
            Assert.Equal(Origin.AddHours(1), runtime.Countdown.LastObservedAtUtc);
            Assert.Equal(frozen.Decision, runtime.Countdown.Decision);
            Assert.False(runtime.Countdown.Frozen);
            Assert.False(runtime.Countdown.DispatchAdmissible);

            // The workspace carries the same refusal and never claims admissibility or a live delay.
            var workspace = runtime.ProjectWorkspace();
            Assert.Equal(CountdownLifecycleState.FAULTED, workspace.Countdown.Lifecycle);
            Assert.Equal(CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION, workspace.Countdown.RefusalCode);
            Assert.False(workspace.Countdown.DispatchAdmissible);
            Assert.False(workspace.Countdown.Frozen);

            // A refused Advance cannot dispatch, and the fault is not retried by any later tick. The
            // stability ticks cover the Job's own release, so the release-triggered next generation is
            // covered too; a dispatch after the fault would replace the published LastDispatch.
            AssertFaultIsStable(runtime, CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION);
            Assert.Equal("SYN-JOB-1", runtime.State.Queue.LastDispatch?.JobId);
            Assert.Null(runtime.State.ActiveJob);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_refused_advance_with_missing_observation_evidence_faults_the_countdown()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Manual(30));
        try
        {
            var head = TickUntilActiveJob(runtime);

            // A deliberately malformed frozen record: the observation baseline a frozen countdown must
            // always carry is absent. The Runtime must fail closed, never reinterpret it as "no
            // regression" and never repair it.
            var malformed = FrozenCountdown(runtime, Manual(30).Configuration, head, Origin) with
            {
                LastObservedAtUtc = null,
            };
            SeedCountdown(runtime, malformed);

            Tick(runtime);

            Assert.Equal(CountdownLifecycleState.FAULTED, runtime.Countdown.Lifecycle);
            Assert.Equal(CountdownRefusalCode.DECISION_TIMESTAMP_INVALID, runtime.Countdown.RefusalCode);
            Assert.Equal(30, runtime.Countdown.RemainingSeconds);
            Assert.Null(runtime.Countdown.LastObservedAtUtc);
            Assert.Equal(malformed.Decision, runtime.Countdown.Decision);
            AssertFaultIsStable(runtime, CountdownRefusalCode.DECISION_TIMESTAMP_INVALID);
            Assert.Equal("SYN-JOB-1", runtime.State.Queue.LastDispatch?.JobId);
            Assert.Null(runtime.State.ActiveJob);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_next_generation_after_a_dispatch_records_a_stale_reading_as_a_fault()
    {
        // The reading is usable for nothing at all: it is already older than the configured RH window,
        // so the plan the Runtime attempts on the release tick can only be refused with RH_STALE. The
        // countdown is DISPATCHED at that moment, which is the closed generation that swallowed it.
        var stale = Reading(520, Origin.AddMinutes(-1));
        var runtime = Create(
            SimulatorScenarioId.NORMAL_COMPLETION, AutoRange(600, 660, windowSeconds: 25, fixedRhInput: stale));
        try
        {
            TickUntilActiveJob(runtime);
            SeedCountdown(runtime, DispatchedCountdown(runtime, AutoRange(600, 660, 25)));

            for (var tick = 0; tick < 60 && runtime.Countdown.Lifecycle == CountdownLifecycleState.DISPATCHED; tick++)
            {
                Tick(runtime);
            }

            // The failure is recorded with the exact refusal, after a CLOSED dispatch generation.
            AssertFailedNextGeneration(runtime, 2, CountdownRefusalCode.RH_STALE);

            // The previous dispatch is neither repeated nor reinterpreted, and the head is still ready.
            Assert.Null(runtime.State.ActiveJob);
            Assert.Equal("SYN-JOB-1", runtime.State.Queue.LastDispatch?.JobId);
            Assert.NotEmpty(runtime.State.Queue.Entries);
            // The failed generation is not retried, not re-sampled and not cleared by later ticks.
            AssertFaultIsStable(runtime, CountdownRefusalCode.RH_STALE);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_next_generation_after_a_dispatch_records_an_unusable_reading_as_a_fault()
    {
        // The reading EXISTS but is not usable: BAD quality is never converted into a delay, and the
        // exact planner refusal is what the Runtime records.
        var unusable = Reading(520, Origin, Quality.BAD);
        var runtime = Create(
            SimulatorScenarioId.NORMAL_COMPLETION, AutoRange(600, 660, windowSeconds: 300, fixedRhInput: unusable));
        try
        {
            TickUntilActiveJob(runtime);
            SeedCountdown(runtime, DispatchedCountdown(runtime, AutoRange(600, 660, 300)));

            for (var tick = 0; tick < 60 && runtime.Countdown.Lifecycle == CountdownLifecycleState.DISPATCHED; tick++)
            {
                Tick(runtime);
            }

            AssertFailedNextGeneration(runtime, 2, CountdownRefusalCode.RH_BAD_QUALITY);
            Assert.Null(runtime.State.ActiveJob);
            Assert.Equal("SYN-JOB-1", runtime.State.Queue.LastDispatch?.JobId);
            AssertFaultIsStable(runtime, CountdownRefusalCode.RH_BAD_QUALITY);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_next_generation_after_a_dispatch_records_generation_exhaustion_without_planning()
    {
        // The reading here is BAD on purpose: if the Runtime sampled and planned before deciding the
        // generation, the recorded refusal would be RH_BAD_QUALITY. It is EXHAUSTED instead, which is
        // the observable proof that the generation authority decided first.
        var unusable = Reading(520, Origin, Quality.BAD);
        var runtime = Create(
            SimulatorScenarioId.NORMAL_COMPLETION, AutoRange(600, 660, windowSeconds: 300, fixedRhInput: unusable));
        try
        {
            TickUntilActiveJob(runtime);
            // The last representable generation: its successor does not exist. A session cannot reach
            // this boundary in a finite test, so the closed generation is placed at it.
            SeedCountdown(runtime, DispatchedCountdown(runtime, AutoRange(600, 660, 300)) with
            {
                Generation = int.MaxValue,
            });

            for (var tick = 0; tick < 60 && runtime.Countdown.Lifecycle == CountdownLifecycleState.DISPATCHED; tick++)
            {
                Tick(runtime);
            }

            Assert.Equal(CountdownLifecycleState.FAULTED, runtime.Countdown.Lifecycle);
            Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_EXHAUSTED, runtime.Countdown.RefusalCode);
            // No successor was fabricated, so the last accepted identity survives unchanged.
            Assert.Equal(int.MaxValue, runtime.Countdown.Generation);
            Assert.Null(runtime.Countdown.Decision);
            Assert.Equal(0, runtime.Countdown.RemainingSeconds);
            Assert.Null(runtime.Countdown.CreatedAtUtc);
            Assert.Null(runtime.Countdown.DispatchedAtUtc);
            Assert.False(runtime.Countdown.DispatchAdmissible);

            // Nothing was dispatched and the failure is not retried.
            Assert.Null(runtime.State.ActiveJob);
            Assert.Equal("SYN-JOB-1", runtime.State.Queue.LastDispatch?.JobId);
            AssertFaultIsStable(runtime, CountdownRefusalCode.COUNTDOWN_GENERATION_EXHAUSTED);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_new_AutoSequence_session_clears_a_faulted_countdown_and_starts_from_current_state()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Manual(5));
        try
        {
            // A fault left over from a previous session: the state a restarted session must not carry.
            SeedCountdown(runtime, NextDispatchCountdownCoordinator.FaultNextGeneration(
                NextDispatchCountdown.None,
                7,
                CountdownRefusalCode.RH_STALE));

            // The session's own StartAutoSequence is the explicit lifecycle trigger that clears it.
            Tick(runtime);
            Assert.Equal(CountdownLifecycleState.NONE, runtime.Countdown.Lifecycle);
            Assert.Null(runtime.Countdown.RefusalCode);
            Assert.Null(runtime.Countdown.Decision);
            Assert.Equal(0, runtime.Countdown.RemainingSeconds);

            // The new session then starts from ITS current state: generation one, full applied delay.
            for (var tick = 0; tick < 60 && runtime.Countdown.Lifecycle != CountdownLifecycleState.ACTIVE_FROZEN; tick++)
            {
                Tick(runtime);
            }

            Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, runtime.Countdown.Lifecycle);
            Assert.Equal(1, runtime.Countdown.Generation);      // never the failed generation
            Assert.Equal(5, runtime.Countdown.RemainingSeconds);
            Assert.Null(runtime.Countdown.RefusalCode);
            Assert.NotNull(runtime.Countdown.Decision);
            Assert.True(runtime.Countdown.Frozen);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_superseded_countdown_restarts_with_the_full_applied_delay()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Manual(5));
        try
        {
            var head = TickUntilActiveJob(runtime);
            var superseded = NextDispatchCountdownCoordinator.Supersede(
                FrozenCountdown(runtime, Manual(5).Configuration, head, Origin),
                CountdownSupersessionReason.QUEUE_HEAD_IDENTITY_CHANGED,
                out var supersedeRefusal);
            Assert.Equal(CountdownRefusalCode.NONE, supersedeRefusal);
            Assert.Equal(CountdownLifecycleState.SUPERSEDED, superseded.Lifecycle);
            Assert.Equal(5, superseded.RemainingSeconds);
            SeedCountdown(runtime, superseded);

            for (var tick = 0; tick < 60 && runtime.Countdown.Lifecycle != CountdownLifecycleState.ACTIVE_FROZEN; tick++)
            {
                Tick(runtime);
            }

            // The authorized restart path is unchanged: the next generation, and the FULL delay again.
            Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, runtime.Countdown.Lifecycle);
            Assert.Equal(2, runtime.Countdown.Generation);
            Assert.Equal(5, runtime.Countdown.RemainingSeconds);
            Assert.Equal(
                CountdownSupersessionReason.QUEUE_HEAD_IDENTITY_CHANGED,
                runtime.Countdown.LastSupersededReason);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public void The_simulation_reading_generation_accepts_the_last_representable_sample()
    {
        var settings = AutoRange(600, 660, windowSeconds: 30);

        Assert.True(settings.TryRhSampleAt(
            SimulationTimeDelaySettings.MaximumSampleTick, Origin, out var input, out var refusal));
        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        var reading = Assert.IsType<RhTimeDelayInput>(input);
        Assert.Equal(long.MaxValue, reading.Generation);     // the last representable generation
        Assert.Equal(UtcTimestamps.Format(Origin), reading.ObservedAt);
        Assert.Equal(Quality.GOOD, reading.Quality);
        Assert.InRange(reading.RhTemperature, settings.Configuration.RhTempMin, settings.Configuration.RhTempMax);

        // The planner accepts it: a positive generation, GOOD quality and a current timestamp.
        Assert.True(TimeDelayPlanner.TryValidateRhInput(
            settings.Configuration, reading, Origin, out _, out var plannerRefusal));
        Assert.Equal(CountdownRefusalCode.NONE, plannerRefusal);

        // Deterministic and never saturated: the same tick always yields the same reading identity.
        Assert.True(settings.TryRhSampleAt(SimulationTimeDelaySettings.MaximumSampleTick, Origin, out var again, out _));
        Assert.Equal(reading, Assert.IsType<RhTimeDelayInput>(again));
    }

    [Fact]
    public void The_simulation_reading_generation_refuses_a_sample_it_cannot_represent()
    {
        var settings = AutoRange(600, 660, windowSeconds: 30);

        // No exception, no wrap and no negative generation: a typed refusal at both bounds.
        Assert.False(settings.TryRhSampleAt(
            SimulationTimeDelaySettings.MaximumSampleTick + 1, Origin, out var input, out var refusal));
        Assert.Null(input);
        Assert.Equal(CountdownRefusalCode.RH_GENERATION_INVALID, refusal);

        Assert.False(settings.TryRhSampleAt(-1L, Origin, out var negative, out var negativeRefusal));
        Assert.Null(negative);
        Assert.Equal(CountdownRefusalCode.RH_GENERATION_INVALID, negativeRefusal);

        // The old int boundary is no longer a narrowing conversion: the reading identity is a long.
        var reading = settings.RhSampleAt(int.MaxValue + 1L, Origin);
        Assert.Equal(int.MaxValue + 2L, reading.Generation);
        Assert.True(reading.Generation > 0);
    }

    // ---------------------------------------------------------------- terminal fence

    /// <summary>
    /// Ticks until the Active Job is released and returns the ordinal of that release tick. The
    /// release is the ONE ordinary lifecycle event that may legitimately start a new generation, so it
    /// is the exact condition a fault must survive.
    /// </summary>
    private static int TickUntilRelease(SimulatorRuntime runtime)
    {
        for (var tick = 0; tick < 120; tick++)
        {
            Tick(runtime);
            if (runtime.State.ActiveJob is null) return tick;
        }

        throw new InvalidOperationException("The scenario never released its Job.");
    }

    [Fact]
    public async Task A_faulted_countdown_is_not_restarted_by_the_release_that_starts_a_decision()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Manual(5));
        try
        {
            var head = TickUntilActiveJob(runtime);

            // A real refused Advance faults the countdown WITH its decision, which is the
            // decision-bearing fault the release must not be able to replace.
            var frozen = FrozenCountdown(runtime, Manual(5).Configuration, head, Origin.AddHours(1));
            SeedCountdown(runtime, frozen);
            Tick(runtime);

            var faulted = runtime.Countdown;
            Assert.Equal(CountdownLifecycleState.FAULTED, faulted.Lifecycle);
            Assert.Equal(CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION, faulted.RefusalCode);
            Assert.Equal(1, faulted.Generation);
            var decisionBefore = Assert.IsType<NextDispatchDelayDecision>(faulted.Decision);

            // The release really happens while the countdown is FAULTED: a Manual decision here WOULD
            // plan successfully, so any re-entry of the gate would be immediately visible.
            TickUntilRelease(runtime);
            Assert.Null(runtime.State.ActiveJob);

            // The release changes nothing: same instance, same lifecycle, same generation, same
            // decision and same refusal. No sampling, no increment, no clearing and no retry.
            Assert.Same(faulted, runtime.Countdown);
            Assert.Equal(CountdownLifecycleState.FAULTED, runtime.Countdown.Lifecycle);
            Assert.Equal(1, runtime.Countdown.Generation);
            Assert.Equal(decisionBefore, runtime.Countdown.Decision);
            Assert.Equal(CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION, runtime.Countdown.RefusalCode);
            Assert.False(runtime.Countdown.Frozen);
            Assert.False(runtime.Countdown.DispatchAdmissible);

            // The refused generation never dispatched, and the released Job was not replaced.
            Assert.Equal("SYN-JOB-1", runtime.State.Queue.LastDispatch?.JobId);
            Assert.Null(runtime.State.ActiveJob);

            // Later ticks, including further releases, still cannot revive it.
            AssertFaultIsStable(runtime, CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_faulted_countdown_consumes_no_reading_and_starts_no_generation_across_a_release()
    {
        // The reading is VALID and current, so if the release re-entered StartNextDecision the plan
        // would be ACCEPTED and the countdown would become ACTIVE_FROZEN with a new generation and a
        // consumed reading. Staying on the same instance therefore proves no sampling took place.
        var reading = Reading(520, Origin);
        var runtime = Create(
            SimulatorScenarioId.NORMAL_COMPLETION, AutoRange(600, 660, windowSeconds: 300, fixedRhInput: reading));
        try
        {
            TickUntilActiveJob(runtime);

            // The plan the release would attempt really is accepted: the refusal below is a FAULT, not
            // a missing premise.
            var head = runtime.State.Queue.Entries[0];
            var wouldSucceed = PlannedDecision(
                runtime, AutoRange(600, 660, 300).Configuration, head, reading, Origin, generation: 2);
            Assert.Equal(2, wouldSucceed.CountdownGeneration);

            SeedCountdown(runtime, NextDispatchCountdownCoordinator.FaultNextGeneration(
                DispatchedCountdown(runtime, AutoRange(600, 660, 300)),
                2,
                CountdownRefusalCode.RH_STALE));
            var faulted = runtime.Countdown;
            Assert.Equal(CountdownLifecycleState.FAULTED, faulted.Lifecycle);
            Assert.Null(faulted.Decision);

            TickUntilRelease(runtime);

            // No generation was consumed for a decision: the identity, the generation and the refusal
            // are exactly the seeded ones.
            Assert.Same(faulted, runtime.Countdown);
            Assert.Equal(2, runtime.Countdown.Generation);
            Assert.Equal(CountdownRefusalCode.RH_STALE, runtime.Countdown.RefusalCode);
            Assert.Null(runtime.Countdown.Decision);
            Assert.Equal(0, runtime.Countdown.RemainingSeconds);

            // The previous dispatch is untouched and no new dispatch followed.
            Assert.Equal("SYN-JOB-1", runtime.State.Queue.LastDispatch?.JobId);
            Assert.Null(runtime.State.ActiveJob);
            Assert.Equal(CountdownLifecycleState.FAULTED, runtime.ProjectWorkspace().Countdown.Lifecycle);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_faulted_countdown_never_dispatches_the_head_after_the_release()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Manual(0));
        try
        {
            TickUntilActiveJob(runtime);
            // A countdown-intrinsic refusal, so the seeded fault is honest for a MANUAL configuration
            // that has no RH input at all.
            SeedCountdown(runtime, NextDispatchCountdownCoordinator.FaultNextGeneration(
                NextDispatchCountdown.None,
                1,
                CountdownRefusalCode.DECISION_TIMESTAMP_INVALID));
            var faulted = runtime.Countdown;

            // A zero-delay Manual configuration would dispatch on the very next gate opening, so a
            // dispatch here would be a fault-clearing retry.
            TickUntilRelease(runtime);
            for (var tick = 0; tick < 10; tick++) Tick(runtime);

            Assert.Same(faulted, runtime.Countdown);
            Assert.Equal(CountdownRefusalCode.DECISION_TIMESTAMP_INVALID, runtime.Countdown.RefusalCode);
            Assert.Equal(1, runtime.Countdown.Generation);
            Assert.Equal("SYN-JOB-1", runtime.State.Queue.LastDispatch?.JobId);
            Assert.Null(runtime.State.ActiveJob);
            Assert.NotEmpty(runtime.State.Queue.Entries);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_cancelled_countdown_restarts_with_the_full_applied_delay()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Manual(5));
        try
        {
            var head = TickUntilActiveJob(runtime);
            var cancelled = NextDispatchCountdownCoordinator.Cancel(
                FrozenCountdown(runtime, Manual(5).Configuration, head, Origin), out var cancelRefusal);
            Assert.Equal(CountdownRefusalCode.NONE, cancelRefusal);
            Assert.Equal(CountdownLifecycleState.CANCELLED, cancelled.Lifecycle);
            Assert.Equal(5, cancelled.RemainingSeconds);
            SeedCountdown(runtime, cancelled);

            // The active Job keeps the gate closed until its release, which is the explicit lifecycle
            // event that restarts a CANCELLED generation.
            for (var tick = 0; tick < 60 && runtime.Countdown.Lifecycle != CountdownLifecycleState.ACTIVE_FROZEN; tick++)
            {
                Tick(runtime);
            }

            // A closed-by-premise CANCELLED generation is still an explicit lifecycle trigger: it
            // restarts on the NEXT generation with the FULL applied delay.
            Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, runtime.Countdown.Lifecycle);
            Assert.Equal(2, runtime.Countdown.Generation);
            Assert.Equal(5, runtime.Countdown.RemainingSeconds);
            Assert.Null(runtime.Countdown.RefusalCode);
            Assert.NotNull(runtime.Countdown.Decision);
        }
        finally { await runtime.DisposeAsync(); }
    }

    // ---------------------------------------------------------------- published timeline evidence

    /// <summary>The first published revision that carries an Active Job, after at most 40 ticks.</summary>
    private static (RuntimeState State, ActiveCleaningJobState Job) FirstActiveJob(SimulatorRuntime runtime)
    {
        for (var tick = 0; tick < 40 && runtime.State.ActiveJob is null; tick++)
        {
            Tick(runtime);
        }

        var state = runtime.State;
        return (state, Assert.IsType<ActiveCleaningJobState>(state.ActiveJob));
    }

    [Fact]
    public async Task The_published_timeline_walks_the_whole_dispatch_cycle_of_one_Job()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION);
        try
        {
            var observed = new List<DispatchTimelineStage>();
            var firstJob = new Dictionary<DispatchTimelineStage, ActiveCleaningJobState>();
            for (var tick = 0; tick < 60; tick++)
            {
                Tick(runtime);
                var workspace = runtime.ProjectWorkspace();
                var stage = workspace.Timeline.Stage;
                if (observed.Count == 0 || observed[^1] != stage)
                {
                    observed.Add(stage);
                    if (workspace.ActiveJob is { } job)
                    {
                        firstJob.TryAdd(stage, job);
                    }
                }

                if (stage == DispatchTimelineStage.COMPLETE)
                {
                    break;
                }
            }

            // One Job, walked step by step: every stage the published evidence can complete appears
            // once, in the Owner-approved order, and no stage is invented or skipped.
            Assert.Equal(
                new[]
                {
                    DispatchTimelineStage.PRECHECK,
                    DispatchTimelineStage.MOVE_TO_P1,
                    DispatchTimelineStage.OPEN_IV,
                    DispatchTimelineStage.P2,
                    DispatchTimelineStage.P3,
                    DispatchTimelineStage.P4,
                    DispatchTimelineStage.P5,
                    DispatchTimelineStage.P6,
                    DispatchTimelineStage.CLOSE_IV,
                    DispatchTimelineStage.MANDATORY_SAFE_RETURN,
                    DispatchTimelineStage.COMPLETE,
                },
                observed);

            // The two pre-verification steps share ONE phase presentation (P1, index zero). The only
            // published difference is the open COMMAND, and neither revision carries a measured
            // resolution: the command is never inferred from a resolution here.
            var waiting = firstJob[DispatchTimelineStage.MOVE_TO_P1];
            Assert.Equal(JobPhase.P1, waiting.Phase);
            Assert.Equal(0, waiting.PhaseIndex);
            Assert.Equal(ValveOpenCommandState.NOT_COMMANDED, waiting.ValveOpenCommand);
            Assert.Null(waiting.ValveOpenResolution);

            var commanded = firstJob[DispatchTimelineStage.OPEN_IV];
            Assert.Equal(JobPhase.P1, commanded.Phase);
            Assert.Equal(0, commanded.PhaseIndex);
            Assert.Equal(ValveOpenCommandState.OPEN_COMMANDED, commanded.ValveOpenCommand);
            Assert.Null(commanded.ValveOpenResolution);

            // A verified phase presents itself, and the stage is the step AFTER it.
            var verifiedSteps = new (DispatchTimelineStage Stage, JobPhase Phase, int Index)[]
            {
                (DispatchTimelineStage.P2, JobPhase.P1, 1),
                (DispatchTimelineStage.P3, JobPhase.P2, 2),
                (DispatchTimelineStage.P4, JobPhase.P3, 3),
                (DispatchTimelineStage.P5, JobPhase.P4, 4),
                (DispatchTimelineStage.P6, JobPhase.P5, 5),
                (DispatchTimelineStage.CLOSE_IV, JobPhase.P6, 6),
            };

            foreach (var (stage, phase, index) in verifiedSteps)
            {
                var job = firstJob[stage];
                Assert.Equal(phase, job.Phase);
                Assert.Equal(index, job.PhaseIndex);
                Assert.Equal(ValveOpenCommandState.OPEN_COMMANDED, job.ValveOpenCommand);
            }

            Assert.Equal("OPEN_CONFIRMED", firstJob[DispatchTimelineStage.P2].ValveOpenResolution);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task The_published_phase_alone_drives_phase_progression()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION);
        try
        {
            var (baseline, job) = FirstActiveJob(runtime);

            // Only the published phase identity changes between the six cases: nothing else in the
            // revision is touched, so the mapping provably reads the published phase.
            var verified = job with
            {
                Lifecycle = JobLifecycle.RUNNING,
                ValveOpenCommand = ValveOpenCommandState.OPEN_COMMANDED,
            };
            var expected = new (JobPhase Phase, int Index, DispatchTimelineStage Stage)[]
            {
                (JobPhase.P1, 1, DispatchTimelineStage.P2),
                (JobPhase.P2, 2, DispatchTimelineStage.P3),
                (JobPhase.P3, 3, DispatchTimelineStage.P4),
                (JobPhase.P4, 4, DispatchTimelineStage.P5),
                (JobPhase.P5, 5, DispatchTimelineStage.P6),
                (JobPhase.P6, 6, DispatchTimelineStage.CLOSE_IV),
            };

            foreach (var (phase, index, stage) in expected)
            {
                var projected = baseline with { ActiveJob = verified with { Phase = phase, PhaseIndex = index } };
                Assert.Equal(
                    stage,
                    DispatchWorkspaceProjector.CurrentStage(projected, NextDispatchCountdown.None, out var failed));
                Assert.False(failed);
            }
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Theory]
    [InlineData(JobLifecycle.ABORTING)]
    [InlineData(JobLifecycle.SAFE_RETURN_CLOSE_VALVE)]
    [InlineData(JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED)]
    [InlineData(JobLifecycle.SAFE_RETURN_TO_STANDBY)]
    [InlineData(JobLifecycle.SAFE_RETURN_VERIFY_STANDBY)]
    public async Task Every_Mandatory_Safe_Return_lifecycle_overrides_the_ordinary_phase_step(JobLifecycle lifecycle)
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION);
        try
        {
            var (baseline, job) = FirstActiveJob(runtime);

            // A mid-Cleaning Job presentation: the ordinary mapping would report P4. The lifecycle
            // must win for every Mandatory Safe Return lifecycle, and none of them is a failure.
            var projected = baseline with
            {
                ActiveJob = job with { Phase = JobPhase.P3, PhaseIndex = 3, Lifecycle = lifecycle },
            };
            var stage = DispatchWorkspaceProjector.CurrentStage(projected, NextDispatchCountdown.None, out var failed);
            Assert.Equal(DispatchTimelineStage.MANDATORY_SAFE_RETURN, stage);
            Assert.False(failed);

            var timeline = DispatchWorkspaceProjector.ProjectTimeline(projected, NextDispatchCountdown.None, stage, failed);
            Assert.Single(timeline.Steps, step => step.State == DispatchTimelineStepState.CURRENT);
            Assert.DoesNotContain(timeline.Steps, step => step.State == DispatchTimelineStepState.FAILED);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task A_failed_Safe_Return_projects_the_failed_mandatory_return_step_and_no_progress()
    {
        var runtime = Create(SimulatorScenarioId.AXIS_STANDBY_FAILURE);
        try
        {
            for (var tick = 0; tick < 60 && runtime.State.ActiveJob?.Lifecycle != JobLifecycle.SAFE_RETURN_FAILED; tick++)
            {
                Tick(runtime);
            }

            // ONE publication for the published Job and the projected document, so the lifecycle read
            // and the timeline read describe the same revision.
            var publication = runtime.Publication;
            var job = Assert.IsType<ActiveCleaningJobState>(publication.Current.ActiveJob);
            Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, job.Lifecycle);

            var workspace = runtime.ProjectWorkspace(publication);
            Assert.Equal(DispatchTimelineStage.MANDATORY_SAFE_RETURN, workspace.Timeline.Stage);
            var failedStep = Assert.Single(
                workspace.Timeline.Steps, step => step.State == DispatchTimelineStepState.FAILED);
            Assert.Equal(DispatchTimelineStage.MANDATORY_SAFE_RETURN, failedStep.Stage);

            // A step carries exactly ONE state, so a failed cycle has no CURRENT step at all: the
            // failed step IS the frontier. Expecting both a FAILED and a CURRENT step would demand two
            // states for one step.
            Assert.DoesNotContain(
                workspace.Timeline.Steps,
                step => step.State == DispatchTimelineStepState.CURRENT);

            // A failure never invents progress: earlier steps stay complete, later steps stay pending.
            Assert.All(
                workspace.Timeline.Steps.Take(failedStep.Ordinal - 1),
                step => Assert.Equal(DispatchTimelineStepState.COMPLETE, step.State));
            Assert.All(
                workspace.Timeline.Steps.Skip(failedStep.Ordinal),
                step => Assert.Equal(DispatchTimelineStepState.PENDING, step.State));
        }
        finally { await runtime.DisposeAsync(); }
    }

    // ---------------------------------------------------------------- startup token

    [Fact]
    public void The_demo_token_is_explicit_and_an_unrecognised_value_refuses_startup()
    {
        Assert.True(RuntimeHostOptions.TryLoadSynthetic(
            DeviceProfile.SIMULATOR, 5181, _ => null, out var absent, out var absentCode, out _));
        Assert.Null(absent.TimeDelay);
        Assert.Equal(string.Empty, absentCode);

        Assert.True(RuntimeHostOptions.TryLoadSynthetic(
            DeviceProfile.SIMULATOR, 5181,
            name => name == RuntimeHostOptions.TimeDelayVariable ? "NONE" : null,
            out var none, out _, out _));
        Assert.Null(none.TimeDelay);

        Assert.True(RuntimeHostOptions.TryLoadSynthetic(
            DeviceProfile.SIMULATOR, 5181,
            name => name == RuntimeHostOptions.TimeDelayVariable ? "MANUAL_5" : null,
            out var manual, out _, out _));
        Assert.NotNull(manual.TimeDelay);
        Assert.Equal(TimeDelayMode.MANUAL_FIXED, manual.TimeDelay!.Configuration.Mode);
        Assert.Equal(5, manual.TimeDelay.Configuration.ManualDelaySeconds);

        Assert.True(RuntimeHostOptions.TryLoadSynthetic(
            DeviceProfile.SIMULATOR, 5181,
            name => name == RuntimeHostOptions.TimeDelayVariable ? "AUTO_VERY_DIRTY" : null,
            out var auto, out _, out _));
        Assert.NotNull(auto.TimeDelay);
        Assert.Equal(TimeDelayMode.AUTO_RH_LINEAR_FROZEN, auto.TimeDelay!.Configuration.Mode);
        Assert.Equal(BoilerCondition.VERY_DIRTY, auto.TimeDelay.Configuration.ActiveBoilerCondition);
        Assert.True(auto.TimeDelay.RampRhTemperature);

        foreach (var invalid in new[] { "MANUAL_", "MANUAL_3601", "MANUAL_-1", "AUTO_", "AUTO_clean", "ZERO", "5" })
        {
            Assert.False(RuntimeHostOptions.TryLoadSynthetic(
                DeviceProfile.SIMULATOR, 5181,
                name => name == RuntimeHostOptions.TimeDelayVariable ? invalid : null,
                out _, out var code, out var detail));
            Assert.Equal(RuntimeHostOptions.InvalidTimeDelayDemo, code);
            Assert.Contains(RuntimeHostOptions.TimeDelayVariable, detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_workspace_document_reuses_the_snapshot_and_carries_no_execution_authority()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION, Manual(5));
        try
        {
            // ONE publication for both documents: "reuses the snapshot" is a reference-identity claim,
            // and reference identity only exists inside a single publication.
            var publication = runtime.Publication;
            var snapshot = runtime.ProjectSnapshot(publication);
            var workspace = runtime.ProjectWorkspace(publication);

            Assert.Equal(snapshot.Revision, workspace.Revision);
            Assert.Equal(snapshot.GeneratedAt, workspace.GeneratedAt);
            Assert.Equal(snapshot.DeviceProfile, workspace.DeviceProfile);
            Assert.Equal(snapshot.Queue, workspace.Queue);
            Assert.Same(snapshot.Queue, workspace.Queue);
            Assert.Same(snapshot.ActiveJob, workspace.ActiveJob);
            Assert.Equal(snapshot.Sequence.AutoSequence, workspace.AutoSequence);
            Assert.Equal(snapshot.Sequence.Mode, workspace.Mode);
            Assert.Equal(CountdownLifecycleState.NONE, workspace.Countdown.Lifecycle);
            Assert.Equal(DispatchTimelineStage.PRECHECK, workspace.Timeline.Stage);
            Assert.Equal(
                DispatchWorkspaceProjectorSteps(),
                workspace.Timeline.Steps.Select(step => step.Stage));
            Assert.All(workspace.Timeline.Steps, step => Assert.False(string.IsNullOrWhiteSpace(step.Label)));
            Assert.All(workspace.Timeline.Steps, step => Assert.InRange(step.Ordinal, 1, 13));
        }
        finally { await runtime.DisposeAsync(); }
    }

    private static IEnumerable<DispatchTimelineStage> DispatchWorkspaceProjectorSteps() =>
        DispatchWorkspaceProjector.OrderedSteps.Select(step => step.Stage);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
