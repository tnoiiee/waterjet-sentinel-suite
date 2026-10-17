namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// One accepted or refused Stop operation. Created only by <see cref="MotionStopSession.Begin"/>.
///
/// An operation is single-use: it is consumed when it is dispatched or fenced, and it can never be
/// dispatched again. It carries the axis, the generation it was issued for and a session sequence
/// number. It has no public constructor and no way to carry command text.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal sealed class MotionStopOperation
{
    internal MotionStopOperation(
        MotionStopSession? owner,
        MotionStopAxis axis,
        long generation,
        long sequence,
        bool accepted,
        string refusalCode)
    {
        Owner = owner;
        Axis = axis;
        Generation = generation;
        Sequence = sequence;
        Accepted = accepted;
        RefusalCode = refusalCode;
    }

    /// <summary>The session that issued this operation. Null for an operation that was never bound to a session.</summary>
    internal MotionStopSession? Owner { get; }

    /// <summary>The requested axis, as a closed-domain value.</summary>
    internal MotionStopAxis Axis { get; }

    /// <summary>The session generation this operation was issued for.</summary>
    internal long Generation { get; }

    /// <summary>The session-local sequence number of this operation.</summary>
    internal long Sequence { get; }

    /// <summary>True when Begin accepted the operation.</summary>
    internal bool Accepted { get; }

    /// <summary>The canonical refusal code when Begin did not accept the operation. Empty otherwise.</summary>
    internal string RefusalCode { get; }

    /// <summary>True once the operation has been dispatched or fenced. Guarded by the session lock.</summary>
    internal bool Consumed { get; set; }
}

/// <summary>
/// The redacted outcome of one Stop operation. It reports the canonical code, the axis, the
/// generation, whether a native call was attempted, and fixed flags that state what this software
/// foundation never claims. It has no vendor text, no reply text and no exception text.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal sealed class MotionStopOutcome
{
    private MotionStopOutcome(string code, MotionStopAxis axis, long generation, bool nativeDispatchAttempted)
    {
        Code = code;
        Axis = axis;
        Generation = generation;
        NativeDispatchAttempted = nativeDispatchAttempted;
    }

    /// <summary>The canonical outcome code. See <see cref="MotionStopOutcomeCodes"/>.</summary>
    internal string Code { get; }

    /// <summary>The requested axis.</summary>
    internal MotionStopAxis Axis { get; }

    /// <summary>The session generation the outcome belongs to.</summary>
    internal long Generation { get; }

    /// <summary>True when one native call was made for this operation.</summary>
    internal bool NativeDispatchAttempted { get; }

    /// <summary>True only when the single dispatch completed without a classified refusal. This is NOT a confirmed stop.</summary>
    internal bool StopDispatched => string.Equals(Code, MotionStopOutcomeCodes.Dispatched, StringComparison.Ordinal);

    /// <summary>Always false in this software foundation. Confirmation requires a later observed-state contract.</summary>
    internal bool StopConfirmed => false;

    /// <summary>Always false: a Stop is never retried, reconnected or replayed.</summary>
    internal bool RetryPermitted => false;

    /// <summary>The fixed redacted detail for <see cref="Code"/>.</summary>
    internal string Detail => MotionStopOutcomeCodes.DetailFor(Code);

    internal static MotionStopOutcome Create(string code, MotionStopAxis axis, long generation, bool nativeDispatchAttempted) =>
        new(code, axis, generation, nativeDispatchAttempted);
}

/// <summary>
/// The C1 Stop-only session: the single owner of one Stop operation slot, with a generation fence,
/// deterministic duplicate rejection, replay rejection and fail-closed invalidation.
///
/// Invariants, each exercised by the focused suite:
///   - one operation is pending or in flight at a time; a second request is refused as busy, never
///     queued and never retried;
///   - each accepted operation is dispatched at most once: it is consumed before the single native
///     call, so a re-entrant or repeated dispatch is refused as replayed;
///   - the native call is made outside the session lock, so a duplicate issued during the call is
///     refused deterministically rather than blocking or running a second dispatch;
///   - the generation advances on invalidation. An operation issued for an earlier generation is
///     refused as stale before any native call, and a native result that returns after the
///     generation advanced is rejected as stale instead of being reported as dispatched;
///   - there is no loop, no timer, no reconnect, no polling and no readback. A refused or failed
///     dispatch is never repeated;
///   - invalidation makes no native call. The session invalidates its dispatch boundary exactly
///     once, and the lifecycle owner closes the controller handle and unloads the module afterwards.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal sealed class MotionStopSession
{
    private readonly object _gate = new();
    private readonly IStopDispatchKernel _kernel;

    private long _generation = 1;
    private long _sequence;
    private bool _closed;
    private bool _dispatchInFlight;
    private MotionStopOperation? _pending;
    private int _kernelDispatchCount;

    internal MotionStopSession(IStopDispatchKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        _kernel = kernel;
    }

    /// <summary>The current generation. Advances once, on invalidation.</summary>
    internal long CurrentGeneration
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    /// <summary>True once the session has been invalidated. A closed session accepts nothing.</summary>
    internal bool IsClosed
    {
        get
        {
            lock (_gate)
            {
                return _closed;
            }
        }
    }

    /// <summary>The number of calls the session has made to the dispatch boundary. A call may be refused by the boundary and make no native call.</summary>
    internal int KernelDispatchCount
    {
        get
        {
            lock (_gate)
            {
                return _kernelDispatchCount;
            }
        }
    }

    /// <summary>
    /// Accepts or refuses one Stop operation. It only records the request; it never calls native code.
    /// </summary>
    internal MotionStopOperation Begin(MotionStopAxis axis)
    {
        lock (_gate)
        {
            var generation = _generation;

            if (_closed)
            {
                return new MotionStopOperation(this, axis, generation, 0, false, MotionStopOutcomeCodes.SessionClosed);
            }

            if (!MotionStopCommandPlan.IsDefined(axis))
            {
                return new MotionStopOperation(this, axis, generation, 0, false, MotionStopOutcomeCodes.AxisOutOfDomain);
            }

            if (_dispatchInFlight || _pending is not null)
            {
                return new MotionStopOperation(this, axis, generation, 0, false, MotionStopOutcomeCodes.OperationBusy);
            }

            _sequence++;
            var operation = new MotionStopOperation(this, axis, generation, _sequence, true, string.Empty);
            _pending = operation;
            return operation;
        }
    }

    /// <summary>
    /// Dispatches one accepted operation with exactly one native call, then classifies the result.
    /// A refused operation makes no native call. Nothing here is ever retried.
    /// </summary>
    internal MotionStopOutcome Dispatch(MotionStopOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var axis = operation.Axis;

        lock (_gate)
        {
            if (!ReferenceEquals(operation.Owner, this))
            {
                return Outcome(MotionStopOutcomeCodes.OperationForeign, axis, operation.Generation, false);
            }

            if (!operation.Accepted)
            {
                return Outcome(MotionStopOutcomeCodes.OperationNotAccepted, axis, operation.Generation, false);
            }

            if (operation.Consumed)
            {
                return Outcome(MotionStopOutcomeCodes.OperationReplayed, axis, operation.Generation, false);
            }

            if (operation.Generation != _generation)
            {
                Consume(operation);
                return Outcome(MotionStopOutcomeCodes.GenerationStale, axis, operation.Generation, false);
            }

            if (_closed)
            {
                Consume(operation);
                return Outcome(MotionStopOutcomeCodes.SessionClosed, axis, operation.Generation, false);
            }

            if (!ReferenceEquals(_pending, operation) || _dispatchInFlight)
            {
                return Outcome(MotionStopOutcomeCodes.OperationBusy, axis, operation.Generation, false);
            }

            Consume(operation);
            _dispatchInFlight = true;
            _kernelDispatchCount++;
        }

        MotionStopNativeReply reply;
        try
        {
            reply = _kernel.Dispatch(axis);
        }
        catch (Exception)
        {
            // The boundary faulted. The fault is recorded as a failed dispatch; no text is kept.
            reply = MotionStopNativeReply.Faulted();
        }

        lock (_gate)
        {
            _dispatchInFlight = false;

            if (operation.Generation != _generation)
            {
                return Outcome(MotionStopOutcomeCodes.ResultStaleRejected, axis, operation.Generation, reply.Attempted);
            }
        }

        return Outcome(MotionStopAcknowledgement.ClassifyOutcome(reply), axis, operation.Generation, reply.Attempted);
    }

    /// <summary>
    /// Begins and dispatches one Stop in one call. A refused Begin returns its refusal without any
    /// native call. An accepted Stop is dispatched exactly once.
    /// </summary>
    internal MotionStopOutcome Stop(MotionStopAxis axis)
    {
        var operation = Begin(axis);
        if (!operation.Accepted)
        {
            return Outcome(operation.RefusalCode, axis, operation.Generation, false);
        }

        return Dispatch(operation);
    }

    /// <summary>
    /// Invalidates the session and its dispatch boundary. Makes no native call. Idempotent.
    /// Advances the generation once, so every operation issued before this point is stale.
    /// </summary>
    internal void Invalidate()
    {
        lock (_gate)
        {
            if (!_closed)
            {
                _generation++;
                _closed = true;
                _pending = null;
            }
        }

        // Taking the boundary lock waits for any in-flight native call to finish. No native call is
        // made here.
        _kernel.Dispose();
    }

    private void Consume(MotionStopOperation operation)
    {
        operation.Consumed = true;
        if (ReferenceEquals(_pending, operation))
        {
            _pending = null;
        }
    }

    private static MotionStopOutcome Outcome(string code, MotionStopAxis axis, long generation, bool attempted) =>
        MotionStopOutcome.Create(code, axis, generation, attempted);
}
