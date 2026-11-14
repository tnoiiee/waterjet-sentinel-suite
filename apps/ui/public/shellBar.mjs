// WJSS Operator presentation - the Unified Command Bar.
//
// One strip at the top of the screen carries everything that is true across every tab: where the
// operator is (six tabs), how the boiler wall stands right now (three summary pills over all 108
// Sensors), and what the host actually is (the runtime mode badge). Nothing in this file draws a value
// the Runtime did not publish, and nothing in it can be reached by a keyboard shortcut that a plant
// floor would hit by accident.
//
// The mode badge is assembled from the published liveness body and the published runtime status only.
// A previous draft of this screen carried a fixed label naming itself a prototype: a label like that is
// read as a statement about the machine, and it stays true after the machine changes. So the badge
// either reports what /api/health and /api/v1/runtime say, or it reports that it has not read them yet.

import { summaryPills } from './topology.mjs';

export const TABS = Object.freeze([
  { key: 1, id: 'main', label: 'Main Control', panel: 'main' },
  { key: 2, id: 'queue', label: 'Cleaning Queue', panel: 'queue' },
  { key: 3, id: 'wj', label: 'WJ & Pump', panel: 'wj' },
  { key: 4, id: 'sensor', label: 'Sensor & Path', panel: 'sensor' },
  { key: 5, id: 'alarms', label: 'Alarms', panel: 'alarms' },
  { key: 6, id: 'system', label: 'System & Trends', panel: 'system' },
]);

// The panels are addressed by their data attribute, not by an id: a duplicate id in a six-tab document
// is one careless edit away, and a data attribute cannot collide with itself.
export const TAB_PANEL_IDS = Object.freeze(TABS.map((tab) => tab.panel));

export function tabKeyOf(id) {
  for (const tab of TABS) {
    if (tab.key === Number(id)) return tab.id;
  }
  return null;
}

export const DEFAULT_TAB = 'main';

// The helper actions a wall screen may offer a commissioning operator. They are presentation-only by
// construction: every one of them re-reads, re-selects or reveals something the host already publishes,
// and none of them composes a request the read-only surface does not answer. They are also gated: they
// render only when the host reports a simulator profile, because "step the phase" means something
// different when a pump is attached.
export const DIAGNOSTICS_ACTIONS = Object.freeze([
  { id: 'repoll', label: 'Re-poll the full Snapshot now' },
  { id: 'focus-active', label: 'Move selection to the active target' },
  { id: 'step-next', label: 'Step selection forward' },
  { id: 'step-prev', label: 'Step selection back' },
  { id: 'modal-preview', label: 'Preview the Critical Alarm modal' },
  { id: 'frame-facts', label: 'Reveal the accepted frame identity' },
]);

export const SIMULATOR_PROFILES = Object.freeze(['SIMULATOR']);

export function isTabId(value) {
  for (const tab of TABS) {
    if (tab.id === value) return true;
  }
  return false;
}

export function tabLabel(id) {
  for (const tab of TABS) {
    if (tab.id === id) return tab.label;
  }
  return id;
}

// The tab the address asks for, read once at boot so a kiosk can be pointed straight at Alarms. An
// unknown fragment is not an error and not a guess: the screen opens Main Control and says what it saw.
export function tabFromHash(hash) {
  if (typeof hash !== 'string' || hash.length === 0) return { id: DEFAULT_TAB, note: '' };
  const value = hash.replace(/^#\/?/, '');
  const wanted = value.indexOf('tab=') === 0 ? value.slice(4) : value;
  if (isTabId(wanted)) return { id: wanted, note: '' };
  if (wanted === '') return { id: DEFAULT_TAB, note: '' };
  return { id: DEFAULT_TAB, note: 'unknown tab requested: ' + wanted };
}

export function modeBadge(health, status) {
  if (!health && !status) {
    return { label: 'HOST NOT READ YET', tone: 'warn', detail: 'no liveness answer and no runtime status yet', allowed: false };
  }
  const profile = health ? health.deviceProfile : status.profile;
  const acquisition = health ? health.acquisitionMode : 'NOT_REPORTED';
  const ready = health ? health.ready : status.ready;
  const stage = health ? health.stageMarker : status.stageMarker;
  if (SIMULATOR_PROFILES.includes(profile)) {
    return {
      label: 'SIMULATOR MODE - OFFLINE LOOPBACK',
      tone: 'info',
      detail: 'profile ' + String(profile) + ', acquisition ' + String(acquisition) + ', readiness ' + String(ready) + ', stage ' + String(stage),
      allowed: true,
    };
  }
  if (profile === 'PRODUCTION') {
    return {
      label: 'PRODUCTION PROFILE - NOT AUTHORIZED IN THIS BUILD',
      tone: 'bad',
      detail: 'the read-only Operator surface does not run against Production; stage ' + String(stage),
      allowed: false,
    };
  }
  return {
    label: 'LIVE TELEMETRY - PRODUCTION NOT AUTHORIZED',
    tone: 'warn',
    detail: 'profile ' + String(profile) + ', acquisition ' + String(acquisition) + ', readiness ' + String(ready) + ', stage ' + String(stage),
    allowed: false,
  };
}

export function diagnosticsBadge(badge) {
  return badge.allowed
    ? 'DIAGNOSTICS - SIMULATOR ONLY, PRESENTATION ONLY'
    : 'DIAGNOSTICS - UNAVAILABLE: THE HOST DOES NOT REPORT A SIMULATOR PROFILE';
}

export function pillCounts(walls) {
  return summaryPills(walls);
}

// The Alarms tab carries a count badge, because an alarm the operator has not seen is the only thing on
// this screen that is time-critical. The number is the published active-unacknowledged count, and an
// unavailable count is shown as such rather than as zero.
export function alarmBadge(alarms) {
  if (!alarms) return { text: 'no data', tone: 'mute', count: null };
  const count = typeof alarms.activeUnack === 'number' ? alarms.activeUnack : null;
  if (count === null) return { text: 'count unavailable', tone: 'mute', count: null };
  if (count === 0) return { text: '0', tone: 'ok', count: 0 };
  return { text: String(count), tone: count > 0 ? 'bad' : 'ok', count };
}

// ---------------------------------------------------------------- render (text and nodes, no markup)
// Every node below is built with createElement and written with textContent. No markup string is
// assembled anywhere in the presentation, so there is no path from a published payload to the parser.

function host(id) {
  return document.getElementById(id);
}

function setText(id, value) {
  const node = host(id);
  if (node) node.textContent = value;
  return node;
}

function setTone(id, tone) {
  const node = host(id);
  if (node) node.dataset.tone = tone;
}

function clear(node) {
  while (node.firstChild) node.removeChild(node.firstChild);
}

/**
 * The six tabs. An alarm count rides on the Alarms tab only, because that is the tab a count should
 * interrupt from; every other tab shows its name and its key, which is what makes the keyboard route
 * discoverable rather than folklore.
 */
export function renderTabStrip(activeId, alarmCount, note) {
  const strip = host('tab-nav');
  if (!strip) return 0;
  clear(strip);
  const hint = document.createElement('span');
  hint.className = 'tabnav-note';
  hint.textContent = note ? String(note) : 'tabs: keys 1 to 6';
  strip.appendChild(hint);
  for (const tab of TABS) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'nav-tab';
    button.dataset.tabTarget = tab.id;
    button.setAttribute('aria-current', tab.id === activeId ? 'page' : 'false');
    const key = document.createElement('span');
    key.className = 'nav-tab-key';
    key.textContent = String(tab.key);
    const label = document.createElement('span');
    label.className = 'tab-label';
    label.textContent = tab.label;
    button.appendChild(key);
    button.appendChild(label);
    if (tab.id === 'alarms' && alarmCount && alarmCount.count !== null) {
      const badge = document.createElement('span');
      badge.className = 'tab-badge';
      badge.dataset.tone = alarmCount.tone;
      badge.textContent = alarmCount.text;
      button.appendChild(badge);
    }
    strip.appendChild(button);
  }
  return TABS.length;
}

/**
 * The three summary pills, built from the published per-wall subtotals. Pill three is the published
 * uncertain count plus the published not-classified count, because the Classification enum has no
 * UNCERTAIN member and a third pill that silently dropped a fourth of the wall would not add up to 108.
 * The tooltip and the note under the bar carry the breakdown, and a disagreement with the protected
 * total is printed rather than reconciled.
 */
export function renderPills(walls) {
  const result = summaryPills(walls);
  const strip = host('summary-pills');
  if (strip) {
    clear(strip);
    for (const pill of result.pills) {
      const node = document.createElement('span');
      node.className = 'pill-count';
      node.dataset.pill = pill.key;
      const label = document.createElement('span');
      label.className = 'pill-label';
      label.textContent = pill.label;
      const value = document.createElement('b');
      value.className = 'pill-value';
      value.textContent = String(pill.value);
      node.appendChild(label);
      node.appendChild(value);
      node.title = pill.detail;
      strip.appendChild(node);
    }
    strip.dataset.count = String(result.pills.length);
    strip.setAttribute('title', result.state === 'mismatch'
      ? result.note
      : 'sum of the published per-wall subtotals; full breakdown in System and Trends: ' + result.breakdown);
    strip.dataset.note = result.state;
  }
  setText('pills-note', result.note);
  setTone('pills-note', result.state === 'mismatch' ? 'bad' : 'mute');
  return result;
}

export function renderModeBadge(badge) {
  setText('mode-badge', badge.label);
  setTone('mode-badge', badge.tone);
  setText('mode-note', badge.detail);
}

/**
 * The Diagnostics dropdown. The toggle is always present so its absence is never a mystery, and the
 * actions are rendered only when the host reports a simulator profile; otherwise the menu says which
 * profile it saw instead. Every action is presentation-only, and the sentence under the bar says so.
 */
export function renderDiagnostics(open, badge, frameFacts) {
  const root = host('diag-root');
  if (root) {
    root.dataset.open = open ? 'true' : 'false';
    root.dataset.allowed = badge.allowed ? 'true' : 'false';
  }
  const toggle = host('diag-badge');
  if (toggle) {
    toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    toggle.textContent = (open ? 'diagnostics (open): ' : 'diagnostics: ') + (badge.allowed ? 'simulator helpers' : 'unavailable');
  }
  setText('diag-note', diagnosticsBadge(badge));
  const menu = host('diag-menu');
  if (!menu) return;
  clear(menu);
  if (!badge.allowed) {
    const line = document.createElement('p');
    line.className = 'diag-denied';
    line.textContent = 'no helper action is offered: ' + badge.detail;
    menu.appendChild(line);
    return;
  }
  for (const action of DIAGNOSTICS_ACTIONS) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'diag-action';
    button.dataset.diagAction = action.id;
    button.textContent = action.label;
    menu.appendChild(button);
  }
  if (frameFacts) {
    const value = document.createElement('p');
    value.className = 'diag-facts';
    value.textContent = frameFacts;
    menu.appendChild(value);
  }
}

export function renderAlarmBadge(alarms) {
  const badge = alarmBadge(alarms);
  setText('alarm-count', badge.text);
  setTone('alarm-count', badge.tone);
  setText('alarm-text', badge.count === null
    ? 'no active unacknowledged count published'
    : badge.count === 0
      ? 'no active unacknowledged alarm'
      : 'active unacknowledged alarm(s) - see the Alarms tab');
  return badge;
}

/**
 * The feed line: what the screen is doing about its own connection. `phase` is the presentation's
 * state, not a host state, and it is worded that way - a screen that says LIVE while its last frame is
 * forty seconds old is the failure this line exists to prevent.
 */
export function renderFeedState(connection, revision, failures, health) {
  const phase = connection && connection.phase ? connection.phase : 'booting';
  const label = phase === 'live' ? 'LIVE - polling'
    : phase === 'stale' ? 'STALE - frame not current'
      : phase === 'offline' ? 'OFFLINE - host unavailable'
        : 'BOOTING - first frame';
  setText('live-state', label);
  setTone('live-state', phase === 'live' ? 'ok' : phase === 'booting' ? 'warn' : 'bad');
  setText('rev-state', 'rev: ' + (revision === null || revision === undefined ? '-' : String(revision))
    + (failures ? ', ' + String(failures) + ' failed attempt(s)' : ''));
  setTone('rev-state', failures ? 'warn' : 'mute');
  // The host the liveness body names for itself, printed as published: an origin the browser happens to
  // be sitting on is a fact about the frame, while this is the host's own statement of what it is.
  setText('host-state', 'host: ' + (health && health.host ? String(health.host) : 'NOT_READ')
    + (health && health.status ? ' (' + String(health.status) + ')' : ''));
  setTone('host-state', health ? (health.status === 'ALIVE' ? 'ok' : 'warn') : 'mute');
  return label;
}

/** The tab the bar is showing, read back from the buttons so a caller can confirm a switch painted. */
export function activeTabId() {
  const strip = host('tab-nav');
  if (!strip) return null;
  for (const node of Array.prototype.slice.call(strip.children)) {
    if (node.getAttribute && node.getAttribute('aria-current') === 'page') return node.dataset.tabTarget || null;
  }
  return null;
}

export function wireBarActions(onTab, onDiagnostics, onDiagnosticsAction) {
  const strip = host('tab-nav');
  if (strip) {
    strip.addEventListener('click', (event) => {
      const target = event.target;
      const button = target && target.closest ? target.closest('.nav-tab') : null;
      if (button && button.dataset.tabTarget) onTab(button.dataset.tabTarget);
    });
  }
  const toggle = host('diag-badge');
  if (toggle) toggle.addEventListener('click', () => onDiagnostics());
  const menu = host('diag-menu');
  if (menu) {
    menu.addEventListener('click', (event) => {
      const target = event.target;
      const button = target && target.closest ? target.closest('.diag-action') : null;
      if (button && button.dataset.diagAction) onDiagnosticsAction(button.dataset.diagAction);
    });
  }
}
