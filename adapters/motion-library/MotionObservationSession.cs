namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The B2 read-only observed-state session: the single owner of one observation slot, with a
/// generation fence, a fail-fast busy refusal and deterministic close.
///
/// What this type is. A typed, vendor-neutral model of "what the controller is doing" whose only
/// reachable outcome in this revision is a truthful UNAVAILABLE (valid subject, no authorized
/// read surface) or a REFUSED (the request itself was not allowed to proceed). It owns no
/// controller handle, loads no native module, opens no connection, knows no endpoint and contains
/// no command text, so it cannot contact anything even by mistake: there is no I/O call in this
/// file at all.
///
/// Why the read is not performed. The four B2 candidates (see
/// <see cref="MotionObservedStateVocabulary"/>) all need the raw command-and-response path, which
/// the B1 gate does not authorize: its authority line is "no raw controller command, no axis
/// read", and the identity surface resolves exactly six typed exports, none of which reads an
/// axis. Adding a seventh export would be a scope and authorization change, not an implementation
/// detail, so it is reported to the Owner instead of being taken here. The evidence baseline
/// records the disposition of each candidate and the exact reason.
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
        "The subject is valid but no authorized read surface exists, so nothing was requested from " +
        "and nothing was returned by any controller. The B1 gate authorizes six typed identity " +
        "exports only and permits no raw command and no axis read. The observation is reported " +
        "UNAVAILABLE instead of a placeholder, and no position, limit, home, brake or fault " +
        "meaning is inferred.";

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

    private long _generation = 1;
    private bool _observationInFlight;
    private bool _closed;
    private bool _resultConsumed;
    private string _lastRefusalCode = string.Empty;

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
    /// Evaluates one observation request. Valid requests produce an UNAVAILABLE outcome and
    /// invalid or disallowed requests produce a REFUSED outcome; both are fail-closed and neither
    /// contacts anything. This method never throws for a refused request.
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
            var state = MotionObservedState.Unavailable(
                kind,
                isDigitalOutput ? MotionObservedStateVocabulary.NoAxis : axisId,
                isDigitalOutput ? outputNumber : null,
                MotionObservedStateVocabulary.ObservationSurfaceNotAuthorized);

            return MotionObservationOutcome.Unavailable(
                MotionObservedStateVocabulary.ObservationSurfaceNotAuthorized,
                SurfaceNotAuthorizedDetail,
                CurrentGeneration,
                state);
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
    /// Deterministic close: the session holds no native module and no controller handle, so close
    /// means releasing the single observation slot and refusing every later request. Idempotent.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _closed = true;
            _observationInFlight = false;
        }
    }

    private MotionObservationOutcome Refuse(string code, string detail) =>
        MotionObservationOutcome.Refused(code, detail, CurrentGeneration);
}
