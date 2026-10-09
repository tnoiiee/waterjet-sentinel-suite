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
            var steps = SimulatorScenarioCatalogue.Create(runtime.State.Sensors).Get(id).Steps;
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
            var steps = SimulatorScenarioCatalogue.Create(runtime.State.Sensors).Get(runtime.ScenarioId).Steps;
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
            for (var i = 0; i <= 7; i++) Tick(runtime);
            var publication = runtime.Publication;
            var snapshot = runtime.ProjectSnapshot(publication);
            var feed = RuntimeDeltaFeed.From(publication);
            Assert.Equal(snapshot.Revision, feed.CurrentRevision);
            Assert.Equal(snapshot.Revision, feed.NewestDeltaRevision);
            Assert.Single(snapshot.Queue.Entries);
            var job = Assert.IsType<ActiveCleaningJobState>(snapshot.ActiveJob);
            var safeReturn = Assert.IsType<SafeReturnState>(job.SafeReturn);
            Assert.Equal("CLOSE_COMMANDED", safeReturn.Valve.Command);
            Assert.Equal("NOT_COMMANDED", safeReturn.Axis.Command);
            var critical = Assert.IsType<CriticalPumpEvent>(snapshot.Sequence.Critical);
            Assert.True(critical.ModalOpen);
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
