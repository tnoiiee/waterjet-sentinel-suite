using System.Reflection;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime;
using Wjss.Runtime.Core;
using Wjss.Runtime.Core.Simulator;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>Host composition and read-only observation; no listener, hardware or commands.</summary>
public sealed class SimulatorFastTrackTests
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);

    private static bool Load(string? label, out RuntimeHostOptions options, out string code) =>
        RuntimeHostOptions.TryLoadSynthetic(DeviceProfile.SIMULATOR, 5181,
            name => name == RuntimeHostOptions.ScenarioVariable ? label : null,
            out options, out code, out _);

    [Fact]
    public void Startup_Exact_Scenario_Selection_And_Single_Read()
    {
        Assert.True(Load(null, out var idle, out _));
        Assert.Equal(SimulatorScenarioId.IDLE, idle.Scenario);
        foreach (var id in Enum.GetValues<SimulatorScenarioId>())
        {
            var reads = 0;
            Assert.True(RuntimeHostOptions.TryLoadSynthetic(DeviceProfile.SIMULATOR, 5181,
                name => name == RuntimeHostOptions.ScenarioVariable ? (++reads, id.ToString()).Item2 : null,
                out var options, out _, out _));
            Assert.Equal(1, reads);
            Assert.Equal(id, options.Scenario);
        }
        foreach (var invalid in new[] { "", " ", "IDLE ", "idle", "UNKNOWN", "0" })
        {
            Assert.False(Load(invalid, out _, out var code));
            Assert.Equal(RuntimeHostOptions.InvalidSimulatorScenario, code);
        }
    }

    [Fact]
    public void Profile_Gate_Precedes_Scenario_Read()
    {
        foreach (var profile in new[] { DeviceProfile.TEST_HARDWARE, DeviceProfile.PRODUCTION })
        {
            var reads = 0;
            Assert.False(RuntimeHostOptions.TryLoadSynthetic(profile, 5181,
                _ => { reads++; return "bad"; }, out _, out var code, out _));
            Assert.Equal(ProfileStartPolicy.RefusalCode, code);
            Assert.Equal(0, reads);
        }
    }

    private static SimulatorRuntime Create(SimulatorScenarioId id) => SimulatorRuntime.Create(new RuntimeHostOptions
    {
        Profile = DeviceProfile.SIMULATOR, Port = 5181, Scenario = id,
        SyntheticSeed = 20261009, TickIntervalMilliseconds = 1000,
        StateHistoryCapacity = 32, DeltaHistoryCapacity = 32,
    }, new FixedClock(Origin));

    // Exercise exactly the private non-overlapping host tick path without starting the timer.
    private static void Tick(SimulatorRuntime runtime) =>
        typeof(SimulatorRuntime).GetMethod("ExecuteTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(runtime, null);

    [Theory]
    [InlineData(SimulatorScenarioId.NORMAL_COMPLETION)]
    [InlineData(SimulatorScenarioId.PUMP_TRIP)]
    [InlineData(SimulatorScenarioId.VALVE_CLOSE_FAILURE)]
    [InlineData(SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO)]
    public async Task Each_Accepted_Revision_Has_One_Delta_And_At_Most_One_Ordered_Event(SimulatorScenarioId id)
    {
        var runtime = Create(id);
        try
        {
            // Ordinal zero is the first tick: step tick 0 is visible at Runtime revision 2.
            Assert.Equal(1, runtime.Publication.Current.Revision);
            Assert.Equal(0, runtime.ScenarioCursor);
            var steps = SimulatorScenarioCatalogue.Create(runtime.State.Sensors, runtime.Options.PressureThresholds).Get(id).Steps;
            for (var ordinal = 0; ordinal < steps.Count + 3; ordinal++)
            {
                var before = runtime.Publication;
                var cursor = runtime.ScenarioCursor;
                Tick(runtime);
                var after = runtime.Publication;
                Assert.Equal(before.Generation + 1, after.Generation);
                Assert.Equal(before.Current.Revision + 1, after.Current.Revision);
                Assert.Equal(after.Current.Revision, after.Deltas[0].Revision);
                Assert.Equal(before.Current.Revision, after.Deltas[0].PreviousRevision);
                Assert.Equal(Math.Min(cursor + 1, steps.Count), runtime.ScenarioCursor);
                Assert.Equal(ordinal + 1, runtime.AcceptedTicks);
                var snapshot = runtime.ProjectSnapshot(after);
                var feed = RuntimeDeltaFeed.From(after);
                Assert.Equal(snapshot.Revision, feed.CurrentRevision);
                Assert.Equal(snapshot.Revision, feed.NewestDeltaRevision);
                Assert.Equal(snapshot.Queue.TotalQueued, snapshot.Queue.Entries.Count);
            }
            Assert.Equal(steps.Count, runtime.ScenarioCursor);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task Refused_Synthetic_Tick_Leaves_Event_Pending_And_Does_Not_Adopt()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION);
        try
        {
            var before = runtime.Publication;
            var state = runtime.Sequencing;
            var retention = runtime.Retention;
            // Force the pre-publication synthetic refusal; no event can be consumed.
            typeof(SimulatorRuntime).GetField("_nextTickInstant", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(runtime, before.Current.GeneratedAtUtc);
            Tick(runtime);
            Assert.Equal(before.Generation, runtime.Publication.Generation);
            Assert.Same(state, runtime.Sequencing);
            Assert.Same(retention, runtime.Retention);
            Assert.Equal(0, runtime.ScenarioCursor);
            Assert.Equal(0, runtime.AcceptedTicks);
            Tick(runtime);
            Assert.Equal(1, runtime.ScenarioCursor);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task Publication_Refusal_Preserves_Pending_Input_And_Ordinal()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION);
        try
        {
            var before = runtime.Publication;
            var state = runtime.Sequencing;
            var retention = runtime.Retention;
            var writerField = typeof(SimulatorRuntime).GetField("_writer", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var realWriter = writerField.GetValue(runtime);
            // A writer whose publication is already ahead refuses the expected revision.
            var ahead = before.Current with
            {
                Revision = before.Current.Revision + 1,
                GeneratedAtUtc = before.Current.GeneratedAtUtc.AddSeconds(1),
            };
            writerField.SetValue(runtime, RuntimePublicationStore.Create(ahead).CreateWriter());
            try
            {
                Tick(runtime);
                Assert.Equal(before.Generation, runtime.Publication.Generation);
                Assert.Same(state, runtime.Sequencing);
                Assert.Same(retention, runtime.Retention);
                Assert.Equal(0, runtime.ScenarioCursor);
                Assert.Equal(0, runtime.AcceptedTicks);
            }
            finally { writerField.SetValue(runtime, realWriter); }
            Tick(runtime);
            Assert.Equal(1, runtime.ScenarioCursor);
            Assert.Equal(2, runtime.Publication.Current.Revision);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task No_Op_Input_Consumes_After_Publication_Without_Sequencing_Change()
    {
        var runtime = Create(SimulatorScenarioId.IDLE);
        try
        {
            var scenarioField = typeof(SimulatorRuntime).GetField("_scenario", BindingFlags.NonPublic | BindingFlags.Instance)!;
            scenarioField.SetValue(runtime, new SimulatorScenario(SimulatorScenarioId.IDLE, "test-only no-op",
                [new SimulatorScenarioStep(0, new ObservePumpState(SimulatorScenarioCatalogue.AtTick(0), PumpObservation.READY))]));
            var before = runtime.Sequencing;
            Tick(runtime);
            Assert.Equal(1, runtime.ScenarioCursor);
            Assert.Equal(before.Mode, runtime.Sequencing.Mode);
            Assert.Equal(before.QueueRevision, runtime.Sequencing.QueueRevision);
            Assert.Contains(runtime.Retention.EvidenceLog, e => e.Outcome == SequencingOutcome.NO_OP);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task Refused_Kernel_Input_Still_Consumes_After_Publication()
    {
        var runtime = Create(SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO);
        try
        {
            var steps = SimulatorScenarioCatalogue.Create(runtime.State.Sensors, runtime.Options.PressureThresholds).Get(runtime.ScenarioId).Steps;
            foreach (var _ in steps) Tick(runtime);
            Assert.Equal(steps.Count, runtime.ScenarioCursor);
            Assert.Contains(runtime.Retention.EvidenceLog, e => e.Outcome == Wjss.Runtime.Core.Sequencing.SequencingOutcome.REFUSED);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task Snapshot_Observes_Queue_Job_And_Safe_Return_From_One_Publication()
    {
        var runtime = Create(SimulatorScenarioId.PUMP_TRIP);
        try
        {
            var scenario = SimulatorScenarioCatalogue
                .Create(runtime.State.Sensors, runtime.Options.PressureThresholds)
                .Get(runtime.ScenarioId);
            var criticalStep = Assert.Single(
                scenario.Steps,
                step => step.Event is ObservePumpState
                {
                    Observation: PumpObservation.TRIP,
                });

            for (var acceptedOrdinal = 0;
                 acceptedOrdinal <= criticalStep.Tick;
                 acceptedOrdinal++)
            {
                Tick(runtime);
            }

            var publication = runtime.Publication;
            var snapshot = runtime.ProjectSnapshot(publication);
            var feed = RuntimeDeltaFeed.From(publication);
            Assert.Equal(snapshot.Revision, feed.CurrentRevision);
            Assert.Equal(snapshot.Revision, feed.NewestDeltaRevision);
            Assert.Single(snapshot.Queue.Entries);
            var job = Assert.IsType<ActiveCleaningJobState>(snapshot.ActiveJob);
            var safeReturn = Assert.IsType<SafeReturnState>(job.SafeReturn);
            Assert.Equal("CLOSE_COMMANDED", safeReturn.Valve.Command);
            Assert.Equal("RETURN_COMMANDED", safeReturn.Axis.Command);
            var critical = Assert.IsType<CriticalPumpEvent>(snapshot.Sequence.Critical);
            Assert.True(critical.ModalOpen);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task Two_Jobs_Complete_Sequentially_With_An_Automatic_Second_Dispatch()
    {
        var runtime = Create(SimulatorScenarioId.TWO_JOB_SEQUENTIAL_COMPLETION);
        try
        {
            var scenario = SimulatorScenarioCatalogue.Create(runtime.State.Sensors, runtime.Options.PressureThresholds)
                .Get(runtime.ScenarioId);
            Assert.Single(scenario.Steps, step => step.Event is DispatchHead);
            var firstSensor = runtime.State.Sensors[0];
            var secondSensor = runtime.State.Sensors[1];
            var firstCloseTick = scenario.Steps.First(
                step => step.Event is ValveSupervisionObserved { LowerDetected: true }).Tick;
            var firstReleaseRevision = 0;
            var secondDispatchRevision = 0;
            string? secondEntryId = null;
            var dispatchCount = 0;
            for (var ordinal = 0; ordinal <= scenario.Steps[^1].Tick + 2; ordinal++)
            {
                var before = runtime.Publication;
                var priorDispatchId = before.Current.Queue.LastDispatch?.DispatchId;
                Tick(runtime);
                var after = runtime.Publication;
                var snapshot = runtime.ProjectSnapshot(after);
                Assert.Equal(before.Current.Revision + 1, after.Current.Revision);
                Assert.Equal(before.Current.Revision, after.Deltas[0].PreviousRevision);
                Assert.Equal(after.Current.Revision, after.Deltas[0].Revision);
                Assert.Equal(ordinal + 1, runtime.AcceptedTicks);
                Assert.InRange(after.Current.Sensors.Count(sensor => sensor.IsActiveJobTarget), 0, 1);
                Assert.Equal(snapshot.ActiveJob?.TargetSensorId,
                    after.Current.Sensors.SingleOrDefault(sensor => sensor.IsActiveJobTarget)?.SensorId);

                var dispatch = snapshot.Queue.LastDispatch;
                if (dispatch?.DispatchId != priorDispatchId)
                {
                    dispatchCount++;
                    Assert.Equal($"SYN-JOB-{dispatchCount}", dispatch!.JobId);
                    Assert.Equal(1, dispatch!.PositionBefore);
                    Assert.Equal(before.Current.Queue.Entries[0].EntryId, dispatch.QueueEntryId);
                    Assert.Equal(before.Current.Queue.Entries[0].SensorId, dispatch.SensorId);
                    Assert.Equal(before.Current.Queue.Revision + 1, snapshot.Queue.Revision);
                    if (dispatchCount == 2)
                    {
                        secondDispatchRevision = snapshot.Revision;
                        Assert.True(firstReleaseRevision > 0);
                        Assert.True(secondDispatchRevision > firstReleaseRevision);
                        Assert.Equal(secondEntryId, dispatch.QueueEntryId);
                        Assert.Equal("SYN-JOB-2", snapshot.ActiveJob?.JobId);
                        Assert.Null(snapshot.ActiveJob?.SafeReturn);
                    }
                }

                if (ordinal == 2)
                {
                    Assert.Collection(snapshot.Queue.Entries,
                        head => Assert.Equal(firstSensor.SensorId, head.SensorId),
                        next => { Assert.Equal(secondSensor.SensorId, next.SensorId); secondEntryId = next.EntryId; });
                }
                if (snapshot.ActiveJob is { JobId: "SYN-JOB-1" } first)
                {
                    Assert.Equal(secondEntryId, Assert.Single(snapshot.Queue.Entries).EntryId);
                    Assert.Equal(firstSensor.SensorId, first.TargetSensorId);
                    Assert.Equal(firstSensor.AssignedWaterJetId, first.JetId);
                    Assert.Equal(firstSensor.AssignedIsolationValveId, first.ValveId);
                    Assert.Equal("SYN-JOB-1", dispatch?.JobId);
                    if (first.SafeReturn is not null)
                    {
                        Assert.Equal("CLOSE_COMMANDED", first.SafeReturn.Valve.Command);
                        Assert.Equal("RETURN_COMMANDED", first.SafeReturn.Axis.Command);
                    }
                }
                if (ordinal == firstCloseTick)
                {
                    Assert.Equal("SYN-JOB-1", snapshot.ActiveJob?.JobId);
                    Assert.NotNull(snapshot.ActiveJob?.SafeReturn?.Valve.Resolution);
                    Assert.Equal("ABSENT", snapshot.ActiveJob?.SafeReturn?.Axis.Standby);
                    Assert.Equal(1, dispatchCount);
                }
                if (snapshot.ActiveJob is { JobId: "SYN-JOB-2" } second)
                {
                    Assert.Equal(secondSensor.SensorId, second.TargetSensorId);
                    Assert.Equal(secondSensor.AssignedWaterJetId, second.JetId);
                    Assert.Equal(secondSensor.AssignedIsolationValveId, second.ValveId);
                    Assert.Empty(snapshot.Queue.Entries);
                }
                if (snapshot.Sequence.LastJobOutcome is { JobId: "SYN-JOB-1" }
                    && firstReleaseRevision == 0)
                {
                    firstReleaseRevision = snapshot.Revision;
                    Assert.Null(snapshot.ActiveJob);
                    Assert.Equal(secondEntryId, Assert.Single(snapshot.Queue.Entries).EntryId);
                    Assert.Equal(1, dispatchCount);
                }
            }
            Assert.Equal(2, dispatchCount);
            Assert.True(secondDispatchRevision > firstReleaseRevision);
            Assert.Equal(firstReleaseRevision + 1, secondDispatchRevision);
            var final = runtime.ProjectSnapshot(runtime.Publication);
            Assert.Empty(final.Queue.Entries);
            Assert.Null(final.ActiveJob);
            Assert.Equal("SYN-JOB-2", final.Sequence.LastJobOutcome?.JobId);
            Assert.Equal("COMPLETED", final.Sequence.LastJobOutcome?.Outcome);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<EquipmentFaultState>>(final.Sequence.EquipmentFaults));
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Theory]
    [InlineData(SimulatorScenarioId.VALVE_CLOSE_LOW, "LOWER_LIMIT_SENSOR_FAULT")]
    [InlineData(SimulatorScenarioId.VALVE_CLOSE_FAILURE, "VALVE_LEAK_SUSPECTED")]
    [InlineData(SimulatorScenarioId.VALVE_CLOSE_HIGH, "VALVE_NOT_FULLY_CLOSED")]
    [InlineData(SimulatorScenarioId.PUMP_TRIP, null)]
    [InlineData(SimulatorScenarioId.PAUSE_AFTER_CURRENT_JOB, null)]
    [InlineData(SimulatorScenarioId.AXIS_STANDBY_FAILURE, null)]
    public async Task Blocking_Return_Or_Mode_Never_Auto_Dispatches_The_Queued_Head(
        SimulatorScenarioId id, string? faultCode)
    {
        var runtime = Create(id);
        try
        {
            var steps = SimulatorScenarioCatalogue.Create(runtime.State.Sensors, runtime.Options.PressureThresholds).Get(id).Steps;
            for (var ordinal = 0; ordinal <= steps[^1].Tick + 3; ordinal++) Tick(runtime);
            var snapshot = runtime.ProjectSnapshot(runtime.Publication);
            Assert.Equal("SYN-JOB-1", snapshot.Queue.LastDispatch?.JobId);
            Assert.Equal(runtime.State.Sensors[1].SensorId, Assert.Single(snapshot.Queue.Entries).SensorId);
            if (faultCode is not null)
                Assert.Contains(snapshot.Sequence.EquipmentFaults!, fault => fault.Diagnosis == faultCode && fault.NextDispatchBlocked);
            if (id == SimulatorScenarioId.VALVE_CLOSE_LOW)
                Assert.Equal("COMPLETE_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT", snapshot.Sequence.LastJobOutcome?.QualifiedCompletion);
            if (id == SimulatorScenarioId.PUMP_TRIP)
                Assert.NotNull(snapshot.Sequence.Critical);
            if (id == SimulatorScenarioId.PAUSE_AFTER_CURRENT_JOB)
                Assert.Equal(AutoSequenceMode.PAUSED, snapshot.Sequence.Mode);
            if (id is SimulatorScenarioId.VALVE_CLOSE_FAILURE or SimulatorScenarioId.VALVE_CLOSE_HIGH or SimulatorScenarioId.AXIS_STANDBY_FAILURE)
                Assert.Equal("SYN-JOB-1", snapshot.ActiveJob?.JobId);
        }
        finally { await runtime.DisposeAsync(); }
    }

    // Test-only replacement of one scheduled observation; the Runtime still processes
    // it through ExecuteTick and the atomic publication writer.
    private static void ReplaceScenarioObservation(SimulatorRuntime runtime,
        Func<SequencingEvent, bool> match, Func<SequencingEvent, SequencingEvent> replace)
    {
        var field = typeof(SimulatorRuntime).GetField("_scenario", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var scenario = Assert.IsType<SimulatorScenario>(field.GetValue(runtime));
        var steps = scenario.Steps.ToArray();
        var index = Array.FindIndex(steps, step => match(step.Event));
        Assert.True(index >= 0);
        steps[index] = steps[index] with { Event = replace(steps[index].Event) };
        field.SetValue(runtime, scenario with { Steps = Array.AsReadOnly(steps) });
    }

    [Theory]
    [InlineData("UPPER_LIMIT_SENSOR_FAULT")]
    [InlineData("COMPLETE_WITH_MULTIPLE_VALVE_LIMIT_FAULTS")]
    [InlineData("VALVE_PRESSURE_INPUT_INVALID")]
    public async Task Faulted_First_Job_Cannot_Auto_Dispatch_Second_Job(string faultCase)
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION);
        try
        {
            if (faultCase is "UPPER_LIMIT_SENSOR_FAULT" or "COMPLETE_WITH_MULTIPLE_VALVE_LIMIT_FAULTS")
                ReplaceScenarioObservation(runtime,
                    e => e is ValveSupervisionObserved { UpperDetected: true },
                    e => ((ValveSupervisionObserved)e) with { UpperDetected = false, TimedOut = true });
            if (faultCase == "COMPLETE_WITH_MULTIPLE_VALVE_LIMIT_FAULTS")
                ReplaceScenarioObservation(runtime,
                    e => e is ValveSupervisionObserved { LowerDetected: true },
                    e => ((ValveSupervisionObserved)e) with { LowerDetected = false, TimedOut = true });
            if (faultCase == "VALVE_PRESSURE_INPUT_INVALID")
                ReplaceScenarioObservation(runtime,
                    e => e is ValveSupervisionObserved { LowerDetected: true },
                    e => ((ValveSupervisionObserved)e) with
                    { Pressure = ((ValveSupervisionObserved)e).Pressure! with { Quality = PressureQuality.BAD } });

            var steps = SimulatorScenarioCatalogue.Create(runtime.State.Sensors, runtime.Options.PressureThresholds)
                .Get(runtime.ScenarioId).Steps;
            for (var ordinal = 0; ordinal <= steps[^1].Tick + 3; ordinal++) Tick(runtime);
            var snapshot = runtime.ProjectSnapshot(runtime.Publication);
            Assert.Equal("SYN-JOB-1", snapshot.Queue.LastDispatch?.JobId);
            Assert.Equal(runtime.State.Sensors[1].SensorId, Assert.Single(snapshot.Queue.Entries).SensorId);
            var faults = Assert.IsAssignableFrom<IReadOnlyList<EquipmentFaultState>>(snapshot.Sequence.EquipmentFaults);
            Assert.NotEmpty(faults);
            Assert.All(faults, fault => Assert.True(fault.NextDispatchBlocked));
            if (faultCase == "COMPLETE_WITH_MULTIPLE_VALVE_LIMIT_FAULTS")
            {
                Assert.Equal(2, snapshot.Sequence.EquipmentFaults?.Count);
                Assert.Equal(faultCase, snapshot.Sequence.LastJobOutcome?.QualifiedCompletion);
            }
            else
                Assert.Contains(faults, fault => fault.Diagnosis == faultCase);
            if (faultCase == "VALVE_PRESSURE_INPUT_INVALID")
                Assert.Equal("SYN-JOB-1", snapshot.ActiveJob?.JobId);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task Axis_Standby_Before_Valve_Resolution_Still_Blocks_The_Next_Job()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION);
        try
        {
            var field = typeof(SimulatorRuntime).GetField("_scenario", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var scenario = Assert.IsType<SimulatorScenario>(field.GetValue(runtime));
            var steps = scenario.Steps.ToArray();
            var valveIndex = Array.FindIndex(steps, step => step.Event is ValveSupervisionObserved { LowerDetected: true });
            var axisIndex = Array.FindIndex(steps, step => step.Event is AxisFeedbackObserved { Feedback: AxisFeedbackState.AT_STANDBY });
            Assert.True(valveIndex >= 0 && axisIndex > valveIndex);
            var valve = (ValveSupervisionObserved)steps[valveIndex].Event;
            var axis = (AxisFeedbackObserved)steps[axisIndex].Event;
            steps[valveIndex] = steps[valveIndex] with { Event = axis with { At = SimulatorScenarioCatalogue.AtTick(steps[valveIndex].Tick) } };
            steps[axisIndex] = steps[axisIndex] with { Event = valve with
                { At = SimulatorScenarioCatalogue.AtTick(steps[axisIndex].Tick),
                  Pressure = valve.Pressure! with { At = SimulatorScenarioCatalogue.AtTick(steps[axisIndex].Tick) } } };
            field.SetValue(runtime, scenario with { Steps = Array.AsReadOnly(steps) });
            for (var ordinal = 0; ordinal <= steps[valveIndex].Tick; ordinal++) Tick(runtime);
            var pending = runtime.ProjectSnapshot(runtime.Publication);
            Assert.Equal("SYN-JOB-1", pending.ActiveJob?.JobId);
            Assert.Equal("STANDBY_CONFIRMED", pending.ActiveJob?.SafeReturn?.Axis.Standby);
            Assert.Null(pending.ActiveJob?.SafeReturn?.Valve.Resolution);
            Assert.Equal(runtime.State.Sensors[1].SensorId, Assert.Single(pending.Queue.Entries).SensorId);
            Tick(runtime);
            var released = runtime.ProjectSnapshot(runtime.Publication);
            Assert.Null(released.ActiveJob);
            Assert.Equal("SYN-JOB-1", released.Sequence.LastJobOutcome?.JobId);
            Assert.Equal(runtime.State.Sensors[1].SensorId, Assert.Single(released.Queue.Entries).SensorId);
            Tick(runtime);
            Assert.Equal("SYN-JOB-2", runtime.ProjectSnapshot(runtime.Publication).ActiveJob?.JobId);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task Auto_Dispatch_Leaves_A_Structurally_Invalid_Head_In_Place()
    {
        var runtime = Create(SimulatorScenarioId.NORMAL_COMPLETION);
        try
        {
            var steps = SimulatorScenarioCatalogue.Create(runtime.State.Sensors, runtime.Options.PressureThresholds)
                .Get(runtime.ScenarioId).Steps;
            for (var ordinal = 0; ordinal <= steps[^1].Tick; ordinal++) Tick(runtime);
            var released = runtime.ProjectSnapshot(runtime.Publication);
            Assert.Null(released.ActiveJob);
            var head = Assert.Single(released.Queue.Entries);
            var field = typeof(SimulatorRuntime).GetField("_scenarioTopology", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var topology = Assert.IsType<SequencingTopology>(field.GetValue(runtime));
            field.SetValue(runtime, new SequencingTopology(
                topology.SensorAssignments.Where(assignment => assignment.SensorId != head.SensorId).ToArray(),
                topology.NonSensorPositionIds));
            for (var extraTick = 0; extraTick < 3; extraTick++)
            {
                Tick(runtime);
                var snapshot = runtime.ProjectSnapshot(runtime.Publication);
                Assert.Null(snapshot.ActiveJob);
                Assert.Equal(head.EntryId, Assert.Single(snapshot.Queue.Entries).EntryId);
                Assert.Equal(released.Queue.Revision, snapshot.Queue.Revision);
                Assert.Equal("SYN-JOB-1", snapshot.Queue.LastDispatch?.JobId);
            }
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public void Inspector_And_Host_Keep_Get_Only_Read_Only_Targets()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var root = directory.FullName;
        var page = File.ReadAllText(Path.Combine(root, "apps", "runtime", "Inspector", "index.html"));
        var host = File.ReadAllText(Path.Combine(root, "apps", "runtime", "Program.cs"));
        Assert.Contains("SIMULATOR / READ ONLY", page, StringComparison.Ordinal);
        foreach (var target in new[] { "kv-sequencing", "queue-body", "safeReturn.valve", "safeReturn.axis" })
            Assert.Contains(target, page, StringComparison.Ordinal);
        foreach (var path in new[] { "RUNTIME_PATH", "DELTAS_PATH", "SNAPSHOT_PREFIX" })
            Assert.Contains("fetchJson(" + path + ")", page, StringComparison.Ordinal);
        Assert.Contains("method: \"GET\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPost(", host, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPut(", host, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPatch(", host, StringComparison.Ordinal);
        Assert.DoesNotContain("MapDelete(", host, StringComparison.Ordinal);
        Assert.DoesNotContain("btn-admit", page, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1", host, StringComparison.Ordinal); // listener stays loopback
        Assert.True(host.IndexOf("TryRequireStartable(profile", StringComparison.Ordinal) <
            host.IndexOf("TryLoadSynthetic(", StringComparison.Ordinal));
        Assert.True(host.IndexOf("TryLoadSynthetic(", StringComparison.Ordinal) <
            host.IndexOf("CreateSlimBuilder(", StringComparison.Ordinal));
        Assert.Contains("read-only observation", RuntimeStatus.QueuePlaceholderLabel, StringComparison.Ordinal);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
