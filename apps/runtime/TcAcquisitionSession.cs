using System.Collections.ObjectModel;
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
/// The immutable acquisition profile ONE session is created for. The profile fixes the mode, the
/// endpoint, the function code, the read tuple and the resulting record variant BEFORE a client
/// exists, so a session can never switch mode, correct its profile, retry with a different tuple or
/// fall back to the other variant. There is no profile-selection logic inside the session.
/// </summary>
public sealed record TcSessionProfile
{
    public required TcAcquisitionMode Mode { get; init; }

    /// <summary>The endpoint the session connects to. Non-address marker in deterministic mode.</summary>
    public required TcNodeEndpoint Endpoint { get; init; }

    /// <summary>True for the explicit one-Node physical read-only variant.</summary>
    public bool Physical => Mode == TcAcquisitionMode.PHYSICAL_READ_ONLY;

    /// <summary>The requested register area, i.e. the only function code ever issued (FC03).</summary>
    public TcRegisterArea RegisterArea => TcRegisterArea.HoldingRegisters;

    /// <summary>The requested 16-bit word quantity of this profile.</summary>
    public ushort Quantity =>
        Physical ? (ushort)PhysicalTcVocabulary.WordCount : DeterministicTcReadOnlyClient.WordCount;

    /// <summary>The candidate zero-based start address of this profile.</summary>
    public ushort StartAddress =>
        Physical ? (ushort)PhysicalTcVocabulary.StartAddress : (ushort)0;

    /// <summary>The non-Hardware deterministic profile. Identical to the accepted one-Node Product slice.</summary>
    public static TcSessionProfile Deterministic() => new()
    {
        Mode = TcAcquisitionMode.DETERMINISTIC,
        // This marker is deliberately not an address: no socket, device, retry or reconnect exists.
        Endpoint = new TcNodeEndpoint
        {
            NodeId = DeterministicTcVocabulary.NodeId,
            IpAddress = DeterministicTcReadOnlyClient.EndpointMarker,
            Port = PhysicalTcVocabulary.RegisterPort,
            UnitIdentifier = 0x00,
        },
    };

    /// <summary>
    /// The explicit physical read-only profile for the already-resolved Owner-local endpoint. The
    /// endpoint value travels only inside this in-memory profile.
    /// </summary>
    public static TcSessionProfile PhysicalReadOnly(TcNodeEndpoint endpoint) => new()
    {
        Mode = TcAcquisitionMode.PHYSICAL_READ_ONLY,
        Endpoint = endpoint,
    };
}

/// <summary>
/// One-use Product session. It owns its read-only client, cancellation, generation/epoch fencing,
/// copying and cleanup. It never writes Runtime state itself: the host's existing non-overlapping
/// tick consumes a successful outcome and publishes it with the matching Delta through its sole
/// RuntimePublicationStore writer. No UI callback, retry, reconnect or polling loop exists here.
///
/// Mode discipline: the session is created for exactly one immutable <see cref="TcSessionProfile"/>.
/// Deterministic mode asserts the development word sequence and produces the non-Hardware record;
/// physical read-only mode reads the Owner-local endpoint once, accepts whatever 40 words the device
/// returns, and produces a QUARANTINED physical record that can never be accepted by any code path.
/// There is no fallback from one variant to the other in either direction.
/// </summary>
public sealed class TcAcquisitionSession : IAsyncDisposable
{
    // The existing read request default is 2000 ms. Connect + read share this attempt
    // budget; each independent best-effort cleanup operation is also time-bounded.
    private static readonly TimeSpan AttemptLimit = TimeSpan.FromMilliseconds(2000);

    private readonly Func<ITcReadOnlyClient> _clientFactory;
    private readonly IClock _clock;
    private readonly TcSessionProfile _profile;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private Task<TcAcquisitionOutcome>? _task;
    private bool _disposed;
    private int _epoch;

    public TcAcquisitionSession(Func<ITcReadOnlyClient> clientFactory, IClock clock, TcSessionProfile profile)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    /// <summary>The immutable profile this one-use session was created for.</summary>
    public TcSessionProfile Profile => _profile;

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
            // No exception message or invented success crosses the Application boundary. In physical
            // mode no transport exception text can therefore carry the endpoint value outward.
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
        // The endpoint is fixed by the profile before the client exists. In deterministic mode it is
        // the non-address marker and no physical endpoint is ever constructed; in physical mode it is
        // the Owner-local endpoint resolved at startup and never written anywhere.
        var endpoint = _profile.Endpoint;
        var connected = await client.ConnectAsync(endpoint, token).WaitAsync(token).ConfigureAwait(false);
        if (connected.Kind != TcReadKind.Success)
            return TcAcquisitionOutcome.Refused("TC_ACQUISITION_CONNECT_REFUSED");
        var generation = connected.Generation;
        if (generation != client.Generation || !client.IsConnected || client.ConnectedEndpoint != endpoint
            || connected.Endpoint != endpoint)
            return TcAcquisitionOutcome.Refused("TC_ACQUISITION_STALE_GENERATION");

        // FC03 Read Holding Registers only. The register area is fixed by the profile and cannot be
        // selected, widened or corrected by configuration: FC04 and every write function code remain
        // unreachable from this path.
        var request = new TcReadRequest
        {
            Endpoint = endpoint,
            Generation = generation,
            StartAddress = _profile.StartAddress,
            Quantity = _profile.Quantity,
            RegisterArea = _profile.RegisterArea,
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
            || read.RawWords is null || read.RawWords.Length != _profile.Quantity)
            return TcAcquisitionOutcome.Refused("TC_ACQUISITION_READ_REFUSED");

        // Both collections are copied before the client is disconnected/disposed. The clock belongs to
        // the Application, never to the client's own result timestamp.
        var copiedWords = Array.AsReadOnly((ushort[])read.RawWords.Clone());
        var acquiredAtUtc = _clock.UtcNow.ToUniversalTime();

        return _profile.Physical
            ? BuildPhysicalState(copiedWords, acquiredAtUtc)
            : BuildDeterministicOutcome(copiedWords, acquiredAtUtc);
    }

    /// <summary>
    /// The deterministic variant: the development word sequence is asserted, so a deterministic run
    /// can never carry device data, and the record keeps the accepted non-Hardware vocabulary.
    /// </summary>
    private static TcAcquisitionOutcome BuildDeterministicOutcome(
        ReadOnlyCollection<ushort> copiedWords, DateTimeOffset acquiredAtUtc)
    {
        for (var index = 0; index < copiedWords.Count; index++)
        {
            if (copiedWords[index] != DeterministicTcVocabulary.WordAt(index))
                return TcAcquisitionOutcome.Refused("TC_ACQUISITION_INVALID_WORDS");
        }

        return TcAcquisitionOutcome.Success(new TcAcquisitionState
        {
            NodeId = DeterministicTcVocabulary.NodeId,
            CabinetId = DeterministicTcVocabulary.CabinetId,
            Provenance = DeterministicTcVocabulary.Provenance,
            Physical = false,
            Quarantined = true,
            AcquisitionState = DeterministicTcVocabulary.Complete,
            AcquiredAtUtc = acquiredAtUtc,
            CopiedWordCount = copiedWords.Count,
            RawWords = copiedWords,
            Quality = DeterministicTcVocabulary.Quality,
            DiagnosticState = DeterministicTcVocabulary.NoDiagnostic,
            MappingStatus = DeterministicTcVocabulary.MappingNotVerified,
            DecodeStatus = DeterministicTcVocabulary.DecodeNotVerified,
            NotVerified = Array.AsReadOnly(DeterministicTcVocabulary.NotVerified.ToArray()),
        });
    }

    /// <summary>
    /// The physical read-only variant. Whatever the device returned is preserved verbatim and stays
    /// QUARANTINED: no scaling, no signed interpretation, no wire-break classification and no
    /// acceptance is applied here. The record states the exact read profile that was used and the
    /// redacted endpoint identity — never the address — so a reader can audit the acquisition without
    /// the confidential value ever leaving the Owner's machine.
    /// </summary>
    private static TcAcquisitionOutcome BuildPhysicalState(
        ReadOnlyCollection<ushort> copiedWords, DateTimeOffset acquiredAtUtc) =>
        TcAcquisitionOutcome.Success(new TcAcquisitionState
        {
            NodeId = PhysicalTcVocabulary.NodeId,
            CabinetId = PhysicalTcVocabulary.CabinetId,
            Provenance = PhysicalTcVocabulary.Provenance,
            Physical = true,
            Quarantined = true,
            AcquisitionState = PhysicalTcVocabulary.Complete,
            AcquiredAtUtc = acquiredAtUtc,
            CopiedWordCount = copiedWords.Count,
            RawWords = copiedWords,
            Quality = PhysicalTcVocabulary.Quality,
            DiagnosticState = PhysicalTcVocabulary.NoDiagnostic,
            MappingStatus = PhysicalTcVocabulary.MappingStatus,
            DecodeStatus = PhysicalTcVocabulary.DecodeStatus,
            NotVerified = Array.AsReadOnly(PhysicalTcVocabulary.NotVerified.ToArray()),
            PhysicalEvidence = new TcPhysicalReadOnlyEvidence
            {
                EndpointIdentity = PhysicalTcVocabulary.EndpointConfigured,
                ReadProfile = TcEndpointConfiguration.AuthorizedReadProfile,
                // CAB1 carries two DRAWING_INTERNAL_CONFLICT rows (offsets 13 and 14); its mapping is
                // therefore declared but not fully drawing-verified, and the summary says so.
                MappingEvidenceState = PhysicalTcVocabulary.MappingEvidenceDrawingInternalConflict,
                CommissioningState = PhysicalTcVocabulary.CommissioningState,
            },
        });

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
