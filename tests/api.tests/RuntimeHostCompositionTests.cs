using System.Reflection;
using System.Text.Json;
using Wjss.Adapters.TcAcquisition;
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

    // A long timer prevents a real background tick while these tests invoke the very same
    // non-overlapping writer path synchronously. No listener, socket or physical client is used.
    private static RuntimeHostOptions TcOptions() => DefaultOptions() with { TickIntervalMilliseconds = 60000 };

    private static void Tick(SimulatorRuntime runtime) =>
        typeof(SimulatorRuntime).GetMethod("ExecuteTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(runtime, null);

    [Fact]
    public async Task One_Use_Session_Publishes_One_Atomic_Tc_State_And_Matching_Snapshot_Delta_Feed()
    {
        var runtime = SimulatorRuntime.Create(TcOptions(), new TestClock(Instant),
            deterministicTcClientFactory: TcReadOnlyClientFactory.CreateDeterministic);
        try
        {
            Assert.Null(runtime.Publication.Current.TcAcquisition);
            Assert.Null(runtime.ProjectSnapshot().TcAcquisition);
            runtime.Start();
            var outcome = await runtime.AcquisitionTask!;
            Assert.True(outcome.Accepted, outcome.Code);
            Assert.Equal(RuntimeReadinessCodes.TcAcquisitionNotPublished, runtime.Readiness().Code);
            Assert.Equal(1, runtime.Publication.Current.Revision); // session cannot publish outside the tick writer

            Tick(runtime);
            var publication = runtime.Publication;
            var snapshot = runtime.ProjectSnapshot(publication);
            var feed = RuntimeDeltaFeed.From(publication);
            Assert.Equal(2, publication.Current.Revision);
            Assert.Equal(1, publication.Generation);
            Assert.Equal(snapshot.Revision, publication.Deltas[0].Revision);
            Assert.Equal(snapshot.Revision, feed.CurrentRevision);
            Assert.Equal(snapshot.Revision, feed.NewestDeltaRevision);
            Assert.Equal(1, feed.Count);
            Assert.Contains("tcAcquisition", feed.Items[0].ChangedSections);
            Assert.Equal("TC-NODE-01", snapshot.TcAcquisition!.NodeId);
            Assert.Equal("CAB1", snapshot.TcAcquisition.CabinetId);
            Assert.Equal("DETERMINISTIC_SIMULATION", snapshot.TcAcquisition.Provenance);
            Assert.False(snapshot.TcAcquisition.Physical);
            Assert.True(snapshot.TcAcquisition.Quarantined);
            Assert.Equal(Enumerable.Range(100, 40).Select(n => (ushort)n), snapshot.TcAcquisition.RawWords);
            Assert.Equal(snapshot.TcAcquisition.RawWords.ToArray(), feed.Items[0].TcAcquisition!.RawWords.ToArray());
            Assert.Equal(RuntimeReadinessCodes.Ready, runtime.Readiness(publication).Code);

            using (var snapshotJson = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, ContractJson.Options)))
                Assert.Equal(40, snapshotJson.RootElement.GetProperty("tcAcquisition")
                    .GetProperty("rawWords").GetArrayLength());
            using (var feedJson = JsonDocument.Parse(JsonSerializer.Serialize(feed, ContractJson.Options)))
                Assert.Equal(40, feedJson.RootElement.GetProperty("items")[0]
                    .GetProperty("tcAcquisition").GetProperty("rawWords").GetArrayLength());

            Tick(runtime);
            var next = runtime.Publication;
            Assert.Equal(3, next.Current.Revision);
            Assert.Null(next.Deltas[0].TcAcquisition);
            Assert.Equal(40, next.Current.TcAcquisition!.RawWords.Count);
            Assert.DoesNotContain("tcAcquisition", RuntimeDeltaFeed.From(next).Items[0].ChangedSections);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task Failed_One_Read_Stays_Null_And_Reports_Unavailable_Without_A_Tc_Delta()
    {
        var creations = 0;
        var runtime = SimulatorRuntime.Create(TcOptions(), new TestClock(Instant),
            deterministicTcClientFactory: () =>
            {
                creations++;
                throw new InvalidOperationException("test-only factory failure");
            });
        try
        {
            runtime.Start();
            var outcome = await runtime.AcquisitionTask!;
            Assert.False(outcome.Accepted);
            Assert.Equal("TC_ACQUISITION_FAILED", outcome.Code);
            Assert.Equal(1, creations);
            Tick(runtime);
            Assert.Equal(2, runtime.Publication.Current.Revision);
            Assert.Null(runtime.ProjectSnapshot().TcAcquisition);
            Assert.Null(runtime.Publication.Deltas[0].TcAcquisition);
            Assert.DoesNotContain("tcAcquisition", RuntimeDeltaFeed.From(runtime.Publication).Items[0].ChangedSections);
            Assert.Equal(RuntimeReadinessCodes.TcAcquisitionUnavailable, runtime.Readiness().Code);
            Assert.Equal(RuntimeHostFaultCodes.TcAcquisitionFailed, runtime.LastFaultCode);
        }
        finally { await runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task Stale_Result_And_Shutdown_Cannot_Adopt_A_Late_Tc_Read()
    {
        // In-memory delayed clients only; never use the physical factory or a local responder.
        foreach (var shutdown in new[] { false, true })
        {
            var client = new DelayedTcClient();
            var runtime = SimulatorRuntime.Create(TcOptions(), new TestClock(Instant),
                deterministicTcClientFactory: () => client);
            var disposed = false;
            try
            {
                runtime.Start();
                await client.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var endpoint = client.ConnectedEndpoint!;
                var originalGeneration = client.Generation;
                if (shutdown)
                {
                    await runtime.DisposeAsync();
                    disposed = true;
                }
                else
                {
                    await client.DisconnectAsync(); // invalidates the outstanding generation
                }
                client.PendingRead.SetResult(TcReadResult.SuccessResult(
                    endpoint, originalGeneration, Enumerable.Range(100, 40).Select(n => (ushort)n).ToArray(),
                    Instant, TcRegisterArea.HoldingRegisters, TcEndiannessCandidate.BigEndian));
                var outcome = await runtime.AcquisitionTask!.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(outcome.Accepted);
                Assert.Null(outcome.State);
                if (!shutdown)
                {
                    Assert.Equal("TC_ACQUISITION_STALE_GENERATION", outcome.Code);
                    Tick(runtime);
                    Assert.Equal(RuntimeReadinessCodes.TcAcquisitionUnavailable, runtime.Readiness().Code);
                    Assert.Null(runtime.Publication.Current.TcAcquisition);
                }
                else
                {
                    Assert.Equal(1, runtime.Publication.Current.Revision);
                    Assert.Null(runtime.Publication.Current.TcAcquisition);
                }
                Assert.Equal(1, client.ConnectCount);
                Assert.Equal(1, client.ReadCount);
                Assert.Equal(1, client.DisposeCount);
            }
            finally { if (!disposed) await runtime.DisposeAsync(); }
        }
    }

    private sealed class DelayedTcClient : ITcReadOnlyClient
    {
        public int Generation { get; private set; }
        public bool IsConnected { get; private set; }
        public TcNodeEndpoint? ConnectedEndpoint { get; private set; }
        public int ConnectCount { get; private set; }
        public int ReadCount { get; private set; }
        public int DisposeCount { get; private set; }
        public TaskCompletionSource<bool> ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<TcReadResult> PendingRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TcReadResult> ConnectAsync(TcNodeEndpoint endpoint, CancellationToken cancellationToken)
        {
            ConnectCount++;
            Generation++;
            IsConnected = true;
            ConnectedEndpoint = endpoint;
            return Task.FromResult(new TcReadResult
            {
                Kind = TcReadKind.Success, Endpoint = endpoint, Generation = Generation,
            });
        }

        public Task<TcReadResult> ReadAsync(TcReadRequest request, CancellationToken cancellationToken)
        {
            ReadCount++;
            ReadStarted.TrySetResult(true);
            return PendingRead.Task;
        }

        public Task DisconnectAsync()
        {
            Generation++;
            IsConnected = false;
            ConnectedEndpoint = null;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            Generation++;
            IsConnected = false;
            ConnectedEndpoint = null;
            return ValueTask.CompletedTask;
        }
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
