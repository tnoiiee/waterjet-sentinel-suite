using System.Text.Json;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime;
using Wjss.Runtime.Core;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Read-only Runtime surface facts and the SIMULATOR composition contract.
///
/// These tests compose the runtime IN PROCESS - no listener is started - so the
/// readiness answers, the deterministic initial revision, the evolution lifecycle
/// and the atomic refusal behaviour are all observable without launching the
/// host. The HTTP behaviour of the same surface is exercised Owner-local against
/// the built host (docs/STAGE_0.3A_OWNER_LOCAL_VALIDATION.md section 14).
///
/// Everything asserted here is a synthetic development value; nothing asserts a
/// process meaning, a device identity or a Production limit, and nothing
/// commands, dispatches or actuates anything.
/// </summary>
public sealed class RuntimeHostCompositionTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    // CA1861: constant sequences shared by the feed test are static readonly fields,
    // not inline array arguments (the arrays are only enumerated).
    private static readonly int[] ExpectedFeedRevisions = [4, 3, 2];
    private static readonly int[] ExpectedFeedPreviousRevisions = [3, 2, 1];

    /// <summary>Development-shaped options: SIMULATOR only, bounded, no real device identity.</summary>
    private static RuntimeHostOptions DefaultOptions() => new()
    {
        Profile = DeviceProfile.SIMULATOR,
        Port = 5181,
        SyntheticSeed = 20261007UL,
        TickIntervalMilliseconds = 150,
        StateHistoryCapacity = 32,
        DeltaHistoryCapacity = 32,
    };

    [Fact]
    public async Task Simulator_Composition_Publishes_Revision_One_And_The_Initial_Snapshot()
    {
        var runtime = SimulatorRuntime.Create(DefaultOptions(), new TestClock(Instant));

        try
        {
            Assert.True(runtime.IsInitialized);
            Assert.Equal(1, runtime.State.Revision);
            Assert.Equal(Instant, runtime.State.GeneratedAtUtc);
            Assert.Equal(Instant, runtime.ComposedAtUtc);
            Assert.Equal(1, runtime.Counters.CommittedRevisions);
            Assert.Equal(1, runtime.Counters.HistoryDepth);
            Assert.Equal(0, runtime.Deltas.Count);
            Assert.Null(runtime.Deltas.NewestRevision);
            Assert.Equal(0L, runtime.AcceptedTicks);
            Assert.Equal(0L, runtime.RejectedTransitions);

            var snapshot = runtime.ProjectSnapshot();
            Assert.Equal(SchemaIds.Snapshot, snapshot.Schema);
            Assert.Equal(RuntimeStage.Marker, snapshot.Runtime.StageMarker);
            Assert.Equal(1, snapshot.Revision);
            Assert.Equal(DeviceProfile.SIMULATOR, snapshot.DeviceProfile);
            Assert.Equal(CanonicalSensorMap.MatrixSlots, snapshot.WallMap.Count);
            Assert.Equal(CanonicalSensorMap.SensorLocations, snapshot.Sensors.Count);
            Assert.Equal(CanonicalSensorMap.ThermocoupleChannelCount,
                snapshot.Sensors
                    .SelectMany(sensor => new[] { sensor.TcFrontChannel, sensor.TcRearChannel })
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            Assert.Equal(4, snapshot.Walls.Count);
            Assert.Null(snapshot.ActiveJob);
            Assert.Equal(PumpRunState.STOPPED, snapshot.Pump.State);
            Assert.Equal(QueueSummary.MaxEntries, snapshot.Queue.Capacity);
            Assert.Equal(0, snapshot.Queue.TotalQueued);
        }
        finally
        {
            await runtime.DisposeAsync();
        }
    }

    [Fact]
    public async Task Same_Seed_And_Clock_Produce_The_Same_Initial_Snapshot()
    {
        // The clock is injected, so the composition instant is part of the input
        // and two runs with the same seed and instant are byte-identical.
        var first = SimulatorRuntime.Create(DefaultOptions(), new TestClock(Instant));
        var second = SimulatorRuntime.Create(DefaultOptions(), new TestClock(Instant));

        try
        {
            var firstJson = JsonSerializer.Serialize(first.ProjectSnapshot(), ContractJson.Options);
            var secondJson = JsonSerializer.Serialize(second.ProjectSnapshot(), ContractJson.Options);

            Assert.Equal(firstJson, secondJson);
        }
        finally
        {
            await first.DisposeAsync();
            await second.DisposeAsync();
        }
    }

    [Fact]
    public async Task Readiness_Requires_The_Evolution_Lifecycle()
    {
        var runtime = SimulatorRuntime.Create(DefaultOptions(), new TestClock(Instant));

        try
        {
            var beforeStart = runtime.Readiness();
            Assert.False(beforeStart.Ready);
            Assert.Equal(RuntimeReadinessCodes.EvolutionNotStarted, beforeStart.Code);

            runtime.Start();

            var afterStart = runtime.Readiness();
            Assert.True(afterStart.Ready, afterStart.Detail);
            Assert.Equal(RuntimeReadinessCodes.Ready, afterStart.Code);
            Assert.True(runtime.IsRunning);
        }
        finally
        {
            await runtime.DisposeAsync();
        }
    }

    [Fact]
    public async Task Evolution_Lifecycle_Advances_Revision_And_Shuts_Down_Cleanly()
    {
        var runtime = SimulatorRuntime.Create(DefaultOptions(), new SystemClock());

        try
        {
            runtime.Start();

            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (runtime.State.Revision < 3 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            var revision = runtime.State.Revision;
            Assert.True(revision >= 3, $"expected at least revision 3, saw {revision}");

            // One accepted tick committed exactly one revision: the store's
            // revision, its history depth and its commit counter agree.
            var counters = runtime.Counters;
            Assert.Equal(revision, counters.CommittedRevisions);
            Assert.Equal(revision, counters.HistoryDepth);
            Assert.Equal(0, counters.RefusedCommits);
            Assert.Equal(0L, runtime.RejectedTransitions);
            Assert.Equal(revision - 1, runtime.AcceptedTicks);

            // Every accepted transition produced exactly one Delta, newest first.
            Assert.Equal(revision, runtime.Deltas.NewestRevision);
            Assert.Equal(revision - 1, runtime.Deltas.Count);
            Assert.Equal(revision, runtime.Deltas.NewestFirst[0].Revision);
        }
        finally
        {
            await runtime.DisposeAsync();
        }

        // Shutdown cancelled the loop: no further revision is committed.
        Assert.False(runtime.IsRunning);
        var revisionAtShutdown = runtime.State.Revision;
        await Task.Delay(400);
        Assert.Equal(revisionAtShutdown, runtime.State.Revision);
    }

    [Fact]
    public async Task Startup_Fault_Keeps_Readiness_Not_Ready()
    {
        var runtime = SimulatorRuntime.CreateFaulted(
            DefaultOptions(),
            new TestClock(Instant),
            RuntimeHostFaultCodes.CompositionFault,
            "synthetic composition fault (test)");

        try
        {
            var readiness = runtime.Readiness();
            Assert.False(readiness.Ready);
            Assert.Equal(RuntimeReadinessCodes.StartupFault, readiness.Code);
            Assert.False(runtime.IsInitialized);
            Assert.False(runtime.IsRunning);
            Assert.Equal(RuntimeHostFaultCodes.CompositionFault, runtime.StartupFaultCode);
            Assert.Equal(RuntimeHostFaultCodes.CompositionFault, runtime.LastFaultCode);

            // Nothing is claimed that does not exist: no Snapshot, no evolution.
            Assert.Throws<InvalidOperationException>(() => runtime.ProjectSnapshot());
            Assert.Throws<InvalidOperationException>(() => runtime.Start());
            Assert.Equal(0, runtime.Deltas.Count);
        }
        finally
        {
            await runtime.DisposeAsync();
        }
    }

    [Fact]
    public void Configuration_Refuses_Foreign_Profiles_And_Malformed_Values()
    {
        // TEST_HARDWARE and PRODUCTION: refused by the policy, before composition.
        foreach (var profile in new[] { DeviceProfile.TEST_HARDWARE, DeviceProfile.PRODUCTION })
        {
            var refused = DefaultOptions() with { Profile = profile };
            Assert.False(refused.TryValidate(out var code, out var detail));
            Assert.Equal(ProfileStartPolicy.RefusalCode, code);
            Assert.Contains("SIMULATOR", detail, StringComparison.Ordinal);

            Assert.Throws<InvalidOperationException>(() => SimulatorRuntime.Create(refused, new TestClock(Instant)));
        }

        // Malformed synthetic configuration: refused with its own code, before any
        // host object exists.
        AssertRefuses(RuntimeHostOptions.SeedVariable, "not-a-number", RuntimeHostOptions.InvalidSyntheticSeed);
        AssertRefuses(RuntimeHostOptions.TickIntervalVariable, "5", RuntimeHostOptions.InvalidTickInterval);
        AssertRefuses(RuntimeHostOptions.TickIntervalVariable, "90000", RuntimeHostOptions.InvalidTickInterval);
        AssertRefuses(RuntimeHostOptions.StateHistoryVariable, "0", RuntimeHostOptions.InvalidStateHistoryCapacity);
        AssertRefuses(RuntimeHostOptions.DeltaHistoryVariable, "4096", RuntimeHostOptions.InvalidDeltaHistoryCapacity);

        // Safe development defaults are accepted.
        Assert.True(RuntimeHostOptions.TryLoadSynthetic(
            DeviceProfile.SIMULATOR, 5181, _ => null, out var loaded, out var defaultCode, out var defaultDetail), defaultDetail);
        Assert.Equal(string.Empty, defaultCode);
        Assert.Equal(RuntimeHostOptions.DefaultTickIntervalMilliseconds, loaded.TickIntervalMilliseconds);
        Assert.True(loaded.TryValidate(out _, out _));
    }

    [Fact]
    public async Task Delta_Feed_Reports_A_Contiguous_Newest_First_Run_As_Clean()
    {
        // A retained newest-first run must not be flagged as a gap: every entry
        // continues the entry walked before it, the newest entry has no newer
        // neighbour, and the oldest entry is not judged against history outside the
        // bounded window. (The projection previously compared an entry's own
        // previousRevision with the value carried from its newer neighbour, so every
        // row except the newest was reported as a gap.)
        var options = DefaultOptions();
        var runtime = SimulatorRuntime.Create(options, new TestClock(Instant));

        try
        {
            // Three accepted ticks through the real evolution + Delta projection,
            // no timers: one accepted tick commits exactly one revision.
            var history = new RuntimeDeltaHistory(capacity: 8);
            var state = runtime.State; // composed revision 1 at Instant

            for (var tick = 1; tick <= 3; tick++)
            {
                var outcome = RuntimeSyntheticEvolution.Tick(
                    state, options.SyntheticSeed, state.Revision, Instant.AddSeconds(tick));

                Assert.True(outcome.Accepted, outcome.RefusalReason);
                Assert.NotNull(outcome.Result);

                history.Append(RuntimeDeltaProjector.Project(state, outcome.Result!));
                state = outcome.Result!.State;
            }

            var feed = RuntimeDeltaFeed.From(history, state.Revision);

            Assert.Equal(3, feed.Count);
            Assert.True(feed.NewestFirst);
            Assert.Equal(4, feed.NewestDeltaRevision);
            Assert.Equal(state.Revision, feed.CurrentRevision);
            Assert.Equal(ExpectedFeedRevisions, feed.Items.Select(item => item.Revision));
            Assert.Equal(ExpectedFeedPreviousRevisions, feed.Items.Select(item => item.PreviousRevision));

            // Adjacent pairs continue the chain...
            for (var index = 0; index + 1 < feed.Items.Count; index++)
            {
                Assert.Equal(feed.Items[index].PreviousRevision, feed.Items[index + 1].Revision);
            }

            // ...and no row - newest, middle or oldest - is reported as a gap.
            Assert.All(feed.Items, item =>
            {
                Assert.False(item.HasRevisionGap);
                Assert.True(item.AppliesCleanly);
            });
        }
        finally
        {
            await runtime.DisposeAsync();
        }
    }

    [Fact]
    public void Read_Only_Route_Identities_Are_Pinned()
    {
        Assert.Equal("/health/live", ApiRoutes.HealthLive);
        Assert.Equal("/health/ready", ApiRoutes.HealthReady);
        Assert.Equal("/api/v1/snapshot", ApiRoutes.Snapshot);
        Assert.Equal("/api/v1/runtime", ApiRoutes.Runtime);
        Assert.Equal("/api/v1/deltas", ApiRoutes.Deltas);
        Assert.Equal("/inspector", ApiRoutes.Inspector);
    }

    private static void AssertRefuses(string variable, string value, string expectedCode)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal) { [variable] = value };

        var accepted = RuntimeHostOptions.TryLoadSynthetic(
            DeviceProfile.SIMULATOR,
            5181,
            key => variables.TryGetValue(key, out var found) ? found : null,
            out _,
            out var code,
            out _);

        Assert.False(accepted);
        Assert.Equal(expectedCode, code);
    }
}
