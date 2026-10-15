using Wjss.Adapters.TcAcquisition;
using Wjss.Contracts;
using Wjss.Time;

namespace Wjss.Runtime;

/// <summary>One bounded, Application-owned acquisition outcome. Failures have no publishable TC state.</summary>
public sealed record TcAcquisitionOutcome
{
    public required bool Accepted { get; init; }
    public TcAcquisitionState? State { get; init; }
    public required string Code { get; init; }

    public static TcAcquisitionOutcome Success(TcAcquisitionState state) =>
        new() { Accepted = true, State = state, Code = "TC_ACQUISITION_COMPLETE" };

    public static TcAcquisitionOutcome Refused(string code) =>
        new() { Accepted = false, Code = code };
}

/// <summary>
/// One-use Product session. It owns its read-only client, cancellation, generation/epoch fencing,
/// copying and cleanup. It never writes Runtime state itself: the host's existing non-overlapping
/// tick consumes a successful outcome and publishes it with the matching Delta through its sole
/// RuntimePublicationStore writer. No UI callback, device connection, retry or polling exists here.
/// </summary>
public sealed class TcAcquisitionSession : IAsyncDisposable
{
    // The existing read request default is 2000 ms. Connect + read share this attempt
    // budget; each independent best-effort cleanup operation is also time-bounded.
    private static readonly TimeSpan AttemptLimit = TimeSpan.FromMilliseconds(2000);

    private readonly Func<ITcReadOnlyClient> _clientFactory;
    private readonly IClock _clock;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private Task<TcAcquisitionOutcome>? _task;
    private bool _disposed;
    private int _epoch;

    public TcAcquisitionSession(Func<ITcReadOnlyClient> clientFactory, IClock clock)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Starts exactly once. A second call is refused, never treated as a retry.</summary>
    public Task<TcAcquisitionOutcome> Start(CancellationToken runtimeToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_task is not null) throw new InvalidOperationException("The TC session has already started.");
            var epoch = Interlocked.Increment(ref _epoch);
            _task = RunOnceAsync(epoch, runtimeToken);
            return _task;
        }
    }

    private async Task<TcAcquisitionOutcome> RunOnceAsync(int epoch, CancellationToken runtimeToken)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(runtimeToken, _stop.Token);
        bound.CancelAfter(AttemptLimit);
        ITcReadOnlyClient? client = null;
        TcAcquisitionOutcome outcome;
        try
        {
            client = _clientFactory();
            outcome = await AcquireAsync(client, bound.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            outcome = TcAcquisitionOutcome.Refused(runtimeToken.IsCancellationRequested || _stop.IsCancellationRequested
                ? "TC_ACQUISITION_CANCELLED" : "TC_ACQUISITION_TIMEOUT");
        }
        catch (Exception)
        {
            // No exception message or invented success crosses the Application boundary.
            outcome = TcAcquisitionOutcome.Refused("TC_ACQUISITION_FAILED");
        }

        // Cleanup completes BEFORE an outcome can be consumed by the Runtime writer. A cleanup
        // failure or timeout invalidates even a successful read. Both operations are attempted;
        // a non-cooperative implementation cannot hang shutdown or turn a late read into success.
        var cleanupFailed = false;
        if (client is not null)
        {
            // Runtime, session and attempt cancellation must not abandon cleanup observation.
            // Each step still has its own finite timeout, independent of those cancelled tokens.
            try { await client.DisconnectAsync().WaitAsync(AttemptLimit, CancellationToken.None).ConfigureAwait(false); }
            catch (Exception) { cleanupFailed = true; }
            try { await client.DisposeAsync().AsTask().WaitAsync(AttemptLimit, CancellationToken.None).ConfigureAwait(false); }
            catch (Exception) { cleanupFailed = true; }
        }

        if (cleanupFailed) return TcAcquisitionOutcome.Refused("TC_ACQUISITION_CLEANUP_FAILED");
        if (runtimeToken.IsCancellationRequested || _stop.IsCancellationRequested
            || Volatile.Read(ref _epoch) != epoch)
            return TcAcquisitionOutcome.Refused("TC_ACQUISITION_CANCELLED_OR_SUPERSEDED");
        return outcome;
    }

    private async Task<TcAcquisitionOutcome> AcquireAsync(ITcReadOnlyClient client, CancellationToken token)
    {
        // This marker is not an IP address. No physical endpoint is ever constructed or read.
        var endpoint = new TcNodeEndpoint
        {
            NodeId = DeterministicTcVocabulary.NodeId,
            IpAddress = DeterministicTcReadOnlyClient.EndpointMarker,
        };
        var connected = await client.ConnectAsync(endpoint, token).WaitAsync(token).ConfigureAwait(false);
        if (connected.Kind != TcReadKind.Success)
            return TcAcquisitionOutcome.Refused("TC_ACQUISITION_CONNECT_REFUSED");
        var generation = connected.Generation;
        if (generation != client.Generation || !client.IsConnected || client.ConnectedEndpoint != endpoint
            || connected.Endpoint != endpoint)
            return TcAcquisitionOutcome.Refused("TC_ACQUISITION_STALE_GENERATION");

        // The interface requires a read-only area and a start field. These fixed placeholders are
        // not physical register evidence; the deterministic implementation performs no transport I/O.
        var request = new TcReadRequest
        {
            Endpoint = endpoint,
            Generation = generation,
            StartAddress = 0,
            Quantity = DeterministicTcReadOnlyClient.WordCount,
            RegisterArea = TcRegisterArea.HoldingRegisters,
            Endianness = TcEndiannessCandidate.BigEndian,
            Timeout = AttemptLimit,
        };
        var read = await client.ReadAsync(request, token).WaitAsync(token).ConfigureAwait(false);
        if (token.IsCancellationRequested || read.Kind == TcReadKind.StaleGenerationRejected
            || read.Generation != generation || client.Generation != generation
            || !client.IsConnected || client.ConnectedEndpoint != endpoint)
            return TcAcquisitionOutcome.Refused("TC_ACQUISITION_STALE_GENERATION");
        if (read.Kind != TcReadKind.Success || read.Endpoint != endpoint
            || read.RegisterArea != request.RegisterArea || read.Endianness != request.Endianness
            || read.RawWords is null || read.RawWords.Length != DeterministicTcVocabulary.WordCount)
            return TcAcquisitionOutcome.Refused("TC_ACQUISITION_READ_REFUSED");
        for (var index = 0; index < read.RawWords.Length; index++)
        {
            if (read.RawWords[index] != DeterministicTcVocabulary.WordAt(index))
                return TcAcquisitionOutcome.Refused("TC_ACQUISITION_INVALID_WORDS");
        }

        // Both collections are copied before the client is disconnected/disposed. The clock belongs
        // to the Application, not the deterministic client's synthetic result timestamp.
        var state = new TcAcquisitionState
        {
            NodeId = DeterministicTcVocabulary.NodeId,
            CabinetId = DeterministicTcVocabulary.CabinetId,
            Provenance = DeterministicTcVocabulary.Provenance,
            Physical = false,
            Quarantined = true,
            AcquisitionState = DeterministicTcVocabulary.Complete,
            AcquiredAtUtc = _clock.UtcNow.ToUniversalTime(),
            CopiedWordCount = read.RawWords.Length,
            RawWords = Array.AsReadOnly((ushort[])read.RawWords.Clone()),
            Quality = DeterministicTcVocabulary.Quality,
            DiagnosticState = DeterministicTcVocabulary.NoDiagnostic,
            MappingStatus = DeterministicTcVocabulary.MappingNotVerified,
            DecodeStatus = DeterministicTcVocabulary.DecodeNotVerified,
            NotVerified = Array.AsReadOnly(DeterministicTcVocabulary.NotVerified.ToArray()),
        };
        return TcAcquisitionOutcome.Success(state);
    }

    public async ValueTask DisposeAsync()
    {
        Task<TcAcquisitionOutcome>? task;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            Interlocked.Increment(ref _epoch);
            task = _task;
        }
        _stop.Cancel();
        if (task is not null) await task.ConfigureAwait(false);
        _stop.Dispose();
    }
}
