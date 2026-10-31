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

            var workspace = runtime.ProjectWorkspace();
            Assert.Equal(SchemaIds.DispatchWorkspace, workspace.Schema);
            Assert.Equal(SchemaIds.DispatchWorkspaceKind, workspace.Kind);
            Assert.Equal(DispatchWorkspace.SimulationNote, workspace.Simulation);
            Assert.Equal(runtime.Publication.Current.Revision, workspace.Revision);

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
            // documents of one revision can never disagree.
            var snapshot = runtime.ProjectSnapshot();
            Assert.Same(snapshot.Queue, workspace.Queue);
            Assert.Same(snapshot.ActiveJob, workspace.ActiveJob);
            Assert.Equal(DispatchTimelineStage.NEXT_DISPATCH_COUNTDOWN, workspace.Timeline.Stage);
            Assert.Equal(13, workspace.Timeline.Steps.Count);
            Assert.Equal(
                DispatchWorkspaceProjectorSteps(),
                workspace.Timeline.Steps.Select(step => step.Stage));
            Assert.Single(workspace.Timeline.Steps.Where(step => step.State == DispatchTimelineStepState.CURRENT));

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
            var dispatchId = job.DispatchId;
            Tick(runtime);
            Tick(runtime);
            Assert.Equal(CountdownLifecycleState.DISPATCHED, runtime.Countdown.Lifecycle);
            Assert.Equal(dispatchId, Assert.IsType<ActiveCleaningJobState>(runtime.State.ActiveJob).DispatchId);
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
            var snapshot = runtime.ProjectSnapshot();
            var workspace = runtime.ProjectWorkspace();

            Assert.Equal(snapshot.Revision, workspace.Revision);
            Assert.Equal(snapshot.GeneratedAt, workspace.GeneratedAt);
            Assert.Equal(snapshot.DeviceProfile, workspace.DeviceProfile);
            Assert.Equal(snapshot.Queue, workspace.Queue);
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
}
