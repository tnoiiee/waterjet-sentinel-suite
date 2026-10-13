using System.Diagnostics;
using Wjss.Adapters.TcAcquisition;

namespace Wjss.TcBench;

/// <summary>Single invocation owner. No timer, retry, reconnect, or detached operation.</summary>
public sealed class TrialRunner
{
    private readonly object _handoffGate = new();
    private long _epoch = 1;
    private bool _stopped;
    private bool _invoked;
    private Action? _cancelOperation;
    private string _stopFailureType = "NOT_PROVIDED";

    public TrialEvidence DryRun(TrialCatalog catalog, string trialId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        lock (_handoffGate)
        {
            RequireOneInvocation();
            return TrialEvidence.Create(catalog, catalog.Select(trialId).Trial, "SIMULATION", _epoch);
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
        var trial = catalog.Select("T01").Trial;
        long epoch;
        lock (_handoffGate) { RequireOneInvocation(); epoch = _epoch; }
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_handoffGate) { _cancelOperation = operationCts.Cancel; if (_stopped) operationCts.Cancel(); }
        using var stopRegistration = cancellationToken.Register(Stop);
        var evidence = TrialEvidence.Create(catalog, trial, "SYNTHETIC_TEST_ONLY", epoch) with { Endpoint = loopback.IpAddress, Port = loopback.Port };
        if (cancellationToken.IsCancellationRequested || Stopped(epoch))
        {
            lock (_handoffGate)
            {
                _cancelOperation = null;
                return evidence with { CancellationState = cancellationToken.IsCancellationRequested ? "REQUESTED" : "STOP_REQUESTED",
                    StopState = _stopped ? "REQUESTED" : "NOT_REQUESTED", LifecycleFailureType = _stopFailureType,
                    FinalHandoffDecision = "REJECTED", AcquisitionEpochAfter = _epoch };
            }
        }
        ITcReadOnlyClient? client = null;
        TcReadResult? connect = null;
        TcReadResult? read = null;
        var disconnect = "NOT_ISSUED";
        var disposal = "NOT_ISSUED";
        var tasks = "NOT_ISSUED";
        var clock = new Stopwatch();
        var transportBeforeCleanup = "NOT_OBSERVED";
        int? generationBeforeCleanup = null;
        int? generationAfterCleanup = null;
        var transportAfter = "NOT_OBSERVED";
        var generationMatched = false;
        try
        {
            client = factory();
            evidence = evidence with { TransportBefore = client.IsConnected ? "CONNECTED" : "DISCONNECTED", GenerationBefore = client.Generation };
            var connectStart = DateTimeOffset.UtcNow;
            clock.Start();
            evidence = evidence with { Connect = new() { State = "INCOMPLETE", StartedAt = connectStart } };
            tasks = "COMPLETE";
            // Task.Run only isolates the synchronous handshake. It is always awaited and never
            // supplied a scheduling cancellation token: even late connect must be observed.
            connect = await Task.Run(() => client.ConnectAsync(loopback, operationCts.Token), CancellationToken.None).ConfigureAwait(false);
            evidence = evidence with { Connect = AdapterOutcome.From(connect, connectStart, clock.Elapsed.TotalMilliseconds) };
            if (connect.Kind == TcReadKind.Success && !Stopped(epoch) && !cancellationToken.IsCancellationRequested
                && client.Generation == connect.Generation && client.ConnectedEndpoint == loopback)
            {
                var request = new TcReadRequest
                {
                    Endpoint = loopback, StartAddress = trial.Start, Quantity = trial.Quantity,
                    RegisterArea = (TcRegisterArea)trial.FunctionCode, Generation = client.Generation,
                    Endianness = TcEndiannessCandidate.BigEndian, Timeout = readTimeout,
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
                try { await client.DisconnectAsync().ConfigureAwait(false); disconnect = "COMPLETE"; }
                catch (Exception ex) { disconnect = "INCOMPLETE"; evidence = evidence with { LifecycleFailureType = ex.GetType().Name }; }
                try { await client.DisposeAsync().ConfigureAwait(false); disposal = "COMPLETE"; }
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
        if (beforeHandoff is not null)
        {
            try { await beforeHandoff().ConfigureAwait(false); }
            catch (Exception ex) { Stop(); evidence = evidence with { LifecycleFailureType = ex.GetType().Name }; }
        }
        lock (_handoffGate)
        {
            _cancelOperation = null;
            var retained = !_stopped && epoch == _epoch && !cancellationToken.IsCancellationRequested
                && generationMatched && read?.Kind == TcReadKind.Success && read.RawWords?.Length == trial.Quantity
                && read.Generation == generationBeforeCleanup && disconnect == "COMPLETE" && disposal == "COMPLETE"
                && evidence.LifecycleFailureType == "NOT_PROVIDED";
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

    private bool Stopped(long epoch) { lock (_handoffGate) return _stopped || epoch != _epoch; }
    private void RequireOneInvocation()
    {
        if (_invoked || _stopped) throw new InvalidOperationException("ONE_INVOCATION_ONLY");
        _invoked = true;
    }
}
