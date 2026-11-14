// WJSS Operator presentation - the Unified Command Bar & Diagnostics Popover.
//
// One 48px top bar plus one 26px status strip carry everything that is true across every tab:
// where the operator is (six tabs), how the boiler wall stands right now (three summary pills over
// all 108 Sensors), and what the host actually is (the runtime mode badge). Nothing in this file
// draws a value the Runtime did not publish, and nothing in it can be reached by a keyboard
// shortcut that a plant floor would hit by accident.
//
// The mode badge is assembled from the published liveness body and the published runtime status
// only. The badge either reports what /api/health and /api/v1/runtime say, or it reports that it
// has not read them yet.

import { text } from './feed.mjs';
import { EXPECTED_TOTAL, WATER_JETS, summaryPills } from './topology.mjs';
import { partitionSensors } from './wallMap.mjs';

export const TABS = Object.freeze([
  { key: 1, id: 'main', label: 'Main Control', panel: 'main' },
  { key: 2, id: 'queue', label: 'Cleaning Queue', panel: 'queue' },
  { key: 3, id: 'wj', label: 'WJ & Pump', panel: 'wj' },
  { key: 4, id: 'sensor', label: 'Sensor & Path', panel: 'sensor' },
  { key: 5, id: 'alarms', label: 'Alarms', panel: 'alarms' },
  { key: 6, id: 'system', label: 'System & Trends', panel: 'system' },
]);

export const TAB_PANEL_IDS = Object.freeze(TABS.map((tab) => tab.panel));

export function tabKeyOf(id) {
  for (const tab of TABS) {
    if (tab.key === Number(id)) return tab.id;
  }
  return null;
}

export const DEFAULT_TAB = 'main';

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
  const profile = (health && health.deviceProfile !== undefined && health.deviceProfile !== null)
    ? health.deviceProfile : (status && status.profile !== undefined ? status.profile : null);
  const acquisition = (health && health.acquisitionMode !== undefined && health.acquisitionMode !== null)
    ? health.acquisitionMode : 'NOT_REPORTED';
  const ready = (health && health.ready !== undefined && health.ready !== null)
    ? health.ready : (status && status.ready !== undefined ? status.ready : null);
  const stage = (health && health.stageMarker !== undefined && health.stageMarker !== null)
    ? health.stageMarker : (status && status.stageMarker !== undefined ? status.stageMarker : null);
  const profileLabel = text(profile, 'NOT_REPORTED');
  const acqLabel = text(acquisition, 'NOT_REPORTED');
  const readyLabel = text(ready, 'NOT_REPORTED');
  const stageLabel = text(stage, 'NOT_REPORTED');
  if (SIMULATOR_PROFILES.includes(profile)) {
    return {
      label: 'SIMULATOR MODE - OFFLINE LOOPBACK',
      tone: 'info',
      detail: 'profile ' + profileLabel + ', acquisition ' + acqLabel + ', readiness ' + readyLabel + ', stage ' + stageLabel,
      allowed: true,
    };
  }
  if (profile === 'PRODUCTION') {
    return {
      label: 'PRODUCTION PROFILE - NOT AUTHORIZED IN THIS BUILD',
      tone: 'bad',
      detail: 'the read-only Operator surface does not run against Production; stage ' + stageLabel,
      allowed: false,
    };
  }
  return {
    label: 'LIVE TELEMETRY - PRODUCTION NOT AUTHORIZED',
    tone: 'warn',
    detail: 'profile ' + profileLabel + ', acquisition ' + acqLabel + ', readiness ' + readyLabel + ', stage ' + stageLabel,
    allowed: false,
  };
}

export function diagnosticsBadge(badge) {
  return badge.allowed
    ? 'DIAGNOSTICS - SIMULATOR ONLY, PRESENTATION ONLY'
    : 'DIAGNOSTICS - UNAVAILABLE: THE HOST DOES NOT REPORT A SIMULATOR PROFILE';
}

export function pillCounts(walls, sensors) {
  if (Array.isArray(sensors) && sensors.length > 0) {
    return partitionSensors(sensors, walls);
  }
  return summaryPills(walls);
}

export function alarmBadge(alarms) {
  if (!alarms) return { text: 'no data', tone: 'mute', count: null };
  const count = typeof alarms.activeUnack === 'number' ? alarms.activeUnack : null;
  if (count === null) return { text: 'count unavailable', tone: 'mute', count: null };
  if (count === 0) return { text: '0', tone: 'ok', count: 0 };
  return { text: String(count), tone: count > 0 ? 'bad' : 'ok', count };
}

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

export function renderTabStrip(activeId, alarmCount, note, queue) {
  const strip = host('tab-nav');
  if (!strip) return 0;
  clear(strip);
  strip.title = note ? String(note) : 'Switch tabs with keys 1 to 6';
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

    if (tab.id === 'queue' && queue && typeof queue.totalQueued === 'number') {
      const qBadge = document.createElement('span');
      qBadge.className = 'nav-badge num';
      qBadge.textContent = String(queue.totalQueued) + '/' + String(queue.capacity || 0);
      button.appendChild(qBadge);
    } else if (tab.id === 'wj') {
      const wBadge = document.createElement('span');
      wBadge.className = 'nav-badge num';
      wBadge.textContent = String(WATER_JETS.length);
      button.appendChild(wBadge);
    } else if (tab.id === 'sensor') {
      const sBadge = document.createElement('span');
      sBadge.className = 'nav-badge num';
      sBadge.textContent = String(EXPECTED_TOTAL);
      button.appendChild(sBadge);
    } else if (tab.id === 'alarms' && alarmCount && alarmCount.count !== null) {
      const badge = document.createElement('span');
      badge.className = 'tab-badge num';
      badge.dataset.tone = alarmCount.tone;
      badge.textContent = alarmCount.text;
      button.appendChild(badge);
    }
    strip.appendChild(button);
  }
  return TABS.length;
}

export function renderPills(walls, sensors) {
  const result = pillCounts(walls, sensors);
  const strip = host('summary-pills');
  if (strip) {
    clear(strip);
    if (result.pills.length === 0) {
      const wait = document.createElement('span');
      wait.className = 'pill-count';
      wait.dataset.pill = 'uncertain';
      wait.textContent = '108 SDS: WAITING';
      strip.appendChild(wait);
    } else {
      for (const pill of result.pills) {
        const node = document.createElement('span');
        node.className = 'pill-count num';
        node.dataset.pill = pill.key;
        const label = document.createElement('span');
        label.className = 'pill-label';
        label.textContent = pill.key === 'uncertain' ? 'UNC:' : (pill.label.toUpperCase() + ':');
        const value = document.createElement('b');
        value.className = 'pill-value';
        value.textContent = String(pill.value);
        node.appendChild(label);
        node.appendChild(value);
        strip.appendChild(node);
      }
    }
    strip.dataset.count = String(result.pills.length);
    const detail = result.rawBreakdown || result.breakdown || '';
    strip.setAttribute('title', result.state === 'mismatch'
      ? result.note
      : 'Mutually exclusive 108-SDS tile states (sum = ' + String(result.total) + '); ' + detail);
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

export function renderDiagnostics(open, badge, frameFacts) {
  const root = host('diag-root');
  if (root) {
    root.dataset.open = open ? 'true' : 'false';
    root.dataset.allowed = badge.allowed ? 'true' : 'false';
  }
  const popover = host('diag-popover');
  if (popover) {
    popover.dataset.open = open ? 'true' : 'false';
  }
  const toggle = host('diag-badge');
  if (toggle) {
    toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    toggle.textContent = open ? 'Diagnostics ^' : 'Diagnostics v';
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
  const jump = host('alarm-jump');
  if (jump) jump.dataset.tone = badge.tone;
  setText('alarm-text', badge.count === null
    ? 'no active unacknowledged count published'
    : badge.count === 0
      ? 'ALL ALARMS CLEAR - 0 UNACKNOWLEDGED'
      : 'UNACK ALARM(S) - CLICK TO INSPECT ALARMS TAB');
  return badge;
}

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
  setText('host-state', 'host: ' + (health && health.host ? String(health.host) : 'NOT_READ')
    + (health && health.status ? ' (' + String(health.status) + ')' : ''));
  setTone('host-state', health ? (health.status === 'ALIVE' ? 'ok' : 'warn') : 'mute');
  return label;
}

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
  const close = host('diag-close');
  if (close) close.addEventListener('click', () => onDiagnostics(false));
  const jump = host('alarm-jump');
  if (jump) jump.addEventListener('click', () => onTab('alarms'));
  const gotoQueue = host('btn-goto-queue');
  if (gotoQueue) gotoQueue.addEventListener('click', () => onTab('queue'));
  const menu = host('diag-menu');
  if (menu) {
    menu.addEventListener('click', (event) => {
      const target = event.target;
      const button = target && target.closest ? target.closest('.diag-action') : null;
      if (button && button.dataset.diagAction) onDiagnosticsAction(button.dataset.diagAction);
    });
  }
}
