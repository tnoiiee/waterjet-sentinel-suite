using System.Diagnostics;
using Wjss.Adapters.TcAcquisition;

namespace Wjss.TcBench;

/// <summary>
/// The outer lifecycle budget for one bench invocation. The approved values are the Stage 0.4C-K6
/// decision: 15 s outer lifecycle, 5 s cleanup-observation threshold, 5 s worker-observation
/// threshold.
///
/// The budget triggers cancellation, epoch invalidation and rejection. It never abandons or detaches
/// the synchronous Connect worker, and no operation is left unobserved: an overdue cleanup or an
/// overdue worker observation is RECORDED and blocks acceptance instead of being hidden. For that
/// reason this budget is an operational threshold, not a hard process-termination ceiling.
/// </summary>
public sealed record PhysicalTrialBudgets
{
    public static PhysicalTrialBudgets Approved { get; } = new()
    {
        LifecycleBudget = TimeSpan.FromSeconds(15),
        CleanupObservationBudget = TimeSpan.FromSeconds(5),
        WorkerObservationBudget = TimeSpan.FromSeconds(5),
    };

    public required TimeSpan LifecycleBudget { get; init; }
    public required TimeSpan CleanupObservationBudget { get; init; }
    public required TimeSpan WorkerObservationBudget { get; init; }

    internal void RequireValid()
    {
        foreach (var budget in new[] { LifecycleBudget, CleanupObservationBudget, WorkerObservationBudget })
        {
            if (budget <= TimeSpan.Zero || budget > TimeSpan.FromMinutes(5)) throw new ArgumentException("INVALID_LIFECYCLE_BUDGET");
        }
    }
}

/// <summary>Single invocation owner. No retry, reconnect, polling or detached operation.</summary>
public sealed class TrialRunner
{
    private readonly object _handoffGate = new();
    private long _epoch = 1;
    private bool _stopped;
    private bool _invoked;
    private Action? _cancelOperation;
    private string _stopFailureType = "NOT_PROVIDED";

    /// <summary>
    /// Operational lifecycle budgets. Production uses the approved defaults; only the focused
    /// software tests narrow a budget to exercise an overdue path deterministically.
    /// </summary>
    public PhysicalTrialBudgets Budgets { get; init; } = PhysicalTrialBudgets.Approved;

    public TrialEvidence DryRun(TrialCatalog catalog, string trialId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        lock (_handoffGate)
        {
            RequireOneInvocation();
            return ApplyBuildFields(TrialEvidence.Create(catalog, catalog.Select(trialId).Trial, "SIMULATION", _epoch), BuildIdentity.LoadTrusted());
        }
    }

    public void Stop()
    {
        lock (_handoffGate)
        {
            if (_stopped) return;
            _stopped = true;
            _epoch++;
            try { _cancelOperation?.Invoke(); }
            catch (Exception ex) { _stopFailureType = ex.GetType().Name; }
        }
    }

    /// <summary>
    /// The production physical entry point. Accepts only a plan produced by the fail-closed
    /// authorization gate, so no client can be constructed before the signature, identity, tuple,
    /// validity and single-use consumption gates have all passed.
    /// </summary>
    public async Task<TrialEvidence> RunPhysicalTrialAsync(PhysicalTrialPlan plan, Func<ITcReadOnlyClient> factory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(factory);
        var execution = new TrialExecution
        {
            Catalog = plan.Catalog,
            Trial = plan.Trial,
            Endpoint = plan.Endpoint,
            Provenance = "PHYSICAL_DIAGNOSTIC",
            Factory = factory,
            ReadTimeout = TimeSpan.FromMilliseconds(2000),
            BuildIdentity = plan.BuildIdentity,
            AuthorizationId = plan.AuthorizationId,
            AuthorizationReference = plan.AuthorizationReference,
            AuthorizationPayloadSha256 = plan.AuthorizationPayloadSha256,
            AuthorizationKeyId = plan.AuthorizationKeyId,
            AuthorizationVerification = PhysicalTrialStates.Verified,
            AuthorizationConsumption = PhysicalTrialStates.Consumed,
        };
        return await ExecuteAsync(execution, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The Local TCP Simulation entry point: one connection attempt and at most one read against
    /// the Owner-local loopback simulator, through the same lifecycle as the physical path. The
    /// invocation configuration is validated fail-closed by <see cref="LocalSimulation.RequireValid"/>
    /// before any client is constructed. No authorization is read, no physical gate is consulted and
    /// no plan exists: the evidence is classified <see cref="LocalSimulation.Provenance"/>, records
    /// the effective loopback endpoint, and stays QUARANTINED. It is never T01 or hardware evidence.
    /// </summary>
    public async Task<TrialEvidence> RunLocalSimulationAsync(TrialCatalog catalog, TrialConfiguration invocation, Func<ITcReadOnlyClient> factory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(factory);
        var validated = LocalSimulation.RequireValid(catalog, invocation);
        var execution = new TrialExecution
        {
            Catalog = catalog,
            Trial = validated,
            Endpoint = validated.Endpoint,
            Provenance = LocalSimulation.Provenance,
            Factory = factory,
            ReadTimeout = TimeSpan.FromMilliseconds(2000),
            BuildIdentity = BuildIdentity.LoadTrusted(),
        };
        return await ExecuteAsync(execution, cancellationToken).ConfigureAwait(false);
    }

    // Deliberately internal: software validation cannot override the executable's trusted tuple.
    // Even this seam validates loopback before constructing a client, irrespective of factory kind.
    internal async Task<TrialEvidence> RunSoftwareTrialAsync(
        TrialCatalog catalog, TcNodeEndpoint loopback, Func<ITcReadOnlyClient> factory,
        TimeSpan readTimeout, Func<Task>? beforeHandoff,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(loopback);
        ArgumentNullException.ThrowIfNull(factory);
        if (loopback.IpAddress != "127.0.0.1" || loopback.Port is < 1 or > 65535 || loopback.Port == 502
            || readTimeout <= TimeSpan.Zero || readTimeout > TimeSpan.FromSeconds(2)) throw new ArgumentException("SOFTWARE_LOOPBACK_ONLY");
        var execution = new TrialExecution
        {
            Catalog = catalog,
            Trial = catalog.Select("T01").Trial,
            Endpoint = loopback,
            Provenance = "SYNTHETIC_TEST_ONLY",
            Factory = factory,
            ReadTimeout = readTimeout,
            BuildIdentity = BuildIdentity.LoadTrusted(),
            BeforeHandoff = beforeHandoff,
        };
        return await ExecuteAsync(execution, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TrialEvidence> ExecuteAsync(TrialExecution execution, CancellationToken cancellationToken)
    {
        var budgets = Budgets;
        ArgumentNullException.ThrowIfNull(budgets);
        budgets.RequireValid();
        var trial = execution.Trial;
        long epoch;
        lock (_handoffGate) { RequireOneInvocation(); epoch = _epoch; }
        var lifecycleWatch = Stopwatch.StartNew();
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // The outer lifecycle budget cancels the operation; it never detaches the synchronous handshake.
        operationCts.CancelAfter(budgets.LifecycleBudget);
        lock (_handoffGate) { _cancelOperation = operationCts.Cancel; if (_stopped) operationCts.Cancel(); }
        using var stopRegistration = cancellationToken.Register(Stop);
        var evidence = CreateEvidence(execution, epoch);
        if (evidence.Provenance == "SYNTHETIC_TEST_ONLY")
        {
            evidence = evidence with { Endpoint = execution.Endpoint.IpAddress, Port = execution.Endpoint.Port };
        }
        if (cancellationToken.IsCancellationRequested || Stopped(epoch))
        {
            lock (_handoffGate)
            {
                _cancelOperation = null;
                return evidence with
                {
                    CancellationState = cancellationToken.IsCancellationRequested ? "REQUESTED" : "STOP_REQUESTED",
                    StopState = _stopped ? "REQUESTED" : "NOT_REQUESTED", LifecycleFailureType = _stopFailureType,
                    FinalHandoffDecision = "REJECTED_OR_NOT_ISSUED", AcquisitionEpochAfter = _epoch,
                };
            }
        }
        ITcReadOnlyClient? client = null;
        TcReadResult? connect = null;
        TcReadResult? read = null;
        var disconnect = "NOT_ISSUED";
        var disposal = "NOT_ISSUED";
        var tasks = "NOT_ISSUED";
        var cleanupWithinBudget = true;
        var workerWithinBudget = true;
        var clock = new Stopwatch();
        var transportBeforeCleanup = "NOT_OBSERVED";
        int? generationBeforeCleanup = null;
        int? generationAfterCleanup = null;
        var transportAfter = "NOT_OBSERVED";
        var generationMatched = false;
        try
        {
            client = execution.Factory();
            evidence = evidence with { TransportBefore = client.IsConnected ? "CONNECTED" : "DISCONNECTED", GenerationBefore = client.Generation };
            var connectStart = DateTimeOffset.UtcNow;
            clock.Start();
            evidence = evidence with { Connect = new() { State = "INCOMPLETE", StartedAt = connectStart } };
            tasks = "COMPLETE";
            // Task.Run only isolates the synchronous handshake. It is always awaited and never
            // supplied a scheduling cancellation token: even late connect must be observed.
            var workerWatch = Stopwatch.StartNew();
            connect = await Task.Run(() => client.ConnectAsync(execution.Endpoint, operationCts.Token), CancellationToken.None).ConfigureAwait(false);
            workerWatch.Stop();
            workerWithinBudget = workerWatch.Elapsed <= budgets.WorkerObservationBudget;
            evidence = evidence with { Connect = AdapterOutcome.From(connect, connectStart, clock.Elapsed.TotalMilliseconds) };
            if (connect.Kind == TcReadKind.Success && !Stopped(epoch) && !cancellationToken.IsCancellationRequested
                && client.Generation == connect.Generation && client.ConnectedEndpoint == execution.Endpoint)
            {
                var request = new TcReadRequest
                {
                    Endpoint = execution.Endpoint, StartAddress = trial.Start, Quantity = trial.Quantity,
                    RegisterArea = (TcRegisterArea)trial.FunctionCode, Generation = client.Generation,
                    Endianness = TcEndiannessCandidate.BigEndian, Timeout = execution.ReadTimeout,
                };
                var readStart = DateTimeOffset.UtcNow;
                clock.Restart();
                evidence = evidence with { Read = new() { State = "INCOMPLETE", StartedAt = readStart } };
                read = await client.ReadAsync(request, operationCts.Token).ConfigureAwait(false);
                evidence = evidence with { Read = AdapterOutcome.From(read, readStart, clock.Elapsed.TotalMilliseconds) };
                generationMatched = read.Generation == request.Generation && client.Generation == request.Generation;
            }
        }
        catch (Exception ex)
        {
            // Operation is observed; raw details stay out of public evidence.
            evidence = evidence with { LifecycleFailureType = ex.GetType().Name };
            var attempted = evidence.Read.State == "INCOMPLETE" ? evidence.Read : evidence.Connect;
            if (attempted.State == "INCOMPLETE")
            {
                var thrown = attempted with { State = "THREW", CompletedAt = DateTimeOffset.UtcNow,
                    ElapsedMilliseconds = clock.Elapsed.TotalMilliseconds, ExceptionType = ex.GetType().Name, DetailCode = "OPERATION_THROWN" };
                evidence = evidence.Read.State == "INCOMPLETE" ? evidence with { Read = thrown } : evidence with { Connect = thrown };
            }
        }
        finally
        {
            if (client is not null)
            {
                try
                {
                    transportBeforeCleanup = client.IsConnected ? "CONNECTED" : "DISCONNECTED";
                    generationBeforeCleanup = client.Generation;
                }
                catch (Exception ex) { evidence = evidence with { LifecycleFailureType = ex.GetType().Name }; }
                try
                {
                    // Cleanup has no cancellation token of its own: it is always observed to terminal
                    // completion, and only the elapsed time decides whether it is overdue.
                    cleanupWithinBudget &= await CompleteWithinBudgetAsync(client.DisconnectAsync(), budgets.CleanupObservationBudget).ConfigureAwait(false);
                    disconnect = "COMPLETE";
                }
                catch (Exception ex) { disconnect = "INCOMPLETE"; evidence = evidence with { LifecycleFailureType = ex.GetType().Name }; }
                try
                {
                    cleanupWithinBudget &= await CompleteWithinBudgetAsync(client.DisposeAsync().AsTask(), budgets.CleanupObservationBudget).ConfigureAwait(false);
                    disposal = "COMPLETE";
                }
                catch (Exception ex) { disposal = "INCOMPLETE"; evidence = evidence with { LifecycleFailureType = ex.GetType().Name }; }
                try
                {
                    transportAfter = client.IsConnected ? "CONNECTED" : "DISCONNECTED";
                    generationAfterCleanup = client.Generation;
                    if (transportAfter != "DISCONNECTED") disposal = "INCOMPLETE";
                }
                catch (Exception ex) { disposal = "INCOMPLETE"; evidence = evidence with { LifecycleFailureType = ex.GetType().Name }; }
            }
        }
        // Deterministic validation seam for a stop between cleanup and final evidence handoff.
        if (execution.BeforeHandoff is not null)
        {
            try { await execution.BeforeHandoff().ConfigureAwait(false); }
            catch (Exception ex) { Stop(); evidence = evidence with { LifecycleFailureType = ex.GetType().Name }; }
        }
        lifecycleWatch.Stop();
        var lifecycleElapsed = lifecycleWatch.Elapsed > budgets.LifecycleBudget;
        var cleanupState = !cleanupWithinBudget || lifecycleElapsed
            ? PhysicalTrialStates.Overdue
            : disconnect == "COMPLETE" && disposal == "COMPLETE" && transportAfter == "DISCONNECTED"
                ? PhysicalTrialStates.Observed
                : PhysicalTrialStates.Unproven;
        lock (_handoffGate)
        {
            _cancelOperation = null;
            var retained = !_stopped && epoch == _epoch && !cancellationToken.IsCancellationRequested
                && generationMatched && read?.Kind == TcReadKind.Success && read.RawWords?.Length == trial.Quantity
                && read.Generation == generationBeforeCleanup && disconnect == "COMPLETE" && disposal == "COMPLETE"
                && evidence.LifecycleFailureType == "NOT_PROVIDED"
                && !lifecycleElapsed && cleanupState == PhysicalTrialStates.Observed && workerWithinBudget;
            return evidence with
            {
                LifecycleFailureType = _stopFailureType != "NOT_PROVIDED" ? _stopFailureType : evidence.LifecycleFailureType,
                AcquisitionEpochAfter = _epoch,
                GenerationBeforeCleanup = generationBeforeCleanup,
                GenerationAfterCleanup = generationAfterCleanup,
                TransportBeforeCleanup = transportBeforeCleanup,
                TransportAfter = transportAfter,
                CancellationState = cancellationToken.IsCancellationRequested ? "REQUESTED" : _stopped ? "STOP_REQUESTED" : "NOT_REQUESTED",
                StopState = _stopped ? "REQUESTED" : "NOT_REQUESTED",
                TimeoutState = connect?.Kind == TcReadKind.Timeout || read?.Kind == TcReadKind.Timeout ? "ELAPSED" : connect?.Kind == TcReadKind.Success && read?.Kind == TcReadKind.Success ? "NOT_ELAPSED" : "NOT_OBSERVED",
                LifecycleBudgetState = lifecycleElapsed ? PhysicalTrialStates.Elapsed : PhysicalTrialStates.NotElapsed,
                CleanupState = cleanupState,
                WorkerObservationState = workerWithinBudget ? PhysicalTrialStates.Observed : PhysicalTrialStates.Overdue,
                StaleDecision = generationMatched && !_stopped && epoch == _epoch ? "CURRENT_AT_DIAGNOSTIC_HANDOFF" : "REJECTED_OR_NOT_ISSUED",
                FinalHandoffDecision = retained ? "RETAINED_QUARANTINED" : "REJECTED_OR_NOT_ISSUED",
                DisconnectResult = disconnect, DisposalResult = disposal, TaskCompletion = tasks,
                Words = retained ? read!.RawWords!.Select((word, index) => new IndexedWord(index, word)).ToArray() : [],
                CopiedWordCount = read?.RawWords?.Length,
                // Only offline Owner review can change acceptance. This runner never accepts.
                TrialAcceptance = TrialAcceptance.QUARANTINED,
            };
        }
    }

    // The budget decides whether a cleanup step is overdue, but the step itself is always awaited to
    // terminal completion: nothing is detached and no abandoned operation can publish later.
    private static async Task<bool> CompleteWithinBudgetAsync(Task task, TimeSpan budget)
    {
        var winner = await Task.WhenAny(task, Task.Delay(budget)).ConfigureAwait(false);
        var withinBudget = winner == task;
        await task.ConfigureAwait(false);
        return withinBudget;
    }

    private static TrialEvidence ApplyBuildFields(TrialEvidence evidence, BuildIdentity buildIdentity) => evidence with
    {
        ApprovedBaseSha = buildIdentity.ApprovedBaseSha,
        ImplementationSha = buildIdentity.ImplementationSha,
        SourceRef = buildIdentity.SourceRef,
        DeploymentBuildIdentity = buildIdentity.Digest,
        PhysicalCapabilityEnabled = buildIdentity.PhysicalCapabilityEnabled,
        CleanStateProof = buildIdentity.PhysicalCapabilityEnabled ? buildIdentity.DeploymentProof : "NOT_PROVIDED",
    };

    private static TrialEvidence CreateEvidence(TrialExecution execution, long epoch) =>
        ApplyBuildFields(TrialEvidence.Create(execution.Catalog, execution.Trial, execution.Provenance, epoch), execution.BuildIdentity) with
        {
            AuthorizationId = execution.AuthorizationId,
            AuthorizationReference = execution.AuthorizationReference,
            AuthorizationPayloadSha256 = execution.AuthorizationPayloadSha256,
            AuthorizationKeyId = execution.AuthorizationKeyId,
            AuthorizationVerification = execution.AuthorizationVerification,
            AuthorizationConsumption = execution.AuthorizationConsumption,
        };

    private bool Stopped(long epoch) { lock (_handoffGate) return _stopped || epoch != _epoch; }

    private void RequireOneInvocation()
    {
        if (_invoked || _stopped) throw new InvalidOperationException("ONE_INVOCATION_ONLY");
        _invoked = true;
    }

    private sealed record TrialExecution
    {
        public required TrialCatalog Catalog { get; init; }
        public required TrialConfiguration Trial { get; init; }
        public required TcNodeEndpoint Endpoint { get; init; }
        public required string Provenance { get; init; }
        public required Func<ITcReadOnlyClient> Factory { get; init; }
        public required TimeSpan ReadTimeout { get; init; }
        public required BuildIdentity BuildIdentity { get; init; }
        public string AuthorizationId { get; init; } = PhysicalTrialStates.NotProvided;
        public string AuthorizationReference { get; init; } = PhysicalTrialStates.NotProvided;
        public string AuthorizationPayloadSha256 { get; init; } = PhysicalTrialStates.NotProvided;
        public string AuthorizationKeyId { get; init; } = PhysicalTrialStates.NotProvided;
        public string AuthorizationVerification { get; init; } = PhysicalTrialStates.NotAttempted;
        public string AuthorizationConsumption { get; init; } = PhysicalTrialStates.NotAttempted;
        public Func<Task>? BeforeHandoff { get; init; }
    }
}
