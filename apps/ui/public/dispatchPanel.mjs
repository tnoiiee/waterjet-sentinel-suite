// WJSS Operator presentation - the Cleaning Job Workspace panel.
//
// One atomic document from GET /api/v1/dispatch-workspace carries the GlobalQueue, the AutoSequence
// mode, the frozen next-dispatch countdown, the dispatch-cycle timeline and the live TimeDelay
// preview, all at one Runtime revision. The panel renders that document; it holds no queue, job,
// eligibility or delay state of its own and it sends nothing back. The countdown is displayed as
// frozen or not frozen because the contract freezes the applied delay per countdown: the screen
// never recomputes a delay, never extends a countdown and never reorders a queue entry.

import { text, number, timeOf } from './feed.mjs';
import { prepareCanvas } from './chartKit.mjs';

const NOT_PUBLISHED = 'NOT_PUBLISHED_BY_RUNTIME';

function fmt(value, empty) {
  return text(value, empty === undefined ? '--' : empty);
}

function fmtNum(value, digits, unit) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return '--';
  return number(value, digits, unit);
}

function fmtTime(value) {
  if (value === null || value === undefined || value === '') return '--';
  return timeOf(value);
}

function section(host, title, build, extraClass) {
  const block = document.createElement('div');
  block.className = extraClass ? 'sub ' + extraClass : 'sub';
  const head = document.createElement('p');
  head.className = 'sub-title';
  head.textContent = title;
  block.appendChild(head);
  build(block);
  host.appendChild(block);
  return block;
}

function facts(host, rows, compact) {
  const list = document.createElement('dl');
  list.className = compact ? 'facts facts-grid-2' : 'facts';
  for (const [label, value, tone] of rows) {
    const item = compact ? document.createElement('div') : null;
    if (item) item.className = 'fact-pair';
    const term = document.createElement('dt');
    term.textContent = label;
    const entry = document.createElement('dd');
    entry.textContent = value;
    if (tone) entry.dataset.tone = tone;
    if (item) {
      item.appendChild(term);
      item.appendChild(entry);
      list.appendChild(item);
    } else {
      list.appendChild(term);
      list.appendChild(entry);
    }
  }
  host.appendChild(list);
  return list;
}

function kpiMini(parent, label, value, sub, tone) {
  const tile = document.createElement('div');
  tile.className = 'kpi-tile';
  if (tone) tile.dataset.tone = tone;
  const l = document.createElement('span');
  l.className = 'kpi-tile-lbl';
  l.textContent = label;
  const v = document.createElement('span');
  v.className = 'kpi-tile-val num';
  v.textContent = value;
  if (tone) v.dataset.tone = tone;
  const s = document.createElement('span');
  s.className = 'kpi-tile-sub num';
  s.textContent = sub;
  tile.appendChild(l);
  tile.appendChild(v);
  tile.appendChild(s);
  parent.appendChild(tile);
}

function table(host, columns, rows, emptyText) {
  if (!rows.length) {
    const note = document.createElement('p');
    note.className = 'note';
    note.textContent = emptyText;
    host.appendChild(note);
    return;
  }
  const node = document.createElement('table');
  node.className = 'grid-table num';
  const head = document.createElement('thead');
  const headRow = document.createElement('tr');
  for (const column of columns) {
    const th = document.createElement('th');
    th.scope = 'col';
    th.textContent = column;
    headRow.appendChild(th);
  }
  head.appendChild(headRow);
  node.appendChild(head);
  const body = document.createElement('tbody');
  for (const row of rows) {
    const tr = document.createElement('tr');
    if (row.selected) tr.dataset.selected = 'true';
    for (const cellValue of row.cells) {
      const td = document.createElement('td');
      td.textContent = cellValue;
      tr.appendChild(td);
    }
    body.appendChild(tr);
  }
  node.appendChild(body);
  host.appendChild(node);
}

export function renderDispatch(host, workspace) {
  host.textContent = '';

  if (!workspace) {
    const note = document.createElement('p');
    note.className = 'note';
    note.dataset.tone = 'bad';
    note.textContent = 'No Cleaning Job Workspace document has been read yet. The panels below stay '
      + 'empty rather than showing a previous revision.';
    host.appendChild(note);
    return;
  }

  if (workspace.kind !== 'dispatch-workspace') {
    const note = document.createElement('p');
    note.className = 'note';
    note.dataset.tone = 'bad';
    note.textContent = 'WORKSPACE REFUSED: the document discriminator is ' + fmt(workspace.kind)
      + ', not the Cleaning Job Workspace the contract names.';
    host.appendChild(note);
    return;
  }

  const queue = workspace.queue || {};
  const entries = Array.isArray(queue.entries) ? queue.entries : [];
  const countdown = workspace.countdown || {};
  const preview = workspace.preview || {};
  const decision = countdown.decision && typeof countdown.decision === 'object' ? countdown.decision : null;
  const manual = String(preview.mode) === 'MANUAL_FIXED';

  // Top 4-tile summary strip for fast scanning
  const kpi = document.createElement('div');
  kpi.className = 'kpi-strip-4';
  kpiMini(kpi, 'COUNTDOWN',
    typeof countdown.remainingSeconds === 'number' ? String(countdown.remainingSeconds) + ' s' : 'IDLE',
    fmt(countdown.lifecycle, 'NONE') + (countdown.frozen ? ' (FROZEN)' : ''),
    String(countdown.lifecycle) === 'ACTIVE_FROZEN' ? 'warn' : 'mute');
  kpiMini(kpi, 'APPLIED DELAY',
    fmtNum(preview.appliedDelaySeconds, 0, 's'),
    fmt(preview.boilerCondition, 'NO CONDITION') + ' | x' + fmtNum(preview.zoneFactor, 2),
    preview.appliedDelaySeconds === null || preview.appliedDelaySeconds === undefined ? 'mute' : 'ok');
  kpiMini(kpi, 'DISPATCH MODE',
    fmt(workspace.mode, fmt(preview.mode)),
    'AutoSeq: ' + fmt(queue.autoSequence),
    countdown.dispatchAdmissible === true ? 'ok' : 'info');
  kpiMini(kpi, 'QUEUE STATUS',
    fmt(queue.totalQueued, '0') + ' / ' + fmt(queue.capacity, '0'),
    'Rev ' + fmt(queue.revision) + ' | ' + fmt(queue.label, 'Global'),
    entries.length > 0 ? 'info' : 'mute');
  host.appendChild(kpi);

  // Two-column compact cards: Countdown + TimeDelay Preview side by side
  const cardPair = document.createElement('div');
  cardPair.className = 'dispatch-cards-2';

  section(cardPair, 'Next-Dispatch Countdown', (block) => {
    facts(block, [
      ['Lifecycle', fmt(countdown.lifecycle, 'IDLE'), String(countdown.lifecycle) === 'ACTIVE_FROZEN' ? 'warn' : null],
      ['Generation', fmt(countdown.generation)],
      ['Remaining', fmtNum(countdown.remainingSeconds, 0, 's')],
      ['Frozen', countdown.frozen === true ? 'YES (locked)' : (countdown.frozen === false ? 'NO' : '--')],
      ['Admissible', countdown.dispatchAdmissible === true ? 'YES' : (countdown.dispatchAdmissible === false ? 'NO' : '--'),
        countdown.dispatchAdmissible === true ? 'ok' : 'mute'],
      ['Refusal', fmt(countdown.refusalCode, 'NONE'),
        countdown.refusalCode && countdown.refusalCode !== 'NONE' ? 'bad' : null],
      ['Superseded', fmt(countdown.supersededReason, fmt(countdown.lastSupersededReason, 'None'))],
      ['Timestamps', fmtTime(countdown.createdAt) + ' / ' + fmtTime(countdown.completedAt) + ' / ' + fmtTime(countdown.dispatchedAt)],
    ]);
  }, 'dispatch-subcard');

  section(cardPair, 'TimeDelay Preview - ' + fmt(preview.mode) + (manual ? ' (Manual)' : ' (Auto)'), (block) => {
    facts(block, [
      ['Available / Exec', fmt(preview.available) + ' / ' + fmt(preview.affectsExecution)],
      ['Refusal', fmt(preview.refusalCode, 'NONE'),
        preview.refusalCode && preview.refusalCode !== 'NONE' ? 'bad' : null],
      ['RH Input', fmtNum(preview.rhTemperature, 1, '%') + ' (' + fmt(preview.rhQuality) + ')', null],
      ['RH Observed / Gen', fmtTime(preview.rhObservedAt) + ' | gen ' + fmt(preview.rhGeneration)],
      ['Clamped RH', fmtNum(preview.clampedRhTemperature, 1, '%')],
      ['Mapped / Base', fmtNum(preview.mappedDelaySeconds, 0, 's') + ' / ' + fmtNum(preview.baseDelaySeconds, 0, 's')],
      ['Zone / Factor', fmt(preview.zone) + ' / ' + fmtNum(preview.zoneFactor, 2)],
      ['Applied / Boiler', fmtNum(preview.appliedDelaySeconds, 0, 's') + ' | ' + fmt(preview.boilerCondition),
        preview.appliedDelaySeconds === null || preview.appliedDelaySeconds === undefined ? 'mute' : 'ok'],
    ]);
  }, 'dispatch-subcard');

  host.appendChild(cardPair);

  // Last Dispatch card (compact 2-col)
  const last = queue.lastDispatch;
  if (last && typeof last === 'object') {
    section(host, 'Last Dispatch Record', (block) => {
      facts(block, [
        ['Dispatch ID', fmt(last.dispatchId)],
        ['Entry / Pos', fmt(last.queueEntryId) + ' (from pos ' + fmt(last.positionBefore) + ')'],
        ['Sensor / Reason', fmt(last.sensorId) + ' - ' + fmt(last.sourceReason)],
        ['Job / Origin', fmt(last.jobId) + ' (' + fmt(last.origin) + ')'],
        ['Queue Rev', fmt(last.queueRevisionBefore) + ' -> ' + fmt(last.queueRevisionAfter)],
        ['Dispatched At', fmtTime(last.dispatchedAt)],
      ], true);
    }, 'dispatch-subcard');
  }

  // Frozen Decision card
  if (decision) {
    section(host, 'Frozen Decision (Captured Once After Mandatory Safe Return)', (block) => {
      facts(block, [
        ['Decision / Gen', fmt(decision.decisionId) + ' | cd #' + fmt(decision.countdownGeneration) + ' / q #' + fmt(decision.queueGeneration)],
        ['Entry / Target', fmt(decision.queueEntryId) + ' -> ' + fmt(decision.nextSensorId) + ' (' + fmt(decision.nextWall) + ' #' + fmt(decision.nextSensorOrderWall) + ')'],
        ['Water Jet', fmt(decision.canonicalWaterJetId) + ' -> ' + fmt(decision.effectiveWaterJetId) + ' (' + fmt(decision.effectiveWaterJetProvenance) + ')'],
        ['Valve / Cabinet', fmt(decision.isolationValveId) + ' | ' + fmt(decision.cabinetId) + ' / ' + fmt(decision.controllerId)],
        ['Axes', fmt(decision.horizontalAxisId) + ' / ' + fmt(decision.verticalAxisId)],
        ['Condition / Mode', fmt(decision.boilerCondition) + ' / ' + fmt(decision.mode) + ' (cfg ' + fmt(decision.configurationVersion) + ')'],
        ['Manual / Auto Range', fmtNum(decision.manualDelaySeconds, 0, 's') + ' | ' + fmtNum(decision.autoDelayMinSeconds, 0, 's') + '-' + fmtNum(decision.autoDelayMaxSeconds, 0, 's')],
        ['Frozen RH Input', decision.rhInput && typeof decision.rhInput === 'object'
          ? fmtNum(decision.rhInput.rhTemperature, 1, '%') + ' ' + fmt(decision.rhInput.quality)
            + ' @ ' + fmtTime(decision.rhInput.observedAt) + ' (gen ' + fmt(decision.rhInput.generation) + ')'
          : (String(decision.mode) === 'MANUAL_FIXED' ? 'Ignored (Manual)' : 'None published')],
        ['RH Range / Window', fmtNum(decision.rhTempMin, 1) + '-' + fmtNum(decision.rhTempMax, 1) + ' / ' + fmt(decision.rhInputWindowSeconds) + ' s'],
        ['Base / Zone / Applied', fmtNum(decision.baseDelaySeconds, 0, 's') + ' | ' + fmt(decision.zone) + ' x' + fmtNum(decision.zoneFactor, 2) + ' -> ' + fmtNum(decision.appliedDelaySeconds, 0, 's'), 'ok'],
        ['Decision Created', fmtTime(decision.createdAt)],
      ], true);
    }, 'dispatch-subcard');
  } else {
    section(host, 'Frozen Decision', (block) => {
      const note = document.createElement('p');
      note.className = 'note';
      note.dataset.tone = 'mute';
      note.textContent = 'Decision: ' + fmt(countdown.decision, 'NONE')
        + ' - no decision is frozen for this generation; applied delay is not fixed yet.';
      block.appendChild(note);
    }, 'dispatch-subcard');
  }

  // Dispatch-cycle timeline table
  const timeline = workspace.timeline || {};
  const steps = Array.isArray(timeline.steps) ? timeline.steps : [];
  section(host, 'Dispatch-Cycle Timeline - ' + String(steps.length) + ' Steps (Stage: ' + fmt(timeline.stage, 'IDLE') + ')', (block) => {
    table(block, ['#', 'Stage', 'Step', 'State'], steps.map((step) => ({
      selected: String(step.state) === 'CURRENT',
      cells: [fmt(step.ordinal), fmt(step.stage), fmt(step.label), fmt(step.state)],
    })), 'The host published no timeline steps.');
  }, 'dispatch-subcard');

  // Compact queue entries reference table when entries exist
  if (entries.length > 0) {
    section(host, 'Queued Entries Detail (' + String(entries.length) + ')', (block) => {
      table(block, ['#', 'Entry', 'Sensor', 'Reason', 'Score', 'Age'],
        entries.map((entry, index) => ({
          selected: index === 0,
          cells: [
            fmt(entry.position),
            fmt(entry.entryId),
            fmt(entry.sensorId),
            fmt(entry.sourceReason),
            fmtNum(entry.dirtyScore, 1),
            fmt(entry.secondsSinceLastClean) + ' s',
          ],
        })),
        'Queue empty.');
    }, 'dispatch-subcard');
  }
}

const VIEW_SCALES = Object.freeze([1, 0.75]);
let viewScale = 1;

export function viewScaleValue() {
  return viewScale;
}

export function setViewScale(value) {
  const wanted = Number(value);
  viewScale = VIEW_SCALES.indexOf(wanted) >= 0 ? wanted : 1;
  return viewScale;
}

export function renderQueueBoard(host, queue, workspace, options) {
  host.textContent = '';
  const withLane = !options || options.lane !== false;
  const compact = Boolean(options && options.compact);
  const onSelect = options && typeof options.onSelect === 'function' ? options.onSelect : null;
  if (!queue) {
    const note = document.createElement('p');
    note.className = 'note';
    note.dataset.tone = 'mute';
    note.textContent = 'no queue published in this frame, so no slot can be shown';
    host.appendChild(note);
    return;
  }
  const capacity = Number(queue.capacity) || 0;
  const entries = Array.isArray(queue.entries) ? queue.entries : [];

  if (compact) {
    if (entries.length === 0) {
      const note = document.createElement('p');
      note.className = 'note';
      note.dataset.tone = 'mute';
      note.textContent = 'Queue empty (0 / ' + String(capacity) + ' slots) - waiting for Runtime dispatch entries.';
      host.appendChild(note);
      return;
    }
    const tbl = document.createElement('table');
    tbl.className = 'mini-table num';
    const thead = document.createElement('thead');
    const hrow = document.createElement('tr');
    for (const col of ['#', 'SDS', 'Score', 'Reason']) {
      const th = document.createElement('th');
      th.scope = 'col';
      th.textContent = col;
      hrow.appendChild(th);
    }
    thead.appendChild(hrow);
    tbl.appendChild(thead);
    const tbody = document.createElement('tbody');
    for (const entry of entries) {
      if (!entry) continue;
      const tr = document.createElement('tr');
      if (onSelect && entry.sensorId) {
        tr.addEventListener('click', () => onSelect(entry.sensorId));
      }
      for (const val of [
        'Q' + fmt(entry.position),
        fmt(entry.sensorId),
        fmtNum(entry.dirtyScore, 1),
        fmt(entry.sourceReason),
      ]) {
        const td = document.createElement('td');
        td.textContent = val;
        tr.appendChild(td);
      }
      tbody.appendChild(tr);
    }
    tbl.appendChild(tbody);
    host.appendChild(tbl);
    return;
  }

  const grid = document.createElement('div');
  grid.className = 'queue-board';
  for (let position = 1; position <= capacity; position += 1) {
    let entry = null;
    for (const candidate of entries) {
      if (candidate && candidate.position === position) entry = candidate;
    }
    const card = document.createElement('div');
    card.className = entry ? 'queue-slot' : 'queue-slot queue-slot-empty';
    if (!entry) {
      const empty = document.createElement('span');
      empty.className = 'queue-slot-id';
      empty.textContent = 'Slot ' + String(position) + ' - EMPTY';
      card.appendChild(empty);
    } else {
      const id = document.createElement('span');
      id.className = 'queue-slot-id';
      id.textContent = String(position) + '. ' + fmt(entry.sensorId) + ' / ' + fmt(entry.entryId);
      card.appendChild(id);
      const score = document.createElement('span');
      score.className = 'queue-slot-score';
      score.textContent = fmtNum(entry.dirtyScore, 1) + ' | ' + fmt(entry.sourceReason)
        + ' | ' + fmt(entry.secondsSinceLastClean) + ' s since clean';
      card.appendChild(score);
      if (typeof entry.dirtyScore === 'number') card.dataset.tone = 'info';
    }
    grid.appendChild(card);
  }
  host.appendChild(grid);

  const totalQueued = Number(queue.totalQueued) || 0;
  const head = document.createElement('p');
  head.className = 'note';
  head.textContent = 'Queue: ' + String(entries.length) + ' of ' + String(capacity) + ' slots filled'
    + ' | total ' + String(totalQueued) + ' at rev ' + fmt(queue.revision)
    + ' (' + fmt(queue.label, 'Global') + ') | AutoSeq ' + fmt(queue.autoSequence)
    + (entries.length === totalQueued ? '' : ' | ENTRIES/TOTAL MISMATCH: ' + String(entries.length) + ' vs ' + String(totalQueued));
  host.appendChild(head);

  if (!withLane) return;
  section(host, 'Next-Dispatch Countdown Lane (View Scale, Never a Recomputation)', (block) => {
    const canvas = document.createElement('canvas');
    canvas.width = 620;
    canvas.height = 96;
    canvas.id = 'delay-curve';
    canvas.setAttribute('role', 'img');
    canvas.setAttribute('aria-label', 'Frozen countdown lane of the Cleaning Job Workspace at the selected view scale');
    block.appendChild(canvas);
    const caption = document.createElement('p');
    caption.className = 'note';
    caption.id = 'curve-caption';
    const buttons = document.createElement('div');
    buttons.className = 'speed';
    for (const scale of VIEW_SCALES) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'speed-btn';
      button.dataset.scale = String(scale);
      button.setAttribute('aria-pressed', viewScale === scale ? 'true' : 'false');
      button.textContent = scale.toFixed(2).replace(/0$/, '') + 'x';
      button.addEventListener('click', () => {
        setViewScale(scale);
        for (const sibling of Array.prototype.slice.call(buttons.children)) {
          sibling.setAttribute('aria-pressed', sibling.dataset.scale === String(scale) ? 'true' : 'false');
        }
        drawCountdownLane(canvas, caption, workspace);
      });
      buttons.appendChild(button);
    }
    block.appendChild(buttons);
    block.appendChild(caption);
    drawCountdownLane(canvas, caption, workspace);
  }, 'dispatch-subcard');
}

export function drawCountdownLane(canvas, caption, workspace) {
  const sized = prepareCanvas(canvas, 620, 96);
  if (!sized) {
    caption.textContent = 'countdown lane: canvas not available in this browser';
    return;
  }
  const { ctx, width, height } = sized;
  const countdown = workspace ? workspace.countdown : null;
  ctx.clearRect(0, 0, width, height);
  ctx.fillStyle = '#0d1014';
  ctx.fillRect(0, 0, width, height);

  const seconds = countdown && typeof countdown.remainingSeconds === 'number' ? countdown.remainingSeconds : null;
  const preview = workspace ? workspace.preview : null;
  const applied = preview && typeof preview.appliedDelaySeconds === 'number' ? preview.appliedDelaySeconds : null;
  const span = Math.max(1, applied === null ? 600 : applied);
  const padX = 18;
  const trackW = Math.max(60, width - padX * 2);
  const lane = height - 28;

  // Baseline axis
  ctx.strokeStyle = '#384250';
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(padX, lane);
  ctx.lineTo(width - padX, lane);
  ctx.stroke();

  const ticks = 5;
  ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
  ctx.textAlign = 'center';
  ctx.fillStyle = '#94a3b8';
  for (let index = 0; index <= ticks; index += 1) {
    const x = padX + (index / ticks) * trackW;
    const shown = Math.round((index / ticks) * span / viewScale);
    ctx.beginPath();
    ctx.moveTo(x, lane - 4);
    ctx.lineTo(x, lane + 4);
    ctx.stroke();
    ctx.fillText(String(shown) + ' s', x, lane + 15);
  }
  ctx.textAlign = 'left';

  if (seconds === null) {
    ctx.setLineDash([4, 3]);
    ctx.strokeStyle = 'rgba(148, 163, 184, 0.45)';
    ctx.beginPath();
    ctx.moveTo(padX, lane - 16);
    ctx.lineTo(width - padX, lane - 16);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.fillStyle = '#94a3b8';
    ctx.fillText('No active countdown seconds published', padX + 4, lane - 26);
    caption.textContent = 'Countdown lane: no remaining seconds published in this workspace - nothing is drawn forward.';
    return;
  }

  const ratio = Math.min(1, Math.max(0, seconds / span));
  const x = padX + ratio * trackW;
  ctx.fillStyle = 'rgba(251, 191, 36, 0.20)';
  ctx.fillRect(padX, lane - 24, Math.max(3, x - padX), 14);
  ctx.strokeStyle = '#fbbf24';
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(padX, lane - 17);
  ctx.lineTo(x, lane - 17);
  ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(x, lane - 28);
  ctx.lineTo(x, lane - 4);
  ctx.stroke();
  ctx.fillStyle = '#f1f5f9';
  ctx.fillText(String(seconds) + ' s remaining', Math.min(x + 6, width - 110), lane - 30);

  caption.textContent = 'Countdown lane: ' + String(seconds) + ' s remaining at ' + viewScale.toFixed(2).replace(/0$/, '')
    + 'x view scale (' + (countdown && countdown.frozen ? 'FROZEN' : 'unfrozen') + ', '
    + fmt(countdown ? countdown.lifecycle : null) + ', gen '
    + fmt(countdown && countdown.decision ? countdown.decision.countdownGeneration : null) + ')';
}

export function renderOutcome(host, snapshot) {
  host.textContent = '';
  const sequence = snapshot ? snapshot.sequence : null;
  const outcome = sequence ? sequence.lastJobOutcome : null;
  if (!outcome) {
    const note = document.createElement('p');
    note.className = 'outcome-absent';
    note.textContent = 'No completed-cycle record published: NO HISTORICAL CYCLE ARCHIVE - LAST OUTCOME ONLY';
    host.appendChild(note);
    return;
  }
  const line = document.createElement('p');
  line.className = 'outcome-line';
  line.dataset.tone = String(outcome.outcome) === 'COMPLETE' ? 'ok' : 'warn';
  line.textContent = 'Job ' + fmt(outcome.jobId) + ' on ' + fmt(outcome.targetSensorId)
    + ' | ' + fmt(outcome.outcome) + ' | Trigger: ' + fmt(outcome.trigger)
    + ' @ ' + fmt(outcome.phaseAtTrigger)
    + ' | Phases Complete: ' + (outcome.cleaningPhasesComplete ? 'YES' : 'NO');
  host.appendChild(line);

  facts(host, [
    ['Valve & Closure', fmt(outcome.valveId) + ' | cmd #' + fmt(outcome.valveCloseCommandSeq)
      + ' | closed #' + fmt(outcome.valveClosedConfirmedSeq, 'inferred')
      + ' (' + fmt(outcome.valveCloseResolution, NOT_PUBLISHED) + ')'],
    ['Diagnosis', fmt(outcome.valveDiagnosis, 'None')],
    ['Qualification', fmt(outcome.qualifiedCompletion, 'None')
      + (Array.isArray(outcome.qualifiedRemarks) && outcome.qualifiedRemarks.length ? ' - ' + outcome.qualifiedRemarks.join(' / ') : '')],
    ['Queue Rev / Entry', fmt(outcome.queueRevisionBefore) + ' -> ' + fmt(outcome.queueRevisionAfter)
      + ' | ' + fmt(outcome.queueEntryId) + ' / ' + fmt(outcome.dispatchId)],
    ['Axis Evidence', 'ret #' + fmt(outcome.axisReturnCommandSeq) + ' | standby #' + fmt(outcome.standbyConfirmedSeq)],
    ['Outcome / Release', '#' + fmt(outcome.outcomeSeq) + ' / #' + fmt(outcome.releaseSeq) + ' | AutoSeq ' + fmt(outcome.autoSequenceAtRelease)],
    ['Finalized', fmtTime(outcome.finalizedAt)],
  ], true);

  const events = Array.isArray(outcome.events) ? outcome.events : [];
  const list = document.createElement('p');
  list.className = 'note';
  list.textContent = 'Events (' + String(events.length) + '): '
    + (events.length ? events.map((event) => String(event.seq) + ':' + String(event.event)).join(', ') : 'none')
    + ' | NO HISTORICAL CYCLE ARCHIVE - LAST OUTCOME ONLY';
  host.appendChild(list);

  const faults = Array.isArray(outcome.equipmentFaults) ? outcome.equipmentFaults : [];
  const tail = document.createElement('p');
  tail.className = 'note';
  tail.dataset.tone = faults.length ? 'bad' : 'mute';
  tail.textContent = faults.length
    ? faults.map((fault) => fmt(fault.valveId) + ' ' + fmt(fault.diagnosis)).join(' | ')
    : 'No equipment fault recorded on that outcome.';
  host.appendChild(tail);
}
