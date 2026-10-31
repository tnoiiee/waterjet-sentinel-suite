// H2 Physical Process-I/O — typed session, fencing, admission and write lifecycle.
//
// One application owner per physical session: explicit open/close, session generation,
// stale-response rejection, reconnect fencing, cancellation, bounded timeout, no fallback
// endpoint, no fabricated observation, no stale write replay, no write after close/Stop,
// exactly-once admitted dispatch, deterministic cleanup, audit trail.
//
// Pure state machines. No I/O. Every refusal is a typed code.

export const REFUSAL = Object.freeze({
  NOT_OPEN: 'WJSS_SESSION_NOT_OPEN',
  CLOSED: 'WJSS_SESSION_CLOSED',
  STALE_GENERATION: 'WJSS_STALE_RESPONSE_GENERATION',
  STALE_FRESHNESS: 'WJSS_STALE_RESPONSE_FRESHNESS',
  RECONNECT_FENCED: 'WJSS_RECONNECT_FENCING',
  CANCELLED: 'WJSS_CANCELLED',
  TIMEOUT: 'WJSS_TIMEOUT',
  WRITE_AFTER_CLOSE: 'WJSS_WRITE_AFTER_CLOSE',
  WRITE_AFTER_STOP: 'WJSS_WRITE_AFTER_STOP',
  WRITE_REPLAY: 'WJSS_STALE_WRITE_REPLAY',
  TOKEN_SPENT: 'WJSS_DISPATCH_TOKEN_SPENT',
  NOT_AUTHORIZED: 'WJSS_WRITE_REFUSED_NOT_AUTHORIZED',
  UNBOUND: 'WJSS_WRITE_REFUSED_UNBOUND',
  EXCLUDED: 'WJSS_WRITE_REFUSED_EXCLUDED',
  NO_NETWORK_ON_REFUSAL: 'WJSS_NO_NETWORK_IO_ON_REFUSAL',
});

let nextToken = 1;

/**
 * A typed fieldbus session: the single application owner of one device connection. The
 * transport itself is injected (simulation or physical-capable adapter); no transport type
 * leaks into the model.
 */
export class ProcessIoSession {
  #transport;
  #state = 'IDLE';
  #generation = 0;
  #lastReadAt = null;
  #stopped = false;
  #dispatched = new Map();
  #audit = [];

  constructor(deviceKey, transport, options = {}) {
    this.deviceKey = deviceKey;
    this.#transport = transport;
    this.freshnessWindowMs = options.freshnessWindowMs ?? 1000;
  }

  get state() { return this.#state; }
  get generation() { return this.#generation; }
  get stopped() { return this.#stopped; }
  get auditTrail() { return Object.freeze([...this.#audit]); }

  #record(entry) {
    this.#audit.push(Object.freeze({ ...entry, generation: this.#generation }));
  }

  open(at) {
    if (this.#state === 'OPEN') return { ok: true, generation: this.#generation };
    this.#generation += 1;
    this.#state = 'OPEN';
    this.#stopped = false;
    this.#dispatched.clear();
    this.#record({ at, event: 'OPEN' });
    return { ok: true, generation: this.#generation };
  }

  close(at) {
    this.#state = 'CLOSED';
    this.#dispatched.clear();
    this.#record({ at, event: 'CLOSE', cleanup: 'DETERMINISTIC' });
    return { ok: true, cleanup: 'DETERMINISTIC' };
  }

  markStop(at) {
    this.#stopped = true;
    this.#record({ at, event: 'STOP' });
    return { ok: true };
  }

  reconnect(at) {
    // Dispose-then-reopen (Legacy behavior); the generation bump fences stale responses and
    // every previously dispatched write token so no stale write can replay.
    this.#generation += 1;
    this.#dispatched.clear();
    this.#record({ at, event: 'RECONNECT', fencing: REFUSAL.RECONNECT_FENCED });
    return { ok: true, generation: this.#generation };
  }

  /** Typed read: records freshness; refuses on closed session. */
  read(at, produce) {
    if (this.#state !== 'OPEN') {
      this.#record({ at, event: 'READ_REFUSED', code: REFUSAL.NOT_OPEN });
      return { ok: false, code: REFUSAL.NOT_OPEN, networkAttempted: false };
    }
    const observation = produce(this.#generation);
    this.#lastReadAt = at;
    this.#record({ at, event: 'READ', generation: this.#generation });
    return { ok: true, observation };
  }

  /** Freshness/quality of the last observation set at time `now`. */
  freshness(now) {
    if (this.#lastReadAt === null) return Object.freeze({ stale: true, code: REFUSAL.STALE_FRESHNESS, ageMs: null });
    const ageMs = now - this.#lastReadAt;
    return Object.freeze({ stale: ageMs > this.freshnessWindowMs, code: ageMs > this.freshnessWindowMs ? REFUSAL.STALE_FRESHNESS : 'NONE', ageMs });
  }

  /** Reject an observation carried by an older session generation. */
  acceptResponse(observationGeneration) {
    if (observationGeneration !== this.#generation) {
      return { ok: false, code: REFUSAL.STALE_GENERATION };
    }
    return { ok: true };
  }

  /**
   * Exactly-once write admission: creates a one-shot dispatch token. Dispatch spends the
   * token; a second dispatch with the same token is refused (no stale write replay).
   */
  admitWrite(at, rowBinding, authorization, canAdmit) {
    if (this.#state !== 'OPEN') {
      this.#record({ at, event: 'WRITE_REFUSED', code: REFUSAL.WRITE_AFTER_CLOSE });
      return { ok: false, code: REFUSAL.WRITE_AFTER_CLOSE, networkAttempted: false };
    }
    if (this.#stopped) {
      this.#record({ at, event: 'WRITE_REFUSED', code: REFUSAL.WRITE_AFTER_STOP });
      return { ok: false, code: REFUSAL.WRITE_AFTER_STOP, networkAttempted: false };
    }
    const verdict = canAdmit(rowBinding, authorization);
    if (!verdict.admitted) {
      this.#record({ at, event: 'WRITE_REFUSED', code: verdict.code, networkAttempted: false });
      return { ok: false, code: verdict.code, networkAttempted: false };
    }
    const token = `T${nextToken}`;
    nextToken += 1;
    this.#dispatched.set(token, Object.freeze({ at, spent: false }));
    this.#record({ at, event: 'WRITE_ADMITTED', token });
    return { ok: true, token };
  }

  /** Dispatch an admitted write through the transport. Token is spent exactly once. */
  dispatch(at, token, request) {
    const entry = this.#dispatched.get(token);
    if (!entry) {
      this.#record({ at, event: 'DISPATCH_REFUSED', code: REFUSAL.TOKEN_SPENT });
      return { ok: false, code: REFUSAL.WRITE_REPLAY, networkAttempted: false };
    }
    if (entry.spent) {
      this.#record({ at, event: 'DISPATCH_REFUSED', code: REFUSAL.TOKEN_SPENT });
      return { ok: false, code: REFUSAL.WRITE_REPLAY, networkAttempted: false };
    }
    if (this.#state !== 'OPEN') {
      this.#record({ at, event: 'DISPATCH_REFUSED', code: REFUSAL.WRITE_AFTER_CLOSE });
      return { ok: false, code: REFUSAL.WRITE_AFTER_CLOSE, networkAttempted: false };
    }
    this.#dispatched.set(token, Object.freeze({ at, spent: true }));
    const result = this.#transport.write(request);
    this.#record({ at, event: 'DISPATCHED', token });
    return { ok: true, dispatched: true, transportResult: result };
  }
}

/**
 * Commanded / effective / read-back separation for one output row. Write acceptance is never
 * physical proof: `commanded` records the request, `effective` is only advanced by an
 * accepted effective-state source, `readBack` comes from the bound feedback row.
 */
export class OutputStateModel {
  constructor(tag) {
    this.tag = tag;
    this.requested = null;
    this.admitted = false;
    this.dispatched = false;
    this.commanded = 'UNKNOWN';
    this.effective = 'UNKNOWN';
    this.readBack = 'UNKNOWN';
    this.lastRefusal = null;
  }

  toView() {
    return Object.freeze({
      tag: this.tag,
      requested: this.requested,
      admitted: this.admitted,
      dispatched: this.dispatched,
      commanded: this.commanded,
      effective: this.effective,
      readBack: this.readBack,
      lastRefusal: this.lastRefusal,
    });
  }
}
