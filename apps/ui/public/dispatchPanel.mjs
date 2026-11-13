// WJSS Operator presentation - the Cleaning Job Workspace panel.
//
// One atomic document from GET /api/v1/dispatch-workspace carries the GlobalQueue, the AutoSequence
// mode, the frozen next-dispatch countdown, the dispatch-cycle timeline and the live TimeDelay
// preview, all at one Runtime revision. The panel renders that document; it holds no queue, job,
// eligibility or delay state of its own and it sends nothing back. The countdown is displayed as
// frozen or not frozen because the contract freezes the applied delay per countdown: the screen
// never recomputes a delay, never extends a countdown and never reorders a queue entry.

import { text, number, timeOf } from './feed.mjs';

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
