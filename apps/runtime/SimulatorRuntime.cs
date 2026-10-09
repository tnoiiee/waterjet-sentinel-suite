using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Wjss.Time;

namespace Wjss.Runtime;

/// <summary>
/// Machine readiness codes of the SIMULATOR runtime. A code is a stable identity
/// for the diagnostics surface, never prose.
/// </summary>
public static class RuntimeReadinessCodes
{
    /// <summary>Everything the readiness contract requires is true.</summary>
    public const string Ready = "RUNTIME_READY";

    /// <summary>The configuration object was not validated before composition.</summary>
    public const string ConfigurationNotValidated = "CONFIGURATION_NOT_VALIDATED";

    /// <summary>The active device profile is not SIMULATOR (ADR-0012; never silently substituted).</summary>
    public const string ProfileNotSimulator = "PROFILE_NOT_SIMULATOR";

    /// <summary>The Runtime State Store was never initialized.</summary>
    public const string StoreNotInitialized = "STORE_NOT_INITIALIZED";

    /// <summary>The initial Snapshot projection was never established.</summary>
    public const string InitialSnapshotUnavailable = "INITIAL_SNAPSHOT_UNAVAILABLE";

    /// <summary>The revision system never published a valid revision.</summary>
    public const string RevisionNotInitialized = "REVISION_NOT_INITIALIZED";

    /// <summary>The synthetic evolution lifecycle was not started.</summary>
    public const string EvolutionNotStarted = "EVOLUTION_NOT_STARTED";

    /// <summary>A startup fault was recorded; the runtime reports it instead of claiming readiness.</summary>
    public const string StartupFault = "STARTUP_FAULT";

    /// <summary>A fatal Runtime fault stopped the evolution lifecycle.</summary>
    public const string FatalRuntimeFault = "FATAL_RUNTIME_FAULT";
}

/// <summary>Machine fault codes recorded by the runtime host (diagnostics only, never prose).</summary>
public static class RuntimeHostFaultCodes
{
    /// <summary>An unexpected exception escaped one tick and was observed.</summary>
    public const string TickLoopException = "TICK_LOOP_EXCEPTION";

    /// <summary>The evolution loop stopped after too many consecutive tick failures, or faulted.</summary>
    public const string TickLoopStopped = "TICK_LOOP_STOPPED";

    /// <summary>The state store refused an accepted candidate revision.</summary>
    public const string CommitRefused = "TICK_COMMIT_REFUSED";

    /// <summary>A committed revision produced no Delta history entry (the feed reports the gap).</summary>
    public const string DeltaHistoryGap = "DELTA_HISTORY_GAP";

    /// <summary>Composition failed before the state store existed.</summary>
    public const string CompositionFault = "COMPOSITION_FAULT";
}

/// <summary>Readiness answer plus the machine code and detail that produced it.</summary>
public sealed record RuntimeReadiness
{
    public required bool Ready { get; init; }
    public required string Code { get; init; }
    public required string Detail { get; init; }

    /// <summary>The ready answer.</summary>
    public static RuntimeReadiness IsReady(string detail) =>
        new() { Ready = true, Code = RuntimeReadinessCodes.Ready, Detail = detail };

    /// <summary>The not-ready answer; the code never falls back to a generic value.</summary>
    public static RuntimeReadiness NotReady(string code, string detail) =>
        new() { Ready = false, Code = code, Detail = detail };
}

/// <summary>
/// The composed SIMULATOR runtime: deterministic initial state, the single
/// authoritative writer, bounded Delta history and the deterministic evolution
/// lifecycle.
///
/// Lifecycle contract
/// <list type="bullet">
///   <item>exactly one accepted tick commits exactly one revision, through the
///   store's single writer, from one non-overlapping loop;</item>
///   <item>a refused tick records a fault and a rejected-transition counter, emits
///   no Delta and advances no revision;</item>
///   <item>the deterministic values depend only on (seed, tick number, committed
///   state): the timer paces execution but is never an input to a value;</item>
///   <item>shutdown cancels the loop through <see cref="DisposeAsync"/> and the
///   loop is observed without rethrowing;</item>
///   <item>a startup fault and a fatal loop fault both keep readiness at
///   <c>503</c> with a structured reason code.</item>
/// </list>
///
/// Scope honesty: this type carries synthetic presentation values only. Kernel
/// dispatch is simulated in memory; there is no external control path, physical
/// device command, operator command, or persistence.
/// </summary>
public sealed class SimulatorRuntime : IAsyncDisposable
{
    /// <summary>Consecutive tick failures after which the loop stops instead of retrying forever.</summary>
    public const int MaximumConsecutiveTickFailures = 5;

    /// <summary>Development Dirty-Score threshold of the composed synthetic configuration.</summary>
    public const double DevelopmentDirtyThreshold = 50.5;

    /// <summary>Development stale threshold of the composed synthetic configuration, in milliseconds.</summary>
    public const int DevelopmentStaleThresholdMilliseconds = 5000;

    /// <summary>Label of the synthetic configuration block; it must never be mistaken for published Production configuration.</summary>
    public const string DevelopmentConfigurationLabel = "SYNTHETIC DEVELOPMENT CONFIGURATION - NOT A PRODUCTION VALUE";

    /// <summary>Revision of the composed synthetic configuration (the only one in Stage 0.3A).</summary>
    public const int DevelopmentConfigurationRevision = 1;

    private readonly RuntimeHostOptions _options;
    private readonly IClock _clock;
    private readonly RuntimePublicationStore? _store;
    private readonly RuntimePublicationWriter? _writer;
    private readonly SimulatorScenario? _scenario;
    private SequencingState _sequencing = SequencingKernel.Initial();
    private SequencingRetention _retention = SequencingRetention.Empty;
    private int _scenarioCursor;
    private readonly Action<string, string>? _faultObserver;
    private readonly CancellationTokenSource _loopCts = new();
    private readonly object _faultGate = new();

    private Task? _loopTask;
    private DateTimeOffset _startedAtUtc;
    private DateTimeOffset _nextTickInstant;
    private bool _started;
    private bool _loopCompleted;
    private long _acceptedTicks;
    private long _rejectedTransitions;
    private string? _lastFaultCode;
    private string? _lastFaultDetail;
    private readonly string? _startupFaultCode;
    private readonly string? _startupFaultDetail;
    private string? _fatalFaultCode;
    private string? _fatalFaultDetail;

    private SimulatorRuntime(
        RuntimeHostOptions options,
        IClock clock,
        RuntimePublicationStore? store,
        RuntimePublicationWriter? writer,
        SimulatorScenario? scenario,
        Action<string, string>? faultObserver,
        DateTimeOffset composedAtUtc,
        string? startupFaultCode,
        string? startupFaultDetail)
    {
        _options = options;
        _clock = clock;
        _store = store;
        _writer = writer;
        _scenario = scenario;
        _faultObserver = faultObserver;
        _startupFaultCode = startupFaultCode;
        _startupFaultDetail = startupFaultDetail;
        _startedAtUtc = composedAtUtc;
        _nextTickInstant = composedAtUtc + options.TickInterval;
    }

    /// <summary>Configured options (already validated).</summary>
    public RuntimeHostOptions Options => _options;

    /// <summary>The instant the initial revision was composed (the deterministic time origin of the tick sequence).</summary>
    public DateTimeOffset ComposedAtUtc { get; private init; }

    /// <summary>True when the state store exists: the runtime can project a Snapshot and evolve.</summary>
    public bool IsInitialized => _store is not null && _writer is not null;

    /// <summary>One detached reader generation; acquire once per response.</summary>
    public RuntimePublication Publication => _store?.Snapshot ?? throw new InvalidOperationException(
        $"[{RuntimeReadinessCodes.StoreNotInitialized}] No Runtime publication exists.");

    public RuntimeState State => Publication.Current;

    // Compatibility diagnostics are calculated from the publication; neither is a live store.
    public RuntimeStoreCounters Counters => CountersFor(Publication);
    public RuntimeDeltaHistory Deltas
    {
        get
        {
            if (_store is null) return new RuntimeDeltaHistory(_options.DeltaHistoryCapacity);
            var publication = Publication;
            var history = new RuntimeDeltaHistory(publication.HistoryCapacity);
            foreach (var delta in publication.Deltas.Reverse()) history.Append(delta);
            return history;
        }
    }

    public SimulatorScenarioId ScenarioId => _options.Scenario;
    public int ScenarioCursor => _scenarioCursor;
    public SequencingState Sequencing => _sequencing;
    public SequencingRetention Retention => _retention;

    public RuntimeStoreCounters CountersFor(RuntimePublication publication) => new()
    {
        CommittedRevisions = publication.Current.Revision,
        RefusedCommits = checked((int)RejectedTransitions),
        RevisionRefusals = 0,
        InvalidStateRefusals = 0,
        HistoryDepth = Math.Min(publication.Current.Revision, _options.StateHistoryCapacity),
        HistoryCapacity = _options.StateHistoryCapacity,
    };

    /// <summary>Accepted ticks since the lifecycle started.</summary>
    public long AcceptedTicks => Interlocked.Read(ref _acceptedTicks);

    /// <summary>Refused transitions since the lifecycle started (no revision was advanced by any of them).</summary>
    public long RejectedTransitions => Interlocked.Read(ref _rejectedTransitions);

    /// <summary>True while the evolution loop is running.</summary>
    public bool IsRunning => _started && !_loopCompleted;

    /// <summary>Instant the lifecycle was started (composition instant before <see cref="Start"/>).</summary>
    public DateTimeOffset StartedAtUtc
    {
        get
        {
            lock (_faultGate)
            {
                return _startedAtUtc;
            }
        }
    }

    /// <summary>Last recorded fault code, or null when no fault was observed.</summary>
    public string? LastFaultCode
    {
        get
        {
            lock (_faultGate)
            {
                return _lastFaultCode;
            }
        }
    }

    /// <summary>Startup fault code, or null when composition succeeded.</summary>
    public string? StartupFaultCode => _startupFaultCode;

    /// <summary>Detail of the last recorded fault, or null.</summary>
    public string? LastFaultDetail
    {
        get
        {
            lock (_faultGate)
            {
                return _lastFaultDetail;
            }
        }
    }

    /// <summary>Uptime of the lifecycle in seconds (0 before <see cref="Start"/>).</summary>
    public double UptimeSeconds
    {
        get
        {
            var started = StartedAtUtc;
            var elapsed = _clock.UtcNow - started;
            return elapsed > TimeSpan.Zero ? elapsed.TotalSeconds : 0.0;
        }
    }

    /// <summary>
    /// Composes the deterministic SIMULATOR runtime from validated options. The
    /// initial revision comes from the adapter's synthetic map and the Runtime
    /// composer, exactly as the Checkpoint A/B tests compose it; the initial
    /// Snapshot projection is established here, because "initial snapshot
    /// available" is a readiness fact and must not be claimed without evidence.
    /// </summary>
    public static SimulatorRuntime Create(
        RuntimeHostOptions options,
        IClock clock,
        Action<string, string>? faultObserver = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        if (!options.TryValidate(out var refusalCode, out var refusalDetail))
        {
            throw new InvalidOperationException($"[{refusalCode}] {refusalDetail}");
        }

        var composedAtUtc = clock.UtcNow;
        var config = new PublishedConfigurationRevision
        {
            Revision = DevelopmentConfigurationRevision,
            PublishedAt = UtcTimestamps.Format(composedAtUtc),
            Label = DevelopmentConfigurationLabel,
            DirtyThreshold = DevelopmentDirtyThreshold,
            StaleThresholdMs = DevelopmentStaleThresholdMilliseconds,
        };

        var seed = new SyntheticSeed(options.SyntheticSeed);
        var initialState = RuntimeStateComposer.ComposeInitial(
            options.Profile,
            config,
            SyntheticSensorMap.BuildWallMap(),
            SyntheticSensorMap.BuildInitialSensors(config, seed, composedAtUtc),
            WaterJetTopologyCatalog.WaterJets,
            WaterJetTopologyCatalog.IsolationValves,
            composedAtUtc);

        var scenarioSet = SimulatorScenarioCatalogue.Create(initialState.Sensors);
        var scenario = scenarioSet.Get(options.Scenario);
        var store = RuntimePublicationStore.Create(initialState, options.DeltaHistoryCapacity);

        // Establish the initial projection NOW: readiness later reports the fact
        // that was established, it never claims a projection that was never made.
        _ = RuntimeSnapshotProjector.Project(store.Snapshot.Current, new RuntimeStoreCounters
        { CommittedRevisions = 1, RefusedCommits = 0, RevisionRefusals = 0, InvalidStateRefusals = 0,
          HistoryDepth = 1, HistoryCapacity = options.StateHistoryCapacity }, 0.0);

        var runtime = new SimulatorRuntime(
            options,
            clock,
            store,
            store.CreateWriter(),
            scenario,
            faultObserver,
            composedAtUtc,
            startupFaultCode: null,
            startupFaultDetail: null)
        {
            ComposedAtUtc = composedAtUtc,
        };

        return runtime;
    }

    /// <summary>
    /// Builds a runtime that carries a startup fault instead of a state store. The
    /// host still starts so the structured <c>503</c> readiness answer is
    /// observable; nothing evolves and no Snapshot exists.
    /// </summary>
    public static SimulatorRuntime CreateFaulted(
        RuntimeHostOptions options,
        IClock clock,
        string faultCode,
        string faultDetail,
        Action<string, string>? faultObserver = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        var composedAtUtc = clock.UtcNow;
        var runtime = new SimulatorRuntime(
            options,
            clock,
            store: null,
            writer: null,
            scenario: null,
            faultObserver,
            composedAtUtc,
            faultCode,
            faultDetail)
        {
            ComposedAtUtc = composedAtUtc,
        };

        // The startup fault is also the last observed fault, so the diagnostics
        // surface never has to guess where a not-ready answer came from.
        runtime.RecordFault(faultCode, faultDetail);
        return runtime;
    }

    /// <summary>Current readiness answer with its machine code.</summary>
    public RuntimeReadiness Readiness(RuntimePublication? publication = null)
    {
        if (!_options.TryValidate(out var validationCode, out var validationDetail))
        {
            return RuntimeReadiness.NotReady(
                RuntimeReadinessCodes.ConfigurationNotValidated,
                $"The configuration is not valid: [{validationCode}] {validationDetail}");
        }

        if (_options.Profile != DeviceProfile.SIMULATOR)
        {
            return RuntimeReadiness.NotReady(
                RuntimeReadinessCodes.ProfileNotSimulator,
                $"The active device profile is {_options.Profile}; only SIMULATOR may report readiness (ADR-0012).");
        }

        if (_startupFaultCode is not null)
        {
            return RuntimeReadiness.NotReady(
                RuntimeReadinessCodes.StartupFault,
                $"Startup fault [{_startupFaultCode}]: {_startupFaultDetail}");
        }

        if (_loopTask?.IsFaulted == true || _fatalFaultCode is not null)
        {
            var fatal = _fatalFaultCode ?? RuntimeHostFaultCodes.TickLoopStopped;
            var fatalDetail = _fatalFaultDetail ?? "The evolution loop faulted.";
            return RuntimeReadiness.NotReady(
                RuntimeReadinessCodes.FatalRuntimeFault,
                $"Fatal runtime fault [{fatal}]: {fatalDetail}");
        }

        if (!IsInitialized)
        {
            return RuntimeReadiness.NotReady(
                RuntimeReadinessCodes.StoreNotInitialized,
                "The Runtime State Store was not initialized.");
        }

        publication ??= Publication;
        if (publication.Current.Revision < RuntimeStateComposer.InitialRevision)
        {
            return RuntimeReadiness.NotReady(
                RuntimeReadinessCodes.RevisionNotInitialized,
                $"The revision system reports revision {publication.Current.Revision}, which is not an initialized revision.");
        }

        if (!_started)
        {
            return RuntimeReadiness.NotReady(
                RuntimeReadinessCodes.EvolutionNotStarted,
                "The deterministic synthetic evolution lifecycle has not been started.");
        }

        // Fail closed rather than assume: readiness verifies the projection it is
        // about to claim instead of trusting that the composition-time projection
        // still matches the committed revision.
        var projection = RuntimeSnapshotProjector.Project(
            publication.Current, CountersFor(publication), UptimeSeconds);

        if (projection.Revision != publication.Current.Revision)
        {
            return RuntimeReadiness.NotReady(
                RuntimeReadinessCodes.InitialSnapshotUnavailable,
                $"The Snapshot projection reports revision {projection.Revision}, "
                + $"not the committed revision {publication.Current.Revision}.");
        }

        return RuntimeReadiness.IsReady(
            $"SIMULATOR runtime ready at revision {publication.Current.Revision} (tick interval {_options.TickIntervalMilliseconds} ms).");
    }

    /// <summary>Projects the current committed revision as the accepted Snapshot contract.</summary>
    public OperationalSnapshot ProjectSnapshot() => ProjectSnapshot(Publication);

    public OperationalSnapshot ProjectSnapshot(RuntimePublication publication) =>
        RuntimeSnapshotProjector.Project(publication.Current, CountersFor(publication), UptimeSeconds);

    /// <summary>
    /// Starts the deterministic evolution lifecycle. One non-overlapping loop owns
    /// every tick; the period only paces execution, so the presented values stay a
    /// pure function of (seed, tick number, committed state).
    /// </summary>
    public void Start()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException(
                $"[{RuntimeReadinessCodes.StoreNotInitialized}] The evolution lifecycle cannot start without an initialized state store.");
        }

        lock (_faultGate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _startedAtUtc = _clock.UtcNow;
            _nextTickInstant = _store!.Snapshot.Current.GeneratedAtUtc + _options.TickInterval;
        }

        _loopTask = Task.Run(() => RunLoopAsync(_loopCts.Token));
    }

    /// <summary>Cancels the lifecycle and observes the loop without rethrowing its outcome.</summary>
    public async ValueTask DisposeAsync()
    {
        _loopCts.Cancel();

        var loop = _loopTask;
        if (loop is not null)
        {
            // WhenAny observes completion (including a faulted or cancelled task)
            // without rethrowing; the outcome is recorded as a fault instead.
            await Task.WhenAny(loop).ConfigureAwait(false);

            if (loop.IsFaulted)
            {
                var failure = loop.Exception?.GetBaseException();
                RecordFatalFault(
                    RuntimeHostFaultCodes.TickLoopStopped,
                    failure is null
                        ? "The evolution loop faulted without an observable exception."
                        : $"{failure.GetType().Name}: {failure.Message}");
            }
        }

        _loopCompleted = true;
        _loopCts.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        var consecutiveFailures = 0;

        using var timer = new PeriodicTimer(_options.TickInterval);
        while (!token.IsCancellationRequested)
        {
            bool tickDue;
            try
            {
                tickDue = await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!tickDue)
            {
                break;
            }

            try
            {
                ExecuteTick();
                consecutiveFailures = 0;
            }
            catch (ArgumentException ex)
            {
                consecutiveFailures = ObserveTickFailure(consecutiveFailures, ex);
            }
            catch (InvalidOperationException ex)
            {
                consecutiveFailures = ObserveTickFailure(consecutiveFailures, ex);
            }

            if (consecutiveFailures >= MaximumConsecutiveTickFailures)
            {
                RecordFatalFault(
                    RuntimeHostFaultCodes.TickLoopStopped,
                    $"The evolution loop stopped after {consecutiveFailures} consecutive tick failures.");
                break;
            }
        }

        _loopCompleted = true;
    }

    private int ObserveTickFailure(int consecutiveFailures, Exception failure)
    {
        var failures = consecutiveFailures + 1;
        RecordFault(
            RuntimeHostFaultCodes.TickLoopException,
            $"{failure.GetType().Name}: {failure.Message}");
        return failures;
    }

    /// <summary>
    /// One accepted tick: derive the candidate, commit it through the single writer,
    /// project and retain its Delta. Refusals are recorded and change nothing.
    /// </summary>
    // First accepted ordinal is zero: catalogue tick 0 is published at Runtime revision 2.
    // Invoked only by the non-overlapping lifecycle (tests can invoke the same path).
    private void ExecuteTick()
    {
        var previousPublication = Publication;
        var previous = previousPublication.Current;
        var tickInstant = _nextTickInstant;
        var outcome = RuntimeSyntheticEvolution.Tick(
            previous, _options.SyntheticSeed, previous.Revision, tickInstant);

        if (!outcome.Accepted)
        {
            Interlocked.Increment(ref _rejectedTransitions);
            RecordFault(outcome.RefusalCode ?? RuntimeHostFaultCodes.CommitRefused,
                outcome.RefusalReason ?? "The synthetic tick was refused.");
            _nextTickInstant = tickInstant + _options.TickInterval;
            return;
        }

        try
        {
            var nextSequencing = _sequencing;
            var nextRetention = _retention;
            var due = _scenario is not null && _scenarioCursor < _scenario.Steps.Count
                && _scenario.Steps[_scenarioCursor].Tick <= _acceptedTicks;
            if (due)
            {
                var transition = SequencingKernel.Apply(_sequencing, _scenario!.Steps[_scenarioCursor].Event);
                nextSequencing = transition.State;
                nextRetention = SequencingRetention.Retain(_retention, _sequencing, transition);
            }

            var synthetic = outcome.Result!.State;
            var candidate = synthetic with
            {
                Sensors = SequencingRuntimeProjection.ProjectSensorQueueStates(nextSequencing, synthetic.Sensors),
                Queue = SequencingRuntimeProjection.ProjectQueue(nextSequencing, nextRetention),
                ActiveJob = SequencingRuntimeProjection.ProjectActiveJob(nextSequencing, nextRetention),
                Sequence = SequencingRuntimeProjection.ProjectSequence(nextSequencing, nextRetention, synthetic.Sequence.Controls),
            };
            RuntimeStateInvariants.RequireValid(candidate);
            var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);
            var published = _writer!.Publish(previous.Revision, candidate, delta);
            if (!published.Accepted)
            {
                Interlocked.Increment(ref _rejectedTransitions);
                RecordFault(published.Code ?? RuntimeHostFaultCodes.CommitRefused, "Runtime publication refused.");
                _nextTickInstant = tickInstant + _options.TickInterval;
                return;
            }

            // Adoption is strictly after the atomic State/Delta publication.
            _sequencing = nextSequencing;
            _retention = nextRetention;
            if (due) _scenarioCursor++;
            Interlocked.Increment(ref _acceptedTicks);
            _nextTickInstant = published.Publication.Current.GeneratedAtUtc + _options.TickInterval;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            Interlocked.Increment(ref _rejectedTransitions);
            _nextTickInstant = tickInstant + _options.TickInterval;
            throw;
        }
    }

    private void RecordFault(string code, string detail)
    {
        lock (_faultGate)
        {
            _lastFaultCode = code;
            _lastFaultDetail = detail;
        }

        _faultObserver?.Invoke(code, detail);
    }

    private void RecordFatalFault(string code, string detail)
    {
        lock (_faultGate)
        {
            _fatalFaultCode = code;
            _fatalFaultDetail = detail;
        }

        RecordFault(code, detail);
    }
}
