// WJSS Operator presentation - boot, the tab router and the wiring of every panel.
//
// This module owns view state only: which Sensor is selected, which tab is open, which trend channels
// are enabled, how dense the cells are and whether the critical dialog is open. It owns no queue, no
// job, no eligibility and no device state, and it holds no authority to change any of them. The lifecycle
// is published on document.body.dataset.appState as 'booting', then 'ready' once an authoritative
// Snapshot has been rendered, then 'failed' when the frame can no longer be called current.
//
// Reconnect policy, stated once: every poll reads the whole Snapshot, so a recovery is always a
// fresh full Snapshot; nothing is replayed, and a frame that is not current is never shown without
// the banner that says so.
//
// The six tabs share one render pass over one frame. Nothing is fetched per tab and nothing is deferred
// until a tab is opened: a wall screen must not have a view that is quietly staler than another.

import { createFeed, text, clockOf, POLL_INTERVAL_MS, MAX_DELTA_ROWS, OFFLINE_AFTER_FAILURES } from './feed.mjs';
import { buildModel, renderWallMap, renderLegend, stepSelection } from './wallMap.mjs';
import { renderSensorDetail, renderSelectedCard, drawTrend } from './sensorDetail.mjs';
import { renderThermal, renderDfrStatement, dfrStatement } from './thermal.mjs';
import { renderDispatch, renderQueueBoard, renderOutcome } from './dispatchPanel.mjs';
import { renderJob, renderPump, renderAlarms, renderAlarmFilters, renderCritical, criticalAlert, renderChips } from './opsPanel.mjs';
import { DEFAULT_TAB, TABS, isTabId, modeBadge, renderAlarmBadge, renderDiagnostics, renderFeedState, renderModeBadge, renderPills, renderTabStrip, tabFromHash, tabKeyOf, wireBarActions } from './shellBar.mjs';
import { renderActiveCockpit, renderCameraPanel, renderChannelToggles, renderDelayBands, renderMaintenanceLock, renderPathTable, renderPumpCard, renderSensorMaster, renderSystemSummary, renderTrendExplorer, renderWallBreakdown, renderWorkspace, renderWsbGrid } from './tabs.mjs';

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
  tab: DEFAULT_TAB,
  tabNote: '',
  diagnosticsOpen: false,
  frameFacts: '',
  overlay: 'cleaning',
  // The explorer starts with every channel the host named enabled. The list is taken from the first
  // accepted frame rather than from a table in this file, so a channel the Runtime stops publishing
  // cannot stay lit on the screen, and one it adds is drawn the frame it appears.
  enabled: [],
  enabledFrom: null,
  filter: 'ALL',
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

/**
 * The tab switch, and the only thing a tab switch does: it marks the document and the bar. Every panel
 * is rendered on every frame regardless of which tab is showing, so no view can be stale and no hidden
 * panel can be quietly out of date when it is opened.
 */
function applyTab(id) {
  const wanted = isTabId(id) ? id : view.tab;
  view.tab = wanted;
  document.body.dataset.tab = wanted;
  for (const panel of Array.prototype.slice.call(document.querySelectorAll('[data-tab-panel]'))) {
    panel.dataset.active = panel.dataset.tabPanel === wanted ? 'true' : 'false';
  }
  return wanted;
}

function repaint(force) {
  render(view.lastState, force);
}

function wireKeys() {
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      closeCritical();
      view.diagnosticsOpen = false;
      repaint(true);
      return;
    }
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    const byDigit = tabKeyOf(event.key);
    if (byDigit) {
      event.preventDefault();
      applyTab(byDigit);
      repaint(true);
      return;
    }
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

  const badge = modeBadge(state.health, state.status);
  renderModeBadge(badge);
  renderTabStrip(view.tab, renderAlarmBadge(state.snapshot ? state.snapshot.alarms : null), view.tabNote);
  renderPills(state.snapshot ? state.snapshot.walls : null);
  renderFeedState(connection, state.revision, state.consecutiveFailures, state.health);
  renderDiagnostics(view.diagnosticsOpen, badge, view.frameFacts);

  const mode = host('mode-line');
  if (mode) {
    const status = state.status;
    const profile = status ? status.profile : (state.snapshot ? state.snapshot.deviceProfile : null);
    mode.textContent = 'tab: ' + view.tab + ' - mode: ' + text(profile, 'not read')
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

/**
 * The Diagnostics menu, and the reason each entry is allowed to exist. Every action here re-reads,
 * re-selects or reveals something already published; none of them composes a request the read-only
 * surface does not answer, and the whole menu renders only when the host reports a simulator profile.
 */
function wireBar(feed) {
  wireBarActions(
    (id) => {
      applyTab(id);
      repaint(true);
    },
    () => {
      view.diagnosticsOpen = !view.diagnosticsOpen;
      repaint(true);
    },
    (action) => {
      const state = view.lastState;
      const snapshot = state ? state.snapshot : null;
      if (action === 'repoll') {
        if (feed && feed.pollNow) feed.pollNow();
        return;
      }
      if (action === 'focus-active') {
        view.selectedId = snapshot && snapshot.activeJob && snapshot.activeJob.targetSensorId
          ? snapshot.activeJob.targetSensorId : view.selectedId;
        repaint(true);
        return;
      }
      if (action === 'step-next' || action === 'step-prev') {
        const ordered = view.model && view.model.ok ? view.model.ordered : [];
        const next = stepSelection(ordered, view.selectedId, action === 'step-next' ? 1 : -1);
        if (next) view.selectedId = next;
        repaint(true);
        return;
      }
      if (action === 'modal-preview') {
        const alert = criticalAlert(snapshot);
        openCritical(alert || { message: 'CRITICAL MODAL PREVIEW: no critical alarm is latched in the accepted frame, so this dialog shows a layout preview and no alarm fact.' });
        return;
      }
      if (action === 'frame-facts') {
        view.frameFacts = 'revision ' + text(snapshot ? snapshot.revision : state.revision, 'none')
          + ' generated ' + text(snapshot ? snapshot.generatedAt : null, '-')
          + ' / ' + String(snapshot && Array.isArray(snapshot.sensors) ? snapshot.sensors.length : 0) + ' Sensors'
          + ' / trend window ' + String(snapshot && snapshot.trend ? snapshot.trend.capacity : 0);
        repaint(true);
      }
    },
  );
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
  // The explorer's channel set is seeded from the published series names before anything is painted, so
  // the first frame already draws: a first paint that waits for a second tick would read as a blank
  // panel on a wall that is being looked at for the first time.
  if (snapshot.trend && Array.isArray(snapshot.trend.seriesNames)
    && view.enabledFrom !== String(snapshot.trend.seriesNames.join(' '))) {
    view.enabled = snapshot.trend.seriesNames.slice();
    view.enabledFrom = String(snapshot.trend.seriesNames.join(' '));
  }
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

  // Tab 1: the cockpit, the card and the camera follow the same frame as the map.
  const cockpit = host('active-cockpit');
  if (cockpit) {
    rebuild(cockpit, (node) => {
      renderJob(node, snapshot);
      renderActiveCockpit(node, snapshot, view, () => {
        if (snapshot.activeJob && snapshot.activeJob.targetSensorId) view.selectedId = snapshot.activeJob.targetSensorId;
        repaint(true);
      }, () => {
        view.overlay = view.overlay === 'cleaning' ? 'inspect' : 'cleaning';
        repaint(true);
      });
    });
  }
  const selectedCard = host('selected-card');
  if (selectedCard) rebuild(selectedCard, (node) => renderSelectedCard(node, snapshot, view.selectedId));
  const queueCompact = host('queue-compact');
  if (queueCompact) rebuild(queueCompact, (node) => renderQueueBoard(node, snapshot.queue, null, { lane: false }));
  const camera = host('camera-panel');
  if (camera) rebuild(camera, (node) => renderCameraPanel(node, snapshot));
  const canvas = host('trend-canvas');
  if (canvas) drawTrend(canvas, host('trend-caption'), host('trend-note'), snapshot, snapshot.pump);

  // Tab 2: the queue board and the single published outcome, beside the atomic workspace document.
  const board = host('queue-board');
  if (board) rebuild(board, (node) => renderQueueBoard(node, state.workspace ? state.workspace.queue : null, state.workspace));
  const outcome = host('outcome-host');
  if (outcome) rebuild(outcome, (node) => renderOutcome(node, snapshot));
  const dispatchRoot = host('dispatch-root');
  const dispatchKey = String(state.workspace ? state.workspace.revision : 'none') + '/' + String(view.selectedId);
  if (dispatchRoot && (force || dispatchKey !== view.dispatchKey)) {
    rebuild(dispatchRoot, (node) => renderDispatch(node, state.workspace));
    view.dispatchKey = dispatchKey;
  }

  // Tab 3: the canonical cabinet grid, the published Pump state and the control availability.
  const grid = host('wsb-grid');
  if (grid) rebuild(grid, (node) => renderWsbGrid(node, snapshot));
  const pumpCard = host('pump-card');
  if (pumpCard) rebuild(pumpCard, (node) => renderPumpCard(node, snapshot));
  const manual = host('manual-lock');
  if (manual) rebuild(manual, (node) => renderMaintenanceLock(node, snapshot));

  // Tab 4: master and detail, the square workspace frame and the published path stages.
  const master = host('sds-master');
  if (master) {
    rebuild(master, (node) => renderSensorMaster(node, snapshot, view.selectedId,
      snapshot.activeJob ? snapshot.activeJob.targetSensorId : null, select));
  }
  const detail = host('sensor-detail');
  if (detail) rebuild(detail, (node) => renderSensorDetail(node, snapshot, view.selectedId));
  const wsCanvas = host('ws-canvas');
  if (wsCanvas) {
    renderWorkspace(wsCanvas, host('ws-caption'), snapshot, view.selectedId,
      snapshot.activeJob ? snapshot.activeJob.targetSensorId : null);
  }
  const pathTable = host('path-table');
  if (pathTable) rebuild(pathTable, (node) => renderPathTable(node, state.workspace));
  const dfr = host('dfr-statement');
  if (dfr) renderDfrStatement(dfr, dfrStatement(snapshot, view.selectedId));
  const thermal = host('thermal-facts');
  if (thermal) rebuild(thermal, (node) => renderThermal(node, snapshot, view.selectedId));

  // Tab 5: the alarm list, the filter that only hides rows, and the latched critical event.
  const filters = host('alarm-filter');
  if (filters) {
    rebuild(filters, (node) => renderAlarmFilters(node, snapshot, (value) => {
      view.filter = value;
      repaint(true);
    }));
  }
  const alarmsRoot = host('alarms-root');
  if (alarmsRoot) rebuild(alarmsRoot, (node) => renderAlarms(node, snapshot, view.selectedId));
  const criticalHost = host('critical-host');
  if (criticalHost) rebuild(criticalHost, (node) => renderCritical(node, snapshot));
  const ack = host('ack-host');
  if (ack) rebuild(ack, (node) => {
    const line = document.createElement('p');
    line.className = 'ack-note';
    line.textContent = 'ACKNOWLEDGE IS NOT OFFERED HERE: the count on this screen is the host\'s own '
      + 'active-unacknowledged count, and the filter above changes only which rows this screen prints. '
      + 'A device acknowledgement is a Runtime action and this build publishes no route that would carry it.';
    node.appendChild(line);
    const counts = document.createElement('dl');
    counts.className = 'facts';
    const values = [
      ['active unacknowledged', text(snapshot.alarms ? snapshot.alarms.activeUnack : null)],
      ['active acknowledged', text(snapshot.alarms ? snapshot.alarms.activeAck : null)],
      ['cleared unacknowledged', text(snapshot.alarms ? snapshot.alarms.clearedUnack : null)],
      ['display filter', String(view.filter)],
    ];
    for (const row of values) {
      const term = document.createElement('dt');
      term.textContent = row[0];
      const value = document.createElement('dd');
      value.textContent = row[1];
      counts.appendChild(term);
      counts.appendChild(value);
    }
    node.appendChild(counts);
  });

  // Tab 6: the host's own evidence, the wall breakdown behind the pills, and the trend explorer.
  const systemHost = host('system-host');
  if (systemHost) rebuild(systemHost, (node) => renderSystemSummary(node, snapshot, state.status, state.health, connection(state)));
  const breakdown = host('wall-breakdown');
  if (breakdown) rebuild(breakdown, (node) => renderWallBreakdown(node, snapshot));
  const explorer = host('explorer-canvas');
  if (explorer) {
    renderTrendExplorer(explorer, host('explorer-caption'), host('explorer-note'), snapshot, view);
  }
  const channels = host('channel-list');
  if (channels) {
    rebuild(channels, (node) => renderChannelToggles(node, snapshot, view, (name) => {
      const index = view.enabled.indexOf(name);
      if (index >= 0 && view.enabled.length > 1) view.enabled.splice(index, 1);
      else if (index < 0) view.enabled.push(name);
      repaint(true);
    }));
  }
  const bands = host('delay-bands');
  if (bands) renderDelayBands(bands, host('delay-bands-caption'), snapshot, state.workspace);
  const opsRoot = host('ops-root');
  const opsKey = String(snapshot.revision) + '/' + String(view.selectedId);
  if (opsRoot && (force || opsKey !== view.opsKey)) {
    rebuild(opsRoot, (node) => renderPump(node, snapshot));
    view.opsKey = opsKey;
  }

  const feedBody = host('feed-body');
  if (feedBody) renderFeedTable(feedBody, state.feed);

  const alert = criticalAlert(snapshot);
  if (alert) openCritical(alert); else closeCritical();

  if (!model.ok) setAppState('failed');
  else setAppState('ready');
}

function connection(state) {
  return {
    label: state.phase === 'live' ? 'live' : state.phase,
    online: state.phase === 'live',
    failures: state.consecutiveFailures,
  };
}

function boot() {
  const start = tabFromHash(window.location.hash);
  view.tab = start.id;
  view.tabNote = start.note;
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
  applyTab(view.tab);
  wireBar(feed);
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
