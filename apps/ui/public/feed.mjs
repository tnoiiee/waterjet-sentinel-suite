// WJSS Operator presentation - the only module in this UI that touches the network.
//
// Read-only by construction: every request this file issues is a GET against a same-origin
// relative path served by Wjss.Runtime. No address, port, host or scheme is read from the
// environment, from a query string or from any input: the presentation cannot be pointed at
// another machine because it never names one. No write verb, no streaming request and no
// retry-with-replay exists here; a command surface does not exist in the host either.
//
// Gap safety (the accepted 0.2.1A rule, carried unchanged): the Snapshot is the authoritative
// document, the Delta feed is metadata about committed revisions, and any discontinuity -
// a fetch failure, a revision that does not advance, or a feed entry that provably does not
// continue its newer neighbour - makes the next frame a fresh full Snapshot. Nothing is
// inferred from a cached partial state and no Delta is ever replayed.

export const SNAPSHOT_PATH = '/api/v1/snapshot';
export const DELTAS_PATH = '/api/v1/deltas';
export const RUNTIME_PATH = '/api/v1/runtime';
export const WORKSPACE_PATH = '/api/v1/dispatch-workspace';
export const HEALTH_PATH = '/api/health';

/** Poll cadence, in milliseconds. The Runtime commits at most one revision per tick. */
export const POLL_INTERVAL_MS = 1000;

/** Rows of bounded Delta history the presentation keeps in the DOM. Never unbounded. */
export const MAX_DELTA_ROWS = 60;

/** Consecutive failed attempts before the frame is reported offline rather than merely stale. */
export const OFFLINE_AFTER_FAILURES = 3;

/** Every poll path, exported so the source contract test can enumerate the whole surface. */
export const POLL_PATHS = Object.freeze([SNAPSHOT_PATH, DELTAS_PATH, RUNTIME_PATH, WORKSPACE_PATH]);

export const HEALTH_EVERY_TICKS = 10;

export class FeedError extends Error {
  constructor(code, detail) {
    super(code + ': ' + detail);
    this.name = 'FeedError';
    this.code = code;
    this.detail = detail;
  }
}

function codeOf(response) {
  return 'HTTP_' + String(response.status);
}

// The 503 body of a not-yet-initialized Runtime carries its own machine code, and that code is a
// more useful fact on screen than the status line. A body that is not the expected shape is never
// guessed at: the status code stands on its own.
async function failureOf(response) {
  let code = codeOf(response);
  let detail = 'the host answered ' + String(response.status);
  try {
    const body = await response.json();
    if (body && typeof body.code === 'string' && body.code.length > 0) {
      code = body.code;
      detail = typeof body.detail === 'string' && body.detail.length > 0 ? body.detail : detail;
    }
  } catch {
    // A non-JSON error body is not a defect: the status code is already the fact.
  }
  return new FeedError(code, detail);
}

export async function getJson(path, signal) {
  let response;
  try {
    response = await fetch(path, { method: 'GET', cache: 'no-store', signal });
  } catch (error) {
    if (error && error.name === 'AbortError') throw new FeedError('ABORTED', path);
    throw new FeedError('FETCH_FAILED', path + ' is not answering');
  }
  if (!response.ok) throw await failureOf(response);
  try {
    return await response.json();
  } catch {
    throw new FeedError('INVALID_RESPONSE', path + ' did not carry a readable document');
  }
}

function isInteger(value) {
  return typeof value === 'number' && Number.isFinite(value) && Math.round(value) === value;
}

/**
 * The Snapshot is the only document that may establish the presented revision. A payload whose
 * discriminator or revision is not what the contract promises is refused, and the previous frame
 * stays on screen marked stale instead of being partially adopted.
 */
export function validateSnapshot(document) {
  if (!document || typeof document !== 'object') return 'the Snapshot document is absent';
  if (document.kind !== 'snapshot') return 'the Snapshot discriminator is ' + String(document.kind);
  if (!isInteger(document.revision)) return 'the Snapshot revision is not an integer';
  if (!Array.isArray(document.sensors)) return 'the Sensor set is absent';
  if (!Array.isArray(document.wallMap)) return 'the wall map is absent';
  return null;
}

/**
 * Chain continuity of the bounded feed, read as data: an entry that provably does not continue its
 * newer neighbour is a gap the screen must show. The window boundary is never a gap, so only the
 * entries the host itself flags are counted.
 */
export function countFeedGaps(feed) {
  const items = feed && Array.isArray(feed.items) ? feed.items : [];
  let gaps = 0;
  for (const item of items) {
    if (item && item.hasRevisionGap === true) gaps += 1;
  }
  return gaps;
}

export function createFeed(handlers) {
  const onFrame = typeof handlers.onFrame === 'function' ? handlers.onFrame : () => {};
  const onFault = typeof handlers.onFault === 'function' ? handlers.onFault : () => {};

  const state = {
    snapshot: null,
    status: null,
    workspace: null,
    feed: null,
    health: null,
    revision: null,
    lastGoodAt: null,
    lastServerTime: null,
    ticks: 0,
    resyncs: 0,
    consecutiveFailures: 0,
    failures: [],
    phase: 'booting',
    snapshotError: null,
  };

  let timer = null;
  let controller = null;
  let running = false;

  function setPhase(next) {
    if (state.phase === next) return;
    state.phase = next;
  }

  function noteFailure(error) {
    state.consecutiveFailures += 1;
    state.failures = [String(error && error.code ? error.code : 'UNKNOWN'),
      String(error && error.detail ? error.detail : 'unknown')]
      .concat(state.failures.slice(0, 9));
    setPhase(state.consecutiveFailures >= OFFLINE_AFTER_FAILURES ? 'offline' : 'stale');
  }

  async function tick() {
    if (!running) return;
    state.ticks += 1;
    if (controller) controller.abort();
    controller = typeof AbortController === 'function' ? new AbortController() : null;
    const signal = controller ? controller.signal : undefined;

    const wanted = POLL_PATHS.slice();
    if (state.ticks % HEALTH_EVERY_TICKS === 1) wanted.push(HEALTH_PATH);
    const results = await Promise.all(wanted.map((path) => getJson(path, signal)
      .then((document) => ({ path, ok: true, document }))
      .catch((error) => ({ path, ok: false, error }))));

    const byPath = new Map();
    for (const result of results) byPath.set(result.path, result);

    const snapshotResult = byPath.get(SNAPSHOT_PATH);
    if (!snapshotResult || !snapshotResult.ok) {
      const error = snapshotResult ? snapshotResult.error : new FeedError('NO_RESPONSE', SNAPSHOT_PATH);
      state.snapshotError = String(error.code) + ' - ' + String(error.detail);
      noteFailure(error);
      onFrame(state);
      onFault(error, state);
      return;
    }

    const invalid = validateSnapshot(snapshotResult.document);
    if (invalid) {
      const error = new FeedError('SNAPSHOT_REJECTED', invalid);
      state.snapshotError = error.code + ' - ' + error.detail;
      noteFailure(error);
      onFrame(state);
      onFault(error, state);
      return;
    }

    const previous = state.revision;
    const next = snapshotResult.document.revision;
    // Any movement that is not a forward advance is a discontinuity the presentation does not
    // paper over: the new Snapshot is adopted whole (it is a whole document) and the frame is
    // announced as a re-synchronization so a reader can see it happened.
    const resynced = previous !== null && next <= previous;
    if (resynced) state.resyncs += 1;

    state.snapshot = snapshotResult.document;
    state.revision = next;
    state.snapshotError = null;
    state.consecutiveFailures = 0;
    state.lastGoodAt = new Date().toISOString();

    const statusResult = byPath.get(RUNTIME_PATH);
    if (statusResult && statusResult.ok) state.status = statusResult.document;
    const workspaceResult = byPath.get(WORKSPACE_PATH);
    if (workspaceResult && workspaceResult.ok) state.workspace = workspaceResult.document;
    const feedResult = byPath.get(DELTAS_PATH);
    if (feedResult && feedResult.ok) state.feed = feedResult.document;
    // The liveness document is polled on a slower cadence than the data: it answers the shell's
    // question, not the operator's. The last value read stays as the displayed fact, because a
    // slower cadence is not a reason to show NOT_READ for a host that answered moments ago.
    const healthResult = byPath.get(HEALTH_PATH);
    if (healthResult && healthResult.ok) state.health = healthResult.document;

    state.lastServerTime = state.snapshot.generatedAt || (state.status && state.status.serverTimeUtc) || null;
    setPhase('live');
    onFrame(state, { resynced, previous, next });
  }

  return {
    state,
    start() {
      if (running) return;
      running = true;
      setPhase('booting');
      // The first frame is always a full Snapshot: a reconnect never resumes from a partial view.
      timer = setInterval(() => { void tick(); }, POLL_INTERVAL_MS);
      void tick();
    },
    stop() {
      running = false;
      if (timer !== null) clearInterval(timer);
      timer = null;
      if (controller) controller.abort();
      controller = null;
    },
    pollNow: tick,
  };
}

/** Formatting helpers shared by the renderers, so a missing value always reads as a fact. */
export function text(value, fallback) {
  if (value === null || value === undefined) return fallback === undefined ? 'NOT_PROVIDED' : fallback;
  if (typeof value === 'number' && !Number.isFinite(value)) return 'NOT_PROVIDED';
  if (value === '') return fallback === undefined ? 'NOT_PROVIDED' : fallback;
  return String(value);
}

export function number(value, digits, unit) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return 'NOT_PROVIDED';
  const fixed = value.toFixed(digits === undefined ? 2 : digits);
  return unit ? fixed + ' ' + unit : fixed;
}

export function timeOf(value) {
  const raw = text(value, '');
  if (raw === '') return 'NOT_PROVIDED';
  const parsed = new Date(raw);
  if (Number.isNaN(parsed.getTime())) return raw;
  return parsed.toISOString().slice(11, 19);
}

export function clockOf(value) {
  const raw = text(value, '');
  if (raw === '') return '--:--:--';
  const parsed = new Date(raw);
  if (Number.isNaN(parsed.getTime())) return raw;
  return parsed.toISOString().slice(11, 19) + ' UTC';
}
