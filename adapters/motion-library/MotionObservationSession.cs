namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The B2 read-only observed-state session: the single owner of one observation slot, with a
/// generation fence, a fail-fast busy refusal and deterministic close.
///
/// What this type is. A typed, vendor-neutral model of "what the controller is doing" with two
/// honest modes. Without a read boundary it can only report UNAVAILABLE (valid subject, no
/// boundary attached) or REFUSED (the request was not allowed to proceed). With a read boundary
/// attached it issues exactly one typed intent per request through
/// <see cref="IObservationReadKernel"/> and reports the bounded native value, or the canonical
/// refusal that replaced it.
///
/// Why this file stays vendor-neutral. The authority split is: the B1 identity surface resolves
/// six typed identity exports and nothing else, while the separately authorized B2 read surface
/// resolves ONE command-and-response export behind an internal, typed, length-bounded plan. This
/// file is the model and the discipline, not the wire: it names no export, no native call, no
/// endpoint and no request text, and it reaches the wire only through a typed interface whose
/// single method takes an intent and returns a bounded result. There is no I/O call in this file
/// at all, so nothing here can contact a controller even by mistake.
///
/// The four B2 candidates (see <see cref="MotionObservedStateVocabulary"/>) are all in the
/// verified subset. The evidence baseline records, per candidate, the read-only proof, the vendor
/// documentation, the bounded request and response shape, the parser boundaries and the
/// interpretation limit.
///
/// Invariants encoded here, each one exercised by the focused suite:
///   - one session is the sole owner of its own observation slot; there is no static or shared
///     state, so two sessions cannot share, steal or observe through each other;
///   - at most ONE observation is outstanding; a second concurrent request is refused, never
///     queued and never retried;
///   - one current generation per session, advanced only by an explicit invalidation; a result
///     tagged with a previous generation is rejected as stale, and a result already accepted for
///     the current generation cannot be replayed;
///   - no retry of a refused or failed read, no reconnect, no polling and no replay: the session
///     has no loop, no timer, no connection and no result cache to re-issue. A second request is
///     a new evaluation, never a retry of the previous one, and nothing is remembered between
///     requests;
///   - close is deterministic and idempotent, and a closed session accepts nothing;
///   - no observation can enable a command: the type has no command member and no reference to
///     the authorization gate, the command gate, the commissioning session, the Runtime or the
///     queue.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed class MotionObservationSession : IDisposable
{
    private const string SurfaceNotAuthorizedDetail =
        "The subject is valid but this session has no read boundary attached, so nothing was "
        + "requested from and nothing was returned by any controller. The observation is reported "
        + "UNAVAILABLE instead of a placeholder, and no position, limit, home, brake or fault "
        + "meaning is inferred.";

    private const string IntentUnmappedDetail =
        "The validated subject is not a member of the closed read plan, so nothing was issued. "
        + "No request text is ever assembled outside that plan.";

    private const string ReadRefusedDetail =
        "The read boundary refused the request or did not return a usable value. The canonical "
        + "refusal word is reported; no vendor error text, exception message or partial value is "
        + "surfaced and nothing is retried.";

    private const string FenceDetail =
        "The result did not belong to the current session generation, or a result was already "
        + "accepted for it. The read is not promoted to a state and nothing is replayed.";

    private const string ResponseNotUsableDetail =
        "A read was attempted but its reply was not one bounded, in-range value, so no "
        + "observation was accepted. The subject is reported UNAVAILABLE rather than a guessed, "
        + "rounded or truncated value.";

    private const string AcceptedDetail =
        "One bounded read was issued and accepted. The value is preserved in its native unit; no "
        + "scaling, no engineering conversion and no limit, home, brake, fault or motion-complete "
        + "meaning is attached.";

    private const string KindUnknownDetail =
        "The requested observation kind is not one of the four typed kinds. Nothing was evaluated " +
        "and nothing was issued.";

    private const string AxisUnknownDetail =
        "The requested subject is not one of the four controller axis letters. Nothing was " +
        "evaluated and nothing was issued.";

    private const string SubjectInvalidDetail =
        "The requested subject is not a valid combination for its kind, or its output number is " +
        "outside the closed output namespace. Nothing was evaluated and nothing was issued.";

    private const string SessionClosedDetail =
        "This observation session is closed. A closed session accepts no further request and never " +
        "reopens; a new session is a new Owner decision, not a retry.";

    private const string OperationBusyDetail =
        "Another observation is still outstanding in this session. The session allows exactly one " +
        "outstanding observation, so the request was refused rather than queued or retried.";

    private readonly object _gate = new();
    private readonly IObservationReadKernel? _readKernel;

    private long _generation = 1;
    private bool _observationInFlight;
    private bool _closed;
    private bool _resultConsumed;
    private string _lastRefusalCode = string.Empty;

    /// <summary>
    /// A session with no read boundary: every valid subject is reported UNAVAILABLE. It holds no
    /// native resource, contacts nothing and can never be promoted to a reading session.
    /// </summary>
    public MotionObservationSession()
    {
    }

    /// <summary>
    /// A session bound to one read boundary. Internal on purpose: the boundary type is internal,
    /// so an application, a route, a contract, a UI or a test fixture cannot attach a read path to
    /// a production session from outside this assembly.
    /// </summary>
    internal MotionObservationSession(IObservationReadKernel readKernel)
    {
        ArgumentNullException.ThrowIfNull(readKernel);
        _readKernel = readKernel;
    }

    /// <summary>The current session generation. One per session; a stale result is rejected against it.</summary>
    public long CurrentGeneration
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    /// <summary>True once the session has been closed. A closed session accepts nothing.</summary>
    public bool IsClosed
    {
        get
        {
            lock (_gate)
            {
                return _closed;
            }
        }
    }

    /// <summary>The last refusal recorded by the fence (staleness or replay). Diagnostics only.</summary>
    internal string LastRefusalCode
    {
        get
        {
            lock (_gate)
            {
                return _lastRefusalCode;
            }
        }
    }

    /// <summary>
    /// True when this session holds no usable read boundary: it either never had one or has been
    /// closed. This is the cleanup truth of the B2 layer — the boundary is invalidated before the
    /// lifecycle owner closes the controller handle and releases the native module, and it can
    /// never be used again afterwards.
    /// </summary>
    internal bool ReadBoundaryInvalidated
    {
        get
        {
            var kernel = _readKernel;
            return kernel is null || kernel.IsInvalidated;
        }
    }

    /// <summary>True while the single observation slot is held.</summary>
    internal bool ObservationInFlight
    {
        get
        {
            lock (_gate)
            {
                return _observationInFlight;
            }
        }
    }

    /// <summary>
    /// Evaluates one observation request. Without a read boundary a valid request produces an
    /// UNAVAILABLE outcome; with one it produces either an accepted bounded observation or the
    /// canonical refusal that replaced it. An invalid or disallowed request always produces a
    /// REFUSED outcome. Every path is fail-closed, nothing is retried, and this method never
    /// throws for a refused request.
    /// </summary>
    public MotionObservationOutcome Observe(MotionObservationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsClosed)
        {
            return Refuse(MotionObservedStateVocabulary.ObservationSessionClosed, SessionClosedDetail);
        }

        var kind = request.Kind ?? string.Empty;
        var axisId = request.AxisId ?? string.Empty;
        var outputNumber = request.OutputNumber;

        if (!MotionObservedStateVocabulary.ObservationKinds.Contains(kind, StringComparer.Ordinal))
        {
            return Refuse(MotionObservedStateVocabulary.ObservationKindUnknown, KindUnknownDetail);
        }

        var isDigitalOutput = string.Equals(
            kind, MotionObservedStateVocabulary.KindDigitalOutput, StringComparison.Ordinal);

        if (isDigitalOutput)
        {
            if (outputNumber < MotionObservedStateVocabulary.MinimumOutputNumber
                || outputNumber > MotionObservedStateVocabulary.MaximumOutputNumber)
            {
                return Refuse(MotionObservedStateVocabulary.ObservationSubjectInvalid, SubjectInvalidDetail);
            }

            if (axisId.Length != 0)
            {
                return Refuse(MotionObservedStateVocabulary.ObservationSubjectInvalid, SubjectInvalidDetail);
            }
        }
        else
        {
            if (!MotionObservedStateVocabulary.AxisIds.Contains(axisId, StringComparer.Ordinal))
            {
                return Refuse(MotionObservedStateVocabulary.ObservationAxisUnknown, AxisUnknownDetail);
            }

            if (outputNumber != 0)
            {
                return Refuse(MotionObservedStateVocabulary.ObservationSubjectInvalid, SubjectInvalidDetail);
            }
        }

        if (!TryBeginObservation())
        {
            return IsClosed
                ? Refuse(MotionObservedStateVocabulary.ObservationSessionClosed, SessionClosedDetail)
                : Refuse(MotionObservedStateVocabulary.OperationBusy, OperationBusyDetail);
        }

        try
        {
            var subjectAxis = isDigitalOutput ? MotionObservedStateVocabulary.NoAxis : axisId;
            var subjectOutput = isDigitalOutput ? outputNumber : (int?)null;
            var generation = CurrentGeneration;
            var kernel = _readKernel;

            if (kernel is null)
            {
                var unavailable = MotionObservedState.Unavailable(
                    kind,
                    subjectAxis,
                    subjectOutput,
                    MotionObservedStateVocabulary.ObservationSurfaceNotAuthorized);

                return MotionObservationOutcome.Unavailable(
                    MotionObservedStateVocabulary.ObservationSurfaceNotAuthorized,
                    SurfaceNotAuthorizedDetail,
                    generation,
                    unavailable);
            }

            if (!MotionObservationCommandPlan.TryResolveIntent(
                    kind,
                    subjectAxis,
                    outputNumber,
                    out var intent))
            {
                return Refuse(
                    MotionObservedStateVocabulary.ObservationIntentUnmapped,
                    IntentUnmappedDetail);
            }

            var read = kernel.Issue(intent);
            if (!read.Succeeded)
            {
                var refusalCode = read.RefusalCode.Length == 0
                    ? MotionObservedStateVocabulary.ObservationCommandFailed
                    : read.RefusalCode;

                return MotionObservationOutcome.Refused(
                    refusalCode,
                    ReadRefusedDetail,
                    generation,
                    read.Attempted);
            }

            if (!TryAcceptInlineResult(generation))
            {
                return MotionObservationOutcome.Refused(
                    LastRefusalCode,
                    FenceDetail,
                    generation,
                    observationIssued: true);
            }

            var valueAccepted = isDigitalOutput
                ? MotionObservationCommandPlan.TryParseOutputState(read.RawText, out _)
                : MotionObservationCommandPlan.TryParseValue(read.RawText, out _);

            if (!valueAccepted)
            {
                var code = isDigitalOutput
                    ? MotionObservedStateVocabulary.ObservationOutputStateOutOfRange
                    : MotionObservedStateVocabulary.ObservationResponseInvalid;

                var rejected = MotionObservedState.Unavailable(
                    kind,
                    subjectAxis,
                    subjectOutput,
                    code);

                return MotionObservationOutcome.Unavailable(
                    code,
                    ResponseNotUsableDetail,
                    generation,
                    rejected,
                    observationIssued: true);
            }

            var observed = MotionObservedState.Observed(
                kind,
                subjectAxis,
                subjectOutput,
                MotionObservationCommandPlan.NativeUnitFor(intent),
                read.RawText,
                MotionObservationCommandPlan.InterpretationFor(intent),
                MotionObservationCommandPlan.AcceptedDetailFor(intent));

            return MotionObservationOutcome.Observed(
                MotionObservedStateVocabulary.ObservationAccepted,
                AcceptedDetail,
                generation,
                observed);
        }
        finally
        {
            EndObservation();
        }
    }

    /// <summary>Fail-fast one-outstanding gate: true when the caller now owns the single observation slot.</summary>
    internal bool TryBeginObservation()
    {
        lock (_gate)
        {
            if (_closed || _observationInFlight)
            {
                return false;
            }

            _observationInFlight = true;
            return true;
        }
    }

    /// <summary>Releases the single observation slot.</summary>
    internal void EndObservation()
    {
        lock (_gate)
        {
            _observationInFlight = false;
        }
    }

    /// <summary>
    /// Moves the session to the next generation, invalidating every result tagged with an earlier
    /// one. Returns the new generation.
    /// </summary>
    internal long InvalidateGeneration()
    {
        lock (_gate)
        {
            _generation++;
            _resultConsumed = false;
            return _generation;
        }
    }

    /// <summary>
    /// The fence for an INLINE read result: the session is still open and the generation captured
    /// before the issue is still current. A close or an invalidation during the read therefore
    /// rejects the value instead of promoting it. An inline result cannot be replayed — it is
    /// returned to the caller that issued it — so this helper does not consume the one-shot slot
    /// that <see cref="TryAcceptResult"/> guards.
    /// </summary>
    private bool TryAcceptInlineResult(long generation)
    {
        lock (_gate)
        {
            if (_closed)
            {
                _lastRefusalCode = MotionObservedStateVocabulary.ObservationSessionClosed;
                return false;
            }

            if (generation != _generation)
            {
                _lastRefusalCode = MotionObservedStateVocabulary.StaleGenerationResult;
                return false;
            }

            _lastRefusalCode = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// The generation fence for a future authorized read: a result is accepted only when it
    /// belongs to the current generation, only once, and only while the session is open. Every
    /// other result is rejected and the reason is recorded. No result can exist in this revision;
    /// the fence is here so the rule cannot be added later as an afterthought.
    /// </summary>
    internal bool TryAcceptResult(long generation)
    {
        lock (_gate)
        {
            if (_closed)
            {
                _lastRefusalCode = MotionObservedStateVocabulary.ObservationSessionClosed;
                return false;
            }

            if (generation != _generation)
            {
                _lastRefusalCode = MotionObservedStateVocabulary.StaleGenerationResult;
                return false;
            }

            if (_resultConsumed)
            {
                _lastRefusalCode = MotionObservedStateVocabulary.ResultReplayed;
                return false;
            }

            _resultConsumed = true;
            _lastRefusalCode = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Deterministic close: invalidate the read boundary first, then release the single
    /// observation slot and refuse every later request. The boundary invalidation performs no
    /// native call, so the lifecycle owner closes the controller handle and releases the native
    /// module afterwards in that order. Idempotent and non-throwing.
    /// </summary>
    public void Dispose()
    {
        IObservationReadKernel? kernel;
        lock (_gate)
        {
            _closed = true;
            _observationInFlight = false;
            kernel = _readKernel;
        }

        kernel?.Dispose();
    }

    private MotionObservationOutcome Refuse(string code, string detail) =>
        MotionObservationOutcome.Refused(code, detail, CurrentGeneration);
}
