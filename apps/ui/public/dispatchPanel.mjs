// WJSS Operator presentation - the Cleaning Job Workspace panel.
//
// One atomic document from GET /api/v1/dispatch-workspace carries the GlobalQueue, the AutoSequence
// mode, the frozen next-dispatch countdown, the dispatch-cycle timeline and the live TimeDelay
// preview, all at one Runtime revision. The panel renders that document; it holds no queue, job,
// eligibility or delay state of its own and it sends nothing back. The countdown is displayed as
// frozen or not frozen because the contract freezes the applied delay per countdown: the screen
// never recomputes a delay, never extends a countdown and never reorders a queue entry.

import { text, number, timeOf } from './feed.mjs';

const NOT_PUBLISHED = 'NOT_PUBLISHED_BY_RUNTIME';

function section(host, title, build) {
  const block = document.createElement('div');
  block.className = 'sub';
  const head = document.createElement('p');
  head.className = 'sub-title';
  head.textContent = title;
  block.appendChild(head);
  build(block);
  host.appendChild(block);
  return block;
}

function facts(host, rows) {
  const list = document.createElement('dl');
  list.className = 'facts';
  for (const [label, value, tone] of rows) {
    const term = document.createElement('dt');
    term.textContent = label;
    const entry = document.createElement('dd');
    entry.textContent = value;
    if (tone) entry.dataset.tone = tone;
    list.appendChild(term);
    list.appendChild(entry);
  }
  host.appendChild(list);
  return list;
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
  node.className = 'grid-table';
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
    note.textContent = 'WORKSPACE REFUSED: the document discriminator is ' + text(workspace.kind)
      + ', not the Cleaning Job Workspace the contract names.';
    host.appendChild(note);
    return;
  }

  const queue = workspace.queue || {};
  const entries = Array.isArray(queue.entries) ? queue.entries : [];
  section(host, 'Global queue - ' + text(queue.totalQueued) + ' of ' + text(queue.capacity) + ' at revision ' + text(queue.revision), (block) => {
    facts(block, [
      ['queue label', text(queue.label)],
      ['capacity / queued', text(queue.capacity) + ' / ' + text(queue.totalQueued)],
      ['auto sequence', text(queue.autoSequence)],
      ['mode', text(workspace.mode)],
      ['queue revision', text(queue.revision)],
    ]);
    table(block, ['pos', 'entry', 'sensor', 'why queued', 'score', 'since last clean'],
      entries.map((entry, index) => ({
        selected: index === 0,
        cells: [
          text(entry.position),
          text(entry.entryId),
          text(entry.sensorId),
          text(entry.sourceReason),
          entry.dirtyScore === null || entry.dirtyScore === undefined ? 'NOT_PROVIDED' : Number(entry.dirtyScore).toFixed(1),
          text(entry.secondsSinceLastClean) + ' s',
        ],
      })),
      'The queue is empty: no entry is waiting for a cleaning cycle.');
    const last = queue.lastDispatch;
    if (last && typeof last === 'object') {
      facts(block, [
        ['last dispatch', text(last.dispatchId)],
        ['entry / position', text(last.queueEntryId) + ' from position ' + text(last.positionBefore)],
        ['sensor / why', text(last.sensorId) + ' - ' + text(last.sourceReason)],
        ['job', text(last.jobId)],
        ['origin', text(last.origin)],
        ['queue revision before / after', text(last.queueRevisionBefore) + ' / ' + text(last.queueRevisionAfter)],
        ['dispatched at', timeOf(last.dispatchedAt)],
      ]);
    }
  });

  const countdown = workspace.countdown || {};
  const countdownRows = [
    ['lifecycle', text(countdown.lifecycle), String(countdown.lifecycle) === 'ACTIVE_FROZEN' ? 'warn' : null],
    ['generation', text(countdown.generation)],
    ['remaining', text(countdown.remainingSeconds) + ' s'],
    ['frozen', text(countdown.frozen) + ' - the applied delay cannot change mid-countdown'],
    ['dispatch admissible', text(countdown.dispatchAdmissible),
      countdown.dispatchAdmissible === true ? 'ok' : 'mute'],
    ['decision', text(countdown.decision)],
    ['refusal code', text(countdown.refusalCode),
      countdown.refusalCode && countdown.refusalCode !== 'NONE' ? 'bad' : null],
    ['superseded', text(countdown.supersededReason)],
    ['last superseded', text(countdown.lastSupersededReason)],
    ['created / completed / dispatched', timeOf(countdown.createdAt) + ' / ' + timeOf(countdown.completedAt)
      + ' / ' + timeOf(countdown.dispatchedAt)],
  ];
  section(host, 'Next-dispatch countdown', (block) => facts(block, countdownRows));

  const decision = countdown.decision;
  if (decision && typeof decision === 'object') {
    section(host, 'Frozen decision - captured once after Mandatory Safe Return', (block) => facts(block, [
      ['decision', text(decision.decisionId)],
      ['countdown generation', text(decision.countdownGeneration)],
      ['queue generation', text(decision.queueGeneration)],
      ['entry', text(decision.queueEntryId)],
      ['next sensor', text(decision.nextSensorId) + ' on ' + text(decision.nextWall)
        + ', order-in-wall ' + text(decision.nextSensorOrderWall)],
      ['canonical / effective jet', text(decision.canonicalWaterJetId) + ' / ' + text(decision.effectiveWaterJetId)],
      ['effective jet provenance', text(decision.effectiveWaterJetProvenance)],
      ['isolation valve', text(decision.isolationValveId)],
      ['cabinet / controller', text(decision.cabinetId) + ' / ' + text(decision.controllerId)],
      ['axes', text(decision.horizontalAxisId) + ' / ' + text(decision.verticalAxisId)],
      ['boiler condition / mode', text(decision.boilerCondition) + ' / ' + text(decision.mode)],
      ['configuration version', text(decision.configurationVersion)],
      ['manual delay', number(decision.manualDelaySeconds, 0, 's')],
      ['frozen RH input', decision.rhInput && typeof decision.rhInput === 'object'
        ? number(decision.rhInput.rhTemperature, 1, '%') + ' / ' + text(decision.rhInput.quality)
          + ' at ' + timeOf(decision.rhInput.observedAt) + ', generation ' + text(decision.rhInput.generation)
        : 'none - ' + (String(decision.mode) === 'MANUAL_FIXED'
            ? 'Manual mode ignores the RH input by contract'
            : 'the host published no frozen input')],
      ['RH range / window', number(decision.rhTempMin, 1) + ' to ' + number(decision.rhTempMax, 1)
        + ' over ' + text(decision.rhInputWindowSeconds) + ' s'],
      ['auto delay range', number(decision.autoDelayMinSeconds, 0, 's') + ' to '
        + number(decision.autoDelayMaxSeconds, 0, 's')],
      ['base delay / zone / factor', number(decision.baseDelaySeconds, 0, 's') + ' / '
        + text(decision.zone) + ' / ' + number(decision.zoneFactor, 2)],
      ['applied delay (frozen)', number(decision.appliedDelaySeconds, 0, 's'), 'ok'],
      ['decision created', timeOf(decision.createdAt)],
    ]));
  } else {
    section(host, 'Frozen decision', (block) => facts(block, [
      ['decision', text(countdown.decision === null || countdown.decision === undefined
        ? 'NONE' : countdown.decision), 'mute'],
      ['meaning', 'no decision is frozen for this generation: the applied delay is not fixed yet'],
    ]));
  }

  const timeline = workspace.timeline || {};
  const steps = Array.isArray(timeline.steps) ? timeline.steps : [];
  section(host, 'Dispatch-cycle timeline - ' + String(steps.length) + ' steps, current stage '
    + text(timeline.stage), (block) => {
    table(block, ['#', 'stage', 'step', 'state'], steps.map((step) => ({
      selected: String(step.state) === 'CURRENT',
      cells: [text(step.ordinal), text(step.stage), text(step.label), text(step.state)],
    })), 'The host published no timeline steps.');
    const note = document.createElement('p');
    note.className = 'note';
    note.textContent = 'Every step is listed in contract order and none is hidden: a pending step '
      + 'reads PENDING, a failed step reads FAILED, and the screen adds no progress of its own.';
    block.appendChild(note);
  });

  const preview = workspace.preview || {};
  const manual = String(preview.mode) === 'MANUAL_FIXED';
  section(host, 'TimeDelay preview - ' + text(preview.mode) + (manual ? ' (Manual fixed)' : ' (Auto mapped)'), (block) => {
    facts(block, [
      ['available', text(preview.available)],
      ['affects execution', text(preview.affectsExecution)],
      ['refusal code', text(preview.refusalCode),
        preview.refusalCode && preview.refusalCode !== 'NONE' ? 'bad' : null],
      ['RH input', number(preview.rhTemperature, 1, '%') + ' / ' + text(preview.rhQuality)
        + ' at ' + timeOf(preview.rhObservedAt)],
      ['RH generation', text(preview.rhGeneration)],
      ['clamped RH', number(preview.clampedRhTemperature, 1, '%')],
      ['mapped delay', number(preview.mappedDelaySeconds, 0, 's')],
      ['base delay', number(preview.baseDelaySeconds, 0, 's')],
      ['zone / factor', text(preview.zone) + ' / ' + number(preview.zoneFactor, 2)],
      ['applied delay', number(preview.appliedDelaySeconds, 0, 's'),
        preview.appliedDelaySeconds === null || preview.appliedDelaySeconds === undefined ? 'mute' : 'ok'],
      ['boiler condition', text(preview.boilerCondition)],
    ]);
    const note = document.createElement('p');
    note.className = 'note';
    note.textContent = 'Preview only. While a countdown is ACTIVE_FROZEN the applied delay shown here '
      + 'is the frozen value, and a live preview that disagrees with it is reported as a preview, '
      + 'never as a change to the running countdown.';
    block.appendChild(note);
  });
}

// A view scale of the countdown lane, in per cent of real time. It is a reading aid only: the frozen
// seconds the host published are the same seconds at 1.0x and at 0.75x, and the axis label says so,
// because a control that quietly changed a displayed countdown would be a control over the sequence.
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

/**
 * The queue as slots rather than as a table: one box per position up to the published capacity, filled
 * from the published entries, and a muted empty-slot box for every position the host did not fill. A
 * blank box on a wall screen reads like a hidden entry, so the emptiness is written out instead.
 */
/**
 * One queue, rendered at two sizes. Tab 2 gets the whole board with the countdown lane; Tab 1 gets the
 * same slots beside the selected Sensor, from the Snapshot's own QueueSummary - the same published fields,
 * because two views of a queue that disagree about what is queued would be a worse defect than a scroll.
 */
export function renderQueueBoard(host, queue, workspace, options) {
  host.textContent = '';
  const withLane = !options || options.lane !== false;
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
      empty.textContent = 'EMPTY SLOT - Available';
      card.appendChild(empty);
    } else {
      const id = document.createElement('span');
      id.className = 'queue-slot-id';
      id.textContent = String(position) + '. ' + text(entry.sensorId) + ' / ' + text(entry.entryId);
      card.appendChild(id);
      const score = document.createElement('span');
      score.className = 'queue-slot-score';
      score.textContent = number(entry.dirtyScore, 1) + ' - ' + text(entry.sourceReason)
        + ' - ' + String(entry.secondsSinceLastClean) + ' s since last clean';
      card.appendChild(score);
      // The threshold that put an entry in the queue is a Snapshot configuration fact, not a queue fact:
      // the slot is therefore toned from the published score alone and the number is printed, never judged.
      if (typeof entry.dirtyScore === 'number') card.dataset.tone = 'info';
    }
    grid.appendChild(card);
  }
  host.appendChild(grid);

  const head = document.createElement('p');
  head.className = 'note';
  head.textContent = 'queue: ' + String(entries.length) + ' of ' + String(capacity) + ' slots filled'
    + ' - total queued ' + String(queue.totalQueued) + ' at queue revision ' + String(queue.revision)
    + ' (' + text(queue.label) + ') - auto sequence ' + text(queue.autoSequence)
    + ' - ' + (entries.length === Number(queue.totalQueued) ? 'entries agree with the total'
      : 'ENTRIES AND TOTAL DISAGREE: ' + String(entries.length) + ' vs ' + String(queue.totalQueued));
  host.appendChild(head);

  if (!withLane) return;
  section(host, 'Next-dispatch countdown lane (view scale, never a recomputation)', (block) => {
    const canvas = document.createElement('canvas');
    canvas.width = 620;
    canvas.height = 110;
    canvas.id = 'delay-curve';
    canvas.setAttribute('role', 'img');
    canvas.setAttribute('aria-label', 'Frozen countdown lane of the Cleaning Job Workspace at the selected view scale');
    block.appendChild(canvas);
    const buttons = document.createElement('div');
    buttons.className = 'speed';
    for (const scale of VIEW_SCALES) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'speed-btn';
      button.dataset.scale = String(scale);
      button.setAttribute('aria-pressed', viewScale === scale ? 'true' : 'false');
      button.textContent = scale.toFixed(2).replace(/0$/, '') + 'x';
      button.addEventListener('click', () => setViewScale(scale));
      buttons.appendChild(button);
    }
    block.appendChild(buttons);
    const caption = document.createElement('p');
    caption.className = 'note';
    caption.id = 'curve-caption';
    block.appendChild(caption);
    drawCountdownLane(canvas, caption, workspace);
  });
}

/**
 * The countdown lane: the remaining seconds the host froze, drawn against the seconds axis at the
 * selected view scale. Nothing is interpolated forward and no timer runs here - when the host stops
 * publishing a countdown the lane says so instead of walking on its own.
 */
export function drawCountdownLane(canvas, caption, workspace) {
  const context = typeof canvas.getContext === 'function' ? canvas.getContext('2d') : null;
  if (!context) {
    caption.textContent = 'countdown lane: canvas not available in this browser';
    return;
  }
  const countdown = workspace ? workspace.countdown : null;
  const width = canvas.width;
  const height = canvas.height;
  context.clearRect(0, 0, width, height);
  const seconds = countdown && typeof countdown.remainingSeconds === 'number' ? countdown.remainingSeconds : null;
  const preview = workspace ? workspace.preview : null;
  const applied = preview && typeof preview.appliedDelaySeconds === 'number' ? preview.appliedDelaySeconds : null;
  const span = Math.max(1, applied === null ? 600 : applied);
  const lane = height - 34;

  context.strokeStyle = 'rgba(120, 140, 160, 0.55)';
  context.lineWidth = 1;
  context.beginPath();
  context.moveTo(8, lane);
  context.lineTo(width - 8, lane);
  context.stroke();
  const ticks = 5;
  context.font = '10px ui-monospace, Menlo, Consolas, monospace';
  context.textAlign = 'center';
  context.fillStyle = 'rgba(154, 165, 177, 0.85)';
  for (let index = 0; index <= ticks; index += 1) {
    const x = 8 + (index / ticks) * (width - 16);
    const shown = Math.round((index / ticks) * span / viewScale);
    context.beginPath();
    context.moveTo(x, lane - 4);
    context.lineTo(x, lane + 4);
    context.stroke();
    context.fillText(String(shown) + ' s', x, lane + 16);
  }
  if (seconds === null) {
    context.setLineDash([4, 3]);
    context.strokeStyle = 'rgba(154, 165, 177, 0.5)';
    context.beginPath();
    context.moveTo(8, lane - 12);
    context.lineTo(width - 8, lane - 12);
    context.stroke();
    context.setLineDash([]);
    caption.textContent = 'countdown lane: no remaining seconds published in this workspace - nothing is drawn forward';
    return;
  }
  const x = 8 + Math.min(1, Math.max(0, seconds / span)) * (width - 16);
  context.strokeStyle = 'rgba(227, 179, 65, 0.95)';
  context.lineWidth = 2;
  context.beginPath();
  context.moveTo(8, lane - 12);
  context.lineTo(x, lane - 12);
  context.stroke();
  context.beginPath();
  context.moveTo(x, lane - 22);
  context.lineTo(x, lane - 2);
  context.stroke();
  caption.textContent = 'countdown lane: ' + String(seconds) + ' s remaining at ' + viewScale.toFixed(2).replace(/0$/, '')
    + 'x view scale - the axis is compressed for reading only, the published countdown and its '
    + (countdown && countdown.frozen ? 'frozen' : 'unfrozen') + ' state are unchanged ('
    + text(countdown ? countdown.lifecycle : null) + ', generation '
    + text(countdown && countdown.decision ? countdown.decision.countdownGeneration : null) + ')';
}

/**
 * The single published outcome of the last released Job. The Runtime keeps one record and no archive of
 * completed cycles, so the panel prints that one row and says what it does not carry, rather than laying
 * out a history that would have to be invented.
 */
export function renderOutcome(host, snapshot) {
  host.textContent = '';
  const sequence = snapshot ? snapshot.sequence : null;
  const outcome = sequence ? sequence.lastJobOutcome : null;
  if (!outcome) {
    const note = document.createElement('p');
    note.className = 'outcome-absent';
    note.textContent = 'no completed-cycle record published: NO HISTORICAL CYCLE ARCHIVE - LAST OUTCOME ONLY';
    host.appendChild(note);
    return;
  }
  const line = document.createElement('p');
  line.className = 'outcome-line';
  line.dataset.tone = String(outcome.outcome) === 'COMPLETE' ? 'ok' : 'warn';
  line.textContent = 'job ' + text(outcome.jobId) + ' on ' + text(outcome.targetSensorId)
    + ' - outcome ' + text(outcome.outcome) + ' - trigger ' + text(outcome.trigger)
    + ' at phase ' + text(outcome.phaseAtTrigger)
    + ' - cleaning phases complete ' + (outcome.cleaningPhasesComplete ? 'YES' : 'NO');
  host.appendChild(line);
  facts(host, [
    ['valve', text(outcome.valveId) + ' - close command seq ' + text(outcome.valveCloseCommandSeq)
      + ', closed confirmed seq ' + text(outcome.valveClosedConfirmedSeq, 'not observed: closure was pressure-inferred')
      + ', resolution ' + text(outcome.valveCloseResolution, NOT_PUBLISHED)],
    ['diagnosis', text(outcome.valveDiagnosis, 'none recorded')],
    ['qualified', text(outcome.qualifiedCompletion, 'not recorded')
      + (Array.isArray(outcome.qualifiedRemarks) && outcome.qualifiedRemarks.length ? ' - ' + outcome.qualifiedRemarks.join(' / ') : '')],
    ['queue revision', text(outcome.queueRevisionBefore) + ' -> ' + text(outcome.queueRevisionAfter)
      + ' (entry ' + text(outcome.queueEntryId) + ', dispatch ' + text(outcome.dispatchId) + ')'],
    ['axis evidence', 'return command seq ' + text(outcome.axisReturnCommandSeq)
      + ', standby confirmed seq ' + text(outcome.standbyConfirmedSeq)],
    ['outcome / release seq', text(outcome.outcomeSeq) + ' / ' + text(outcome.releaseSeq)],
    ['auto sequence', text(outcome.autoSequenceAtRelease)],
    ['finalized', timeOf(outcome.finalizedAt)],
  ]);
  const events = Array.isArray(outcome.events) ? outcome.events : [];
  const list = document.createElement('p');
  list.className = 'note';
  list.textContent = 'events of that record: ' + String(events.length)
    + (events.length ? ' - ' + events.map((event) => String(event.seq) + ' ' + String(event.event)).join(', ') : ' - none')
    + ' | NO HISTORICAL CYCLE ARCHIVE - LAST OUTCOME ONLY';
  host.appendChild(list);
  const faults = Array.isArray(outcome.equipmentFaults) ? outcome.equipmentFaults : [];
  const tail = document.createElement('p');
  tail.className = 'note';
  tail.dataset.tone = faults.length ? 'bad' : 'mute';
  tail.textContent = faults.length
    ? faults.map((fault) => text(fault.valveId) + ' ' + text(fault.diagnosis)).join(' | ')
    : 'no equipment fault recorded on that outcome';
  host.appendChild(tail);
}
