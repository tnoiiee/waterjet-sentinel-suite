// WJSS Operator presentation - boot and wiring.
//
// This module owns view state only: which Sensor is selected, how dense the cells are and whether
// the critical dialog is open. It owns no queue, no job, no eligibility and no device state, and
// it holds no authority to change any of them. The lifecycle is published on
// document.body.dataset.appState as 'booting', then 'ready' once an authoritative Snapshot has
// been rendered, then 'failed' when the frame can no longer be called current.
//
// Reconnect policy, stated once: every poll reads the whole Snapshot, so a recovery is always a
// fresh full Snapshot; nothing is replayed, and a frame that is not current is never shown without
// the banner that says so.

import { createFeed, text, clockOf, POLL_INTERVAL_MS, MAX_DELTA_ROWS, OFFLINE_AFTER_FAILURES } from './feed.mjs';
import { buildModel, renderWallMap, renderLegend, stepSelection } from './wallMap.mjs';
import { renderSensorDetail, drawTrend } from './sensorDetail.mjs';
import { renderThermal, renderDfrStatement, dfrStatement } from './thermal.mjs';
import { renderDispatch } from './dispatchPanel.mjs';
import { renderJob, renderPump, renderAlarms, criticalAlert, renderChips } from './opsPanel.mjs';

const DENSITY_KEY = 'wjss.operator.density';
const densities = ['comfortable', 'compact'];

const view = {
  selectedId: null,
  model: null,
  dismissed: new Set(),
  lastState: null,
  paintedRegions: new Set(),
  dispatchKey: null,
  opsKey: null,
};

function host(id) {
  return document.getElementById(id);
}

function setAppState(state) {
  document.body.dataset.appState = state;
  const line = host('state-line');
  if (line) line.textContent = 'app state: ' + state;
}

function showBootError(detail) {
  const box = host('boot-error');
  const line = host('boot-error-detail');
  if (line) line.textContent = detail;
  if (box) box.hidden = false;
  setAppState('failed');
}

function applyDensity(name) {
  const value = densities.indexOf(name) >= 0 ? name : 'comfortable';
  document.body.dataset.density = value;
  for (const button of Array.prototype.slice.call(document.querySelectorAll('[data-density-set]'))) {
    button.setAttribute('aria-pressed', button.dataset.densitySet === value ? 'true' : 'false');
  }
  try {
    window.localStorage.setItem(DENSITY_KEY, value);
  } catch {
    // A locked-down kiosk profile may refuse storage. Density is presentation only, so a refusal
    // is not a fault to report: the default simply stands.
  }
}

function wireDensity() {
  for (const button of Array.prototype.slice.call(document.querySelectorAll('[data-density-set]'))) {
    button.addEventListener('click', () => applyDensity(button.dataset.densitySet));
  }
  let stored = null;
  try {
    stored = window.localStorage.getItem(DENSITY_KEY);
  } catch {
    stored = null;
  }
  applyDensity(stored || document.body.dataset.density || 'comfortable');
}

function repaint(force) {
  render(view.lastState, force);
}

function wireKeys() {
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      closeCritical();
      return;
    }
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    const delta = { ArrowRight: 1, ArrowLeft: -1, ArrowDown: 1, ArrowUp: -1 }[event.key];
    if (!delta) return;
    const ordered = view.model && view.model.ok ? view.model.ordered : [];
    if (ordered.length === 0) return;
    const next = stepSelection(ordered, view.selectedId, delta);
    if (!next) return;
    event.preventDefault();
    view.selectedId = next;
    repaint(true);
  });
}

function closeCritical() {
  const modal = host('alarm-modal');
  if (modal) modal.hidden = true;
}

function dismissCritical() {
  const body = host('alarm-modal-body');
  if (body) view.dismissed.add(body.textContent);
  closeCritical();
}

function openCritical(alert) {
  const modal = host('alarm-modal');
  const body = host('alarm-modal-body');
  if (!modal || !body) return;
  if (view.dismissed.has(alert.message)) return;
  body.textContent = alert.message;
  modal.hidden = false;
}

function wireCritical() {
  const close = host('alarm-modal-close');
  if (close) close.addEventListener('click', dismissCritical);
  const modal = host('alarm-modal');
  if (modal) {
    modal.addEventListener('click', (event) => {
      if (event.target === modal) dismissCritical();
    });
  }
}

/** Keeps the internal scroll of a rebuilt panel, so a steady one-second cadence cannot fight it. */
function rebuild(node, paint) {
  const top = node.scrollTop;
  node.textContent = '';
  paint(node);
  node.scrollTop = top;
}

function renderBanners(state) {
  const stale = host('banner-stale');
  if (stale) {
    if (state.phase === 'live') {
      stale.hidden = true;
      stale.textContent = '';
    } else {
      stale.hidden = false;
      stale.textContent = (state.phase === 'offline' ? 'HOST UNAVAILABLE - RECONNECTING' : 'FRAME IS NOT CURRENT - RETRYING')
        + ': ' + String(state.consecutiveFailures) + ' failed attempt'
        + (state.consecutiveFailures === 1 ? '' : 's')
        + '. The frame shown is from revision ' + text(state.revision, 'none') + ' at '
        + clockOf(state.lastGoodAt) + '. A recovery always starts with a fresh full Snapshot.';
    }
  }

  const fault = host('banner-fault');
  if (fault) {
    const problems = [];
    if (state.resyncs > 0) {
      problems.push(String(state.resyncs) + ' revision discontinuit'
        + (state.resyncs === 1 ? 'y' : 'ies') + ' observed and re-snapshotted');
    }
    const items = state.feed && Array.isArray(state.feed.items) ? state.feed.items : [];
    if (items.some((item) => item && item.hasRevisionGap === true)) {
      problems.push('the bounded Delta feed reports a provable gap inside its own retained window');
    }
    if (state.snapshotError) problems.push('last refusal: ' + state.snapshotError);
    if (problems.length === 0) {
      fault.hidden = true;
      fault.textContent = '';
    } else {
      fault.hidden = false;
      fault.textContent = 'CONTINUITY: ' + problems.join(' - ') + '.';
    }
  }
}

function renderHeader(state) {
  const chips = host('chips');
  const connection = {
    phase: state.phase,
    failures: state.consecutiveFailures,
    resyncs: state.resyncs,
  };
  if (chips) renderChips(chips, state.snapshot, state.status, state.health, connection);

  const clock = host('server-clock');
  if (clock) clock.textContent = clockOf(state.lastServerTime);

  const mode = host('mode-line');
  if (mode) {
    const status = state.status;
    const profile = status ? status.profile : (state.snapshot ? state.snapshot.deviceProfile : null);
    mode.textContent = 'mode: ' + text(profile, 'not read')
      + ' / acquisition ' + text(state.health ? state.health.acquisitionMode : null, 'not read')
      + ' / stage ' + text(state.snapshot && state.snapshot.runtime ? state.snapshot.runtime.stageMarker : null);
  }

  const hostLine = host('host-line');
  if (hostLine) {
    hostLine.textContent = 'host: same origin ' + String(window.location.origin)
      + ' - poll ' + String(POLL_INTERVAL_MS) + ' ms, feed rows capped at ' + String(MAX_DELTA_ROWS)
      + ', read-only';
  }
}

function renderFeedTable(body, feed) {
  body.textContent = '';
  const items = feed && Array.isArray(feed.items) ? feed.items.slice(0, MAX_DELTA_ROWS) : [];
  if (items.length === 0) {
    const row = document.createElement('tr');
    row.className = 'row-empty';
    const cell = document.createElement('td');
    cell.colSpan = 6;
    cell.textContent = 'No Delta retained yet: the host has committed no revision into the bounded window.';
    row.appendChild(cell);
    body.appendChild(row);
    return;
  }
  for (const item of items) {
    const row = document.createElement('tr');
    if (item.hasRevisionGap === true) row.dataset.alarm = 'critical';
    const cells = [
      text(item.revision),
      text(item.previousRevision),
      clockOf(item.generatedAt),
      text(item.changedSensorCount),
      Array.isArray(item.changedSections) && item.changedSections.length > 0
        ? item.changedSections.join(' ') : 'none',
      item.hasRevisionGap === true ? 'GAP' : (item.appliesCleanly === true ? 'clean' : 'unproven'),
    ];
    for (const value of cells) {
      const cell = document.createElement('td');
      cell.textContent = value;
      row.appendChild(cell);
    }
    body.appendChild(row);
  }
}

function render(state, force) {
  if (!state) return;
  view.lastState = state;
  const snapshot = state.snapshot;

  renderHeader(state);
  renderBanners(state);

  if (!snapshot) {
    if (state.phase === 'booting' && state.consecutiveFailures >= OFFLINE_AFTER_FAILURES) setAppState('failed');
    return;
  }

  const model = buildModel(snapshot);
  view.model = model;
  if (!view.selectedId) {
    const active = snapshot.activeJob && snapshot.activeJob.targetSensorId ? snapshot.activeJob.targetSensorId : null;
    view.selectedId = active || (model.ok && model.ordered.length > 0 ? model.ordered[0] : null);
  }

  const select = (id) => {
    view.selectedId = id;
    repaint(true);
  };
  const mapHost = host('wallmap');
  if (mapHost) {
    const top = mapHost.scrollTop;
    renderWallMap(mapHost, model, snapshot, view.selectedId, select);
    mapHost.scrollTop = top;
  }
  const legend = host('legend');
  if (legend && !view.paintedRegions.has('legend')) {
    renderLegend(legend);
    view.paintedRegions.add('legend');
  }

  const detail = host('sensor-detail');
  if (detail) rebuild(detail, (node) => renderSensorDetail(node, snapshot, view.selectedId));
  const canvas = host('trend-canvas');
  if (canvas) drawTrend(canvas, host('trend-caption'), host('trend-note'), snapshot, snapshot.pump);

  const dfr = host('dfr-statement');
  if (dfr) renderDfrStatement(dfr, dfrStatement(snapshot, view.selectedId));
  const thermal = host('thermal-facts');
  if (thermal) rebuild(thermal, (node) => renderThermal(node, snapshot, view.selectedId));

  const dispatchRoot = host('dispatch-root');
  const dispatchKey = String(state.workspace ? state.workspace.revision : 'none') + '/' + String(view.selectedId);
  if (dispatchRoot && (force || dispatchKey !== view.dispatchKey)) {
    rebuild(dispatchRoot, (node) => renderDispatch(node, state.workspace));
    view.dispatchKey = dispatchKey;
  }

  const opsRoot = host('ops-root');
  const opsKey = String(snapshot.revision) + '/' + String(view.selectedId);
  if (opsRoot && (force || opsKey !== view.opsKey)) {
    rebuild(opsRoot, (node) => {
      renderJob(node, snapshot);
      renderPump(node, snapshot);
      renderAlarms(node, snapshot, view.selectedId);
    });
    view.opsKey = opsKey;
  }

  const feedBody = host('feed-body');
  if (feedBody) renderFeedTable(feedBody, state.feed);

  const alert = criticalAlert(snapshot);
  if (alert) openCritical(alert); else closeCritical();

  if (!model.ok) setAppState('failed');
  else setAppState('ready');
}

function boot() {
  const feed = createFeed({
    onFrame: (state) => render(state, false),
    onFault: (error) => {
      if (!error || error.code === 'ABORTED') return;
      if (document.body.dataset.appState !== 'booting') return;
      const box = host('boot-error');
      const line = host('boot-error-detail');
      if (line) line.textContent = String(error.code) + ' - ' + String(error.detail);
      if (box) box.hidden = false;
    },
  });
  feed.start();
  window.addEventListener('beforeunload', () => feed.stop());
}

try {
  wireDensity();
  wireCritical();
  wireKeys();
  const box = host('boot-error');
  if (box) box.hidden = true;
  boot();
} catch (error) {
  showBootError(error && error.message ? String(error.message) : 'the presentation module threw during boot');
}
