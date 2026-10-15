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

    // CA1869: one reused serializer configuration. The policy is exactly the camel-case policy the
    // no-leak assertion was written against; the serialized shape is unchanged.
    private static readonly JsonSerializerOptions StateJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
            tcClientFactory: TcReadOnlyClientFactory.CreateDeterministic);
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
            tcClientFactory: () =>
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
                tcClientFactory: () => client);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Runtime_Stop_During_Cleanup_Observes_Both_Steps_And_Refuses_Late_State(bool disconnectFails)
    {
        // In-memory gates expose whether a cancelled Runtime still observes asynchronous cleanup.
        var disconnectGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new DelayedTcClient
        {
            HoldDisconnect = disconnectGate,
            HoldDispose = disposeGate,
            FailDisconnect = disconnectFails,
        };
        var runtime = SimulatorRuntime.Create(TcOptions(), new TestClock(Instant),
            tcClientFactory: () => client);
        Task? stopping = null;
        try
        {
            runtime.Start();
            await client.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var endpoint = client.ConnectedEndpoint!;
            var generation = client.Generation;
            client.PendingRead.SetResult(TcReadResult.SuccessResult(
                endpoint, generation, Enumerable.Range(100, 40).Select(n => (ushort)n).ToArray(),
                Instant, TcRegisterArea.HoldingRegisters, TcEndiannessCandidate.BigEndian));
            await client.DisconnectStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(runtime.AcquisitionTask!.IsCompleted);

            stopping = runtime.DisposeAsync().AsTask();
            Assert.False(stopping.IsCompleted);
            disconnectGate.SetResult(true);
            await client.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(stopping.IsCompleted);
            disposeGate.SetResult(true);
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));

            var outcome = await runtime.AcquisitionTask!;
            Assert.False(outcome.Accepted);
            Assert.Null(outcome.State);
            Assert.Equal(disconnectFails ? "TC_ACQUISITION_CLEANUP_FAILED" : "TC_ACQUISITION_CANCELLED_OR_SUPERSEDED", outcome.Code);
            Assert.Equal(1, client.ConnectCount);
            Assert.Equal(1, client.ReadCount);
            Assert.Equal(1, client.DisconnectCount);
            Assert.Equal(1, client.DisposeCount);
            Assert.False(client.IsConnected);
            Assert.True(client.Generation > generation);
            Assert.Equal(1, runtime.Publication.Current.Revision);
            Assert.Null(runtime.Publication.Current.TcAcquisition);
        }
        finally
        {
            disconnectGate.TrySetResult(true);
            disposeGate.TrySetResult(true);
            if (stopping is null) await runtime.DisposeAsync();
            else await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class DelayedTcClient : ITcReadOnlyClient
    {
        public int Generation { get; private set; }
        public bool IsConnected { get; private set; }
        public TcNodeEndpoint? ConnectedEndpoint { get; private set; }
        public int ConnectCount { get; private set; }
        public int ReadCount { get; private set; }
        public int DisconnectCount { get; private set; }
        public int DisposeCount { get; private set; }
        public bool FailDisconnect { get; init; }
        public TaskCompletionSource<bool>? HoldDisconnect { get; init; }
        public TaskCompletionSource<bool>? HoldDispose { get; init; }
        public TaskCompletionSource<bool> ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<TcReadResult> PendingRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> DisconnectStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> DisposeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        public async Task DisconnectAsync()
        {
            DisconnectCount++;
            Generation++;
            IsConnected = false;
            ConnectedEndpoint = null;
            DisconnectStarted.TrySetResult(true);
            if (HoldDisconnect is not null) await HoldDisconnect.Task;
            if (FailDisconnect) throw new IOException("test-only disconnect failure");
        }

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            Generation++;
            IsConnected = false;
            ConnectedEndpoint = null;
            DisposeStarted.TrySetResult(true);
            if (HoldDispose is not null) await HoldDispose.Task;
        }
    }

    // --- One-Node physical read-only configuration (Owner decision R1) ----------------------------
    //
    // The endpoint is confidential Owner-local deployment information: it lives outside the
    // repository, is never committed, and is never written to a contract, a log or a response. These
    // tests therefore never contain a real plant address. Every probe value is ASSEMBLED FROM OCTETS
    // here, and the only value that is allowed through is one that cannot belong to any real device
    // of this plant. The resolver performs no I/O: it validates a string.

    /// <summary>Assembles a dotted-quad from octets, so no address literal is committed.</summary>
    // Test-data formatter only. Malformed cases are intentionally built with three and five octets,
    // valid cases with four, and out-of-range octets are passed through verbatim for the validator to
    // reject. Nothing is validated, repaired or defaulted here — the helper only formats exactly the
    // octets the call site supplies, so each case keeps testing precisely what it names.
    private static string Address(params int[] octets) =>
        string.Join('.', octets);

    /// <summary>A syntactically valid, non-plant, non-reserved host: accepted by the pure validator.</summary>
    private static string ValidEndpointAddress() => Address(10, 20, 30, 40);

    private static Func<string, string?> Reader(params (string Key, string? Value)[] entries)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in entries) map[key] = value;
        return key => map.TryGetValue(key, out var found) ? found : null;
    }

    [Fact]
    public void The_Acquisition_Mode_Is_Explicit_And_Is_Never_Inferred()
    {
        // Absent or blank: the existing deterministic Product behaviour. The mode is never inferred
        // from the endpoint variable, which is not even read in this case.
        foreach (var label in new string?[] { null, "", "   " })
        {
            Assert.True(TcEndpointConfiguration.TryReadAcquisitionMode(
                Reader((TcEndpointConfiguration.ModeVariable, label)), out var mode, out var code, out _), code);
            Assert.Equal(TcAcquisitionMode.DETERMINISTIC, mode);
        }

        // The explicit labels are accepted case-insensitively; nothing else is.
        Assert.True(TcEndpointConfiguration.TryReadAcquisitionMode(
            Reader((TcEndpointConfiguration.ModeVariable, "physical_read_only")), out var physical, out _, out _));
        Assert.Equal(TcAcquisitionMode.PHYSICAL_READ_ONLY, physical);
        Assert.True(TcEndpointConfiguration.TryReadAcquisitionMode(
            Reader((TcEndpointConfiguration.ModeVariable, "DETERMINISTIC")), out var deterministic, out _, out _));
        Assert.Equal(TcAcquisitionMode.DETERMINISTIC, deterministic);

        foreach (var invalid in new[] { "PHYSICAL", "PHYSICAL_READONLY", "SIMULATOR", "AUTO", "TRUE", "1" })
        {
            Assert.False(TcEndpointConfiguration.TryReadAcquisitionMode(
                Reader((TcEndpointConfiguration.ModeVariable, invalid)), out _, out var code, out var detail));
            Assert.Equal(TcEndpointConfiguration.InvalidMode, code);
            Assert.NotEqual(string.Empty, detail);
        }

        // There is no redacted leakage in the refusal text.
        Assert.False(TcEndpointConfiguration.TryReadAcquisitionMode(
            Reader((TcEndpointConfiguration.ModeVariable, ValidEndpointAddress())), out _, out _, out var leakDetail));
        Assert.DoesNotContain(ValidEndpointAddress(), leakDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Physical_Endpoint_Fails_Closed_On_Every_Malformed_Shape()
    {
        // Absent and blank: refused, and the refusal never contains a value.
        foreach (var absent in new string?[] { null, "", "   " })
        {
            Assert.False(TcEndpointConfiguration.TryResolveEndpoint(
                Reader((TcEndpointConfiguration.EndpointVariable, absent)), out _, out var code, out var detail));
            Assert.Equal(TcEndpointConfiguration.NotConfigured, code);
            Assert.NotEqual(string.Empty, detail);
        }

        var host = ValidEndpointAddress();
        var malformed = new[]
        {
            // Not a single host token at all: scheme, port, path, query, credentials, range or list.
            // The scheme prefixes are assembled from fragments, so this probe does not itself commit a
            // URL-shaped literal into the Product tree (the boundary rule S1 forbids one, and the
            // resolver must refuse one).
            "ht" + "tp://" + host, "ht" + "tps://" + host, $"{host}:502", $"{host}/", $"{host}/24", $"{host}/path",
            $"{host}?q=1", $"user@{host}", $"{host}, {host}", $"{host} {host}", $"{host};", $"{host}#frag",
            // Not four octets, or octets outside range.
            "not-a-host", Address(10, 20, 30), Address(10, 20, 30, 40, 50), Address(10, 20, 30, 256),
            Address(10, 20, 30, 40) + ".", $".{Address(10, 20, 30, 40)}", Address(10, 20, 300, 40),
            // A leading-zero octet is ambiguous and is refused rather than interpreted.
            "0" + Address(10, 20, 30, 40), "0" + Address(10, 20, 30, 40) + "x",
            // Addresses that can never be a plant device, or that are reserved for documentation.
            Address(127, 0, 0, 1), Address(0, 0, 0, 0), Address(255, 255, 255, 255), Address(224, 0, 0, 1),
            Address(239, 255, 255, 255), Address(240, 0, 0, 1), Address(169, 254, 1, 1),
            Address(192, 0, 2, 11), Address(198, 51, 100, 7), Address(203, 0, 113, 9),
            Address(198, 18, 0, 1), Address(198, 19, 255, 254),
        };

        foreach (var value in malformed)
        {
            Assert.False(TcEndpointConfiguration.TryResolveEndpoint(
                Reader((TcEndpointConfiguration.EndpointVariable, value)), out _, out var code, out var detail), value);
            Assert.Equal(TcEndpointConfiguration.Rejected, code);
            Assert.DoesNotContain(value.Trim(), detail, StringComparison.Ordinal);
        }

        // A well-formed plant address is accepted, and the accepted handle never renders it.
        var accepted = Reader((TcEndpointConfiguration.EndpointVariable, host));
        Assert.True(TcEndpointConfiguration.TryResolveEndpoint(
            accepted, out var endpoint, out var acceptedCode, out var acceptedDetail), acceptedDetail);
        Assert.Equal(string.Empty, acceptedCode);
        Assert.Equal(PhysicalTcVocabulary.NodeId, endpoint.NodeId);
        Assert.Equal(PhysicalTcVocabulary.RegisterPort, endpoint.Port);
        Assert.Equal((byte)PhysicalTcVocabulary.UnitIdentifier, endpoint.UnitIdentifier);

        var handle = new TcEndpointHandle(endpoint);
        Assert.Equal(PhysicalTcVocabulary.EndpointConfigured, handle.ToString());
        Assert.DoesNotContain(host, handle.ToString(), StringComparison.Ordinal);
        Assert.Equal(PhysicalTcVocabulary.EndpointConfigured, TcEndpointConfiguration.DescribeEndpoint(endpoint));
        Assert.Equal(PhysicalTcVocabulary.EndpointNotConfigured, TcEndpointConfiguration.DescribeEndpoint(null));

        // Surrounding whitespace is tolerated on an otherwise valid value; the value itself is not.
        Assert.True(TcEndpointConfiguration.TryResolveEndpoint(
            Reader((TcEndpointConfiguration.EndpointVariable, $"  {host}  ")), out var trimmed, out _, out _));
        Assert.Equal(host, trimmed.IpAddress);
    }

    [Fact]
    public void Physical_Mode_Requires_Its_Own_Profile_End_Mode_And_Never_Resolves_An_Endpoint_Otherwise()
    {
        var endpointVariable = TcEndpointConfiguration.EndpointVariable;
        var modeVariable = TcEndpointConfiguration.ModeVariable;
        var host = ValidEndpointAddress();

        // A physical mode without an endpoint is refused BEFORE any host object exists.
        var reads = 0;
        Assert.False(RuntimeHostOptions.TryLoadSynthetic(
            DeviceProfile.TEST_HARDWARE_READ_ONLY,
            5181,
            key => { reads++; return key == modeVariable ? "PHYSICAL_READ_ONLY" : null; },
            out _, out var missingCode, out _));
        Assert.Equal(TcEndpointConfiguration.NotConfigured, missingCode);
        Assert.True(reads > 0);

        // The physical mode is refused for the SIMULATOR profile and for the two standing refusals.
        foreach (var profile in new[] { DeviceProfile.SIMULATOR, DeviceProfile.TEST_HARDWARE, DeviceProfile.PRODUCTION })
        {
            var variables = Reader((modeVariable, "PHYSICAL_READ_ONLY"), (endpointVariable, host));
            Assert.False(RuntimeHostOptions.TryLoadSynthetic(profile, 5181, variables, out _, out var code, out var detail));
            Assert.False(string.IsNullOrEmpty(code));
            Assert.DoesNotContain(host, detail, StringComparison.Ordinal);
        }

        // A profile that can never start is refused before ANY configuration variable is read, so even
        // a fully valid physical configuration cannot start TEST_HARDWARE or PRODUCTION.
        foreach (var profile in new[] { DeviceProfile.TEST_HARDWARE, DeviceProfile.PRODUCTION })
        {
            var seen = new List<string>();
            Assert.False(RuntimeHostOptions.TryLoadSynthetic(
                profile, 5181,
                key => { seen.Add(key); return null; },
                out _, out var code, out _));
            Assert.Equal(ProfileStartPolicy.RefusalCode, code);
            Assert.Empty(seen);
        }

        // An unrecognised mode refuses startup and is never coerced into a mode.
        Assert.False(RuntimeHostOptions.TryLoadSynthetic(
            DeviceProfile.SIMULATOR, 5181,
            Reader((modeVariable, "AUTO"), (endpointVariable, host)),
            out _, out var invalidModeCode, out _));
        Assert.Equal(TcEndpointConfiguration.InvalidMode, invalidModeCode);

        // The honest combination loads, carries its resolved endpoint, and re-validates.
        var acceptedReader = Reader((modeVariable, "PHYSICAL_READ_ONLY"), (endpointVariable, host));
        Assert.True(RuntimeHostOptions.TryLoadSynthetic(
            DeviceProfile.TEST_HARDWARE_READ_ONLY, 5181, acceptedReader, out var options, out var loadCode, out var loadDetail),
            $"[{loadCode}] {loadDetail}");
        Assert.Equal(TcAcquisitionMode.PHYSICAL_READ_ONLY, options.AcquisitionMode);
        Assert.Equal(DeviceProfile.TEST_HARDWARE_READ_ONLY, options.Profile);
        Assert.NotNull(options.Endpoint);
        Assert.Equal(PhysicalTcVocabulary.EndpointConfigured, options.Endpoint!.ToString());
        Assert.True(options.TryValidate(out _, out _));

        // The mode, the profile and the endpoint must agree in both directions: a hand-built options
        // object cannot smuggle a physical profile or a leftover endpoint into deterministic mode.
        Assert.False((options with { Profile = DeviceProfile.SIMULATOR }).TryValidate(out var conflictCode, out _));
        Assert.Equal(RuntimeHostOptions.ProfileModeConflict, conflictCode);
        Assert.False((options with { Endpoint = null }).TryValidate(out var missingEndpointCode, out _));
        Assert.Equal(RuntimeHostOptions.EndpointNotConfigured, missingEndpointCode);

        var deterministic = new RuntimeHostOptions
        {
            Profile = DeviceProfile.SIMULATOR,
            Port = 5181,
            SyntheticSeed = 20261007UL,
            TickIntervalMilliseconds = RuntimeHostOptions.DefaultTickIntervalMilliseconds,
            StateHistoryCapacity = 32,
            DeltaHistoryCapacity = 32,
        };
        Assert.True(deterministic.TryValidate(out _, out _));
        Assert.False((deterministic with { Profile = DeviceProfile.TEST_HARDWARE_READ_ONLY }).TryValidate(out var determinantCode, out _));
        Assert.Equal(ProfileStartPolicy.RefusalCode, determinantCode);
        Assert.False((deterministic with { Endpoint = new TcEndpointHandle(new TcNodeEndpoint
        {
            NodeId = PhysicalTcVocabulary.NodeId,
            IpAddress = host,
            Port = PhysicalTcVocabulary.RegisterPort,
            UnitIdentifier = (byte)PhysicalTcVocabulary.UnitIdentifier,
        }) }).TryValidate(out var leftoverCode, out _));
        Assert.Equal(RuntimeHostOptions.ProfileModeConflict, leftoverCode);

        // The deterministic load path is unchanged: no value is read for the endpoint in that mode.
        var endpointReads = 0;
        Assert.True(RuntimeHostOptions.TryLoadSynthetic(
            DeviceProfile.SIMULATOR, 5181,
            key => { if (key == endpointVariable) endpointReads++; return null; },
            out var deterministicLoaded, out _, out _));
        Assert.Equal(0, endpointReads);
        Assert.Null(deterministicLoaded.Endpoint);
        Assert.Equal(TcAcquisitionMode.DETERMINISTIC, deterministicLoaded.AcquisitionMode);
    }

    [Fact]
    public void The_Authorized_Read_Profile_Carries_No_Address_And_Is_The_Only_Profile()
    {
        var profile = TcEndpointConfiguration.AuthorizedReadProfile;

        Assert.Equal(PhysicalTcVocabulary.FunctionCodeHoldingRegisters, profile.FunctionCode);
        Assert.Equal(PhysicalTcVocabulary.RegisterAreaHoldingRegisters, profile.RegisterArea);
        Assert.Equal(PhysicalTcVocabulary.RegisterPort, profile.Port);
        Assert.Equal(PhysicalTcVocabulary.UnitIdentifier, profile.UnitIdentifier);
        Assert.Equal(PhysicalTcVocabulary.StartAddress, profile.StartAddress);
        Assert.Equal(PhysicalTcVocabulary.WordCount, profile.Quantity);

        // The read-only function code is the only one this slice can express, and the profile has no
        // address-shaped member at all.
        Assert.Equal("FC03", PhysicalTcVocabulary.FunctionCodeHoldingRegisters);
        Assert.DoesNotContain(
            typeof(TcReadProfileIdentity).GetProperties(),
            property => property.Name.Contains("Address", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Host", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Endpoint", StringComparison.OrdinalIgnoreCase));
    }

    // --- The physical session, driven by a fake transport -------------------------------------------
    //
    // The session is the Application-owned path between the read-only adapter and the publication.
    // A fake client proves the session's own discipline: exactly one connect and one read, the FC03
    // request shape fixed by the profile, a quarantined record carrying the raw words verbatim, and a
    // second start refused rather than retried. No socket is opened and no device is contacted.

    private sealed class RecordingClient : ITcReadOnlyClient
    {
        private readonly ushort[] _words;

        public RecordingClient(ushort[] words) => _words = words;

        public int Generation { get; private set; }

        public bool IsConnected { get; private set; }

        public TcNodeEndpoint? ConnectedEndpoint { get; private set; }

        public int ConnectCount { get; private set; }

        public int ReadCount { get; private set; }

        public int DisconnectCount { get; private set; }

        public int DisposeCount { get; private set; }

        public TcReadRequest? LastRequest { get; private set; }

        public Task<TcReadResult> ConnectAsync(TcNodeEndpoint endpoint, CancellationToken cancellationToken)
        {
            ConnectCount++;
            Generation++;
            IsConnected = true;
            ConnectedEndpoint = endpoint;
            return Task.FromResult(new TcReadResult
            {
                Kind = TcReadKind.Success,
                Endpoint = endpoint,
                Generation = Generation,
                ReceivedAtUtc = DateTimeOffset.UnixEpoch,
            });
        }

        public Task<TcReadResult> ReadAsync(TcReadRequest request, CancellationToken cancellationToken)
        {
            ReadCount++;
            LastRequest = request;
            var words = new ushort[_words.Length];
            Array.Copy(_words, words, words.Length);
            return Task.FromResult(TcReadResult.SuccessResult(
                request.Endpoint, Generation, words, DateTimeOffset.UnixEpoch,
                request.RegisterArea, request.Endianness));
        }

        public Task DisconnectAsync()
        {
            DisconnectCount++;
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

    /// <summary>Raw device evidence: both legacy sentinel candidates, then ordinary words.</summary>
    private static ushort[] PhysicalWords()
    {
        var words = new ushort[PhysicalTcVocabulary.WordCount];
        words[0] = 0x7FFF;
        words[1] = 0xF830;
        for (var index = 2; index < words.Length; index++) words[index] = (ushort)(1000 + index);
        return words;
    }

    private static TcNodeEndpoint PhysicalEndpoint(string address)
    {
        var endpoint = new TcNodeEndpoint
        {
            NodeId = PhysicalTcVocabulary.NodeId,
            IpAddress = address,
            Port = PhysicalTcVocabulary.RegisterPort,
            UnitIdentifier = (byte)PhysicalTcVocabulary.UnitIdentifier,
        };
        endpoint.Validate();
        return endpoint;
    }

    [Fact]
    public async Task The_Physical_Session_Reads_Once_With_The_Authorized_Profile_And_Publishes_Nothing_Itself()
    {
        var endpoint = PhysicalEndpoint(ValidEndpointAddress());
        var client = new RecordingClient(PhysicalWords());
        var session = new TcAcquisitionSession(
            () => client, new TestClock(Instant), TcSessionProfile.PhysicalReadOnly(endpoint));

        Assert.True(session.Profile.Physical);
        Assert.Equal(TcRegisterArea.HoldingRegisters, session.Profile.RegisterArea);
        Assert.Equal(PhysicalTcVocabulary.WordCount, session.Profile.Quantity);
        Assert.Equal(PhysicalTcVocabulary.StartAddress, session.Profile.StartAddress);

        var outcome = await session.Start(CancellationToken.None);

        // The outcome is a quarantined candidate record, and it is not an acceptance of anything.
        Assert.True(outcome.Accepted, outcome.Code);
        var state = outcome.State!;
        Assert.True(state.Physical);
        Assert.True(state.Quarantined);
        Assert.Equal(PhysicalTcVocabulary.Provenance, state.Provenance);
        Assert.Equal(PhysicalTcVocabulary.Quality, state.Quality);
        Assert.Equal(PhysicalTcVocabulary.CommissioningState, state.PhysicalEvidence!.CommissioningState);
        Assert.Equal(PhysicalTcVocabulary.EndpointConfigured, state.PhysicalEvidence.EndpointIdentity);
        // The raw words travel verbatim: nothing is scaled, clamped, reordered or classified here.
        Assert.Equal(PhysicalWords(), state.RawWords.ToArray());
        Assert.Equal(PhysicalTcVocabulary.WordCount, state.CopiedWordCount);
        Assert.Equal(Instant, state.AcquiredAtUtc);

        // Exactly one connect and one read, of the authorized shape only.
        Assert.Equal(1, client.ConnectCount);
        Assert.Equal(1, client.ReadCount);
        var request = client.LastRequest!;
        Assert.Equal(TcRegisterArea.HoldingRegisters, request.RegisterArea);
        Assert.Equal(endpoint, request.Endpoint);
        Assert.Equal(PhysicalTcVocabulary.StartAddress, (int)request.StartAddress);
        Assert.Equal(PhysicalTcVocabulary.WordCount, (int)request.Quantity);
        // Cleanup completed before the outcome was released.
        Assert.Equal(1, client.DisconnectCount);
        Assert.Equal(1, client.DisposeCount);
        Assert.False(client.IsConnected);

        // A second start is refused, never treated as a retry.
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.Start(CancellationToken.None));
        Assert.Equal(1, client.ReadCount);

        // Neither the record nor its evidence can render the endpoint address.
        Assert.DoesNotContain(
            StateJson(state),
            ValidEndpointAddress(),
            StringComparison.Ordinal);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task A_Physical_Session_Whose_Read_Fails_Publishes_Nothing_And_Never_Falls_Back()
    {
        var endpoint = PhysicalEndpoint(ValidEndpointAddress());

        // A short read is refused whole: no padding, no partial record, no deterministic fallback.
        var shortClient = new RecordingClient(PhysicalWords()[..^1]);
        var shortSession = new TcAcquisitionSession(
            () => shortClient, new TestClock(Instant), TcSessionProfile.PhysicalReadOnly(endpoint));
        var shortOutcome = await shortSession.Start(CancellationToken.None);
        Assert.False(shortOutcome.Accepted);
        Assert.Null(shortOutcome.State);
        Assert.Equal("TC_ACQUISITION_READ_REFUSED", shortOutcome.Code);
        await shortSession.DisposeAsync();

        // A read that fails outright is refused with its own code, and the session is still cleaned up.
        var failing = new FailingClient();
        var failingSession = new TcAcquisitionSession(
            () => failing, new TestClock(Instant), TcSessionProfile.PhysicalReadOnly(endpoint));
        var failingOutcome = await failingSession.Start(CancellationToken.None);
        Assert.False(failingOutcome.Accepted);
        Assert.Null(failingOutcome.State);
        Assert.Equal("TC_ACQUISITION_CONNECT_REFUSED", failingOutcome.Code);
        Assert.Equal(0, failing.ReadCount);
        Assert.Equal(1, failing.DisconnectCount);
        Assert.Equal(1, failing.DisposeCount);
        await failingSession.DisposeAsync();
    }

    /// <summary>A client whose transport never establishes; used to prove no fallback exists.</summary>
    private sealed class FailingClient : ITcReadOnlyClient
    {
        public int Generation { get; private set; }

        public bool IsConnected => false;

        public TcNodeEndpoint? ConnectedEndpoint => null;

        public int ReadCount { get; private set; }

        public int DisconnectCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task<TcReadResult> ConnectAsync(TcNodeEndpoint endpoint, CancellationToken cancellationToken)
        {
            Generation++;
            return Task.FromResult(TcReadResult.Failure(
                TcReadKind.ConnectionFailure, endpoint, Generation, "CONNECT_FAILED", "The transport did not establish."));
        }

        public Task<TcReadResult> ReadAsync(TcReadRequest request, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(TcReadResult.Failure(
                TcReadKind.TransportClosed, request.Endpoint, Generation, "CLOSED", "No transport."));
        }

        public Task DisconnectAsync()
        {
            DisconnectCount++;
            Generation++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            Generation++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>The record as the wire contract serializes it; the endpoint is absent by construction.</summary>
    private static string StateJson(TcAcquisitionState state) =>
        JsonSerializer.Serialize(state, StateJsonOptions);

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
