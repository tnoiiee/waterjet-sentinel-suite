// WJSS Operator presentation - Active Job KPI Cockpit, Pump, Alarms and Device Links.
//
// Every value on this panel is a published field of the Snapshot. The panel is read-only in the
// strongest sense available to it: it renders no button, no field and no gesture that maps to a
// request other than a GET, because the host exposes no write route at all. Pressures are printed
// with their published quality and source identity, so a value can never be read as a promise the
// instrument did not make.

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
    if (Array.isArray(value)) {
      for (const piece of value) {
        const pill = document.createElement('span');
        pill.className = 'pill pill-UNCERTAIN';
        pill.textContent = piece;
        entry.appendChild(pill);
      }
    } else {
      entry.textContent = value;
    }
    if (tone) entry.dataset.tone = tone;
    list.appendChild(term);
    list.appendChild(entry);
  }
  host.appendChild(list);
  return list;
}

function kpiTile(parent, label, value, sub, tone, titleText) {
  const tile = document.createElement('div');
  tile.className = 'kpi-tile';
  if (tone) tile.dataset.tone = tone;
  if (titleText) tile.title = titleText;
  const lbl = document.createElement('span');
  lbl.className = 'kpi-tile-lbl';
  lbl.textContent = label;
  const val = document.createElement('span');
  val.className = 'kpi-tile-val num';
  val.textContent = value;
  if (tone) val.dataset.tone = tone;
  const subEl = document.createElement('span');
  subEl.className = 'kpi-tile-sub num';
  subEl.textContent = sub;
  tile.appendChild(lbl);
  tile.appendChild(val);
  tile.appendChild(subEl);
  parent.appendChild(tile);
  return tile;
}

function safeReturnFacts(safeReturn) {
  if (!safeReturn || typeof safeReturn !== 'object') {
    return { tone: 'mute', line: 'NONE - no Mandatory Safe Return is running for this job' };
  }

  const valve = safeReturn.valve && typeof safeReturn.valve === 'object' ? safeReturn.valve : null;
  const axis = safeReturn.axis && typeof safeReturn.axis === 'object' ? safeReturn.axis : null;
  const failure = safeReturn.failure && typeof safeReturn.failure === 'object' ? safeReturn.failure : null;
  const legs = 'valve ' + (valve
    ? text(valve.valveId, 'unnamed') + ' command ' + text(valve.command) + ', feedback ' + text(valve.feedback)
      + ', resolution ' + text(valve.resolution, 'NOT_RESOLVED')
    : 'no valve leg published')
    + ' - axis ' + (axis
      ? 'command ' + text(axis.command) + ', standby ' + text(axis.standby)
      : 'no axis leg published');

  return {
    tone: failure ? 'bad' : 'warn',
    line: text(safeReturn.step, 'STEP_NOT_PUBLISHED')
      + ' - triggered by ' + text(safeReturn.trigger)
      + ', pending ' + text(safeReturn.pendingOutcome)
      + ', started ' + timeOf(safeReturn.startedAt)
      + ' - ' + legs
      + (failure ? ' - FAILED ' + text(failure.reason) + ' at ' + timeOf(failure.at) : ''),
  };
}

function bar(host, fraction) {
  const value = typeof fraction === 'number' && Number.isFinite(fraction) ? Math.min(1, Math.max(0, fraction)) : 0;
  const track = document.createElement('div');
  track.className = 'progress';
  track.setAttribute('role', 'img');
  track.setAttribute('aria-label', 'phase progress ' + Math.round(value * 100) + ' percent');
  const fill = document.createElement('div');
  fill.className = 'progress-fill';
  fill.style.setProperty('--fraction', String(value));
  track.appendChild(fill);
  host.appendChild(track);
}

function activeSeqIndex(job) {
  if (!job) return -1;
  const phase = String(job.phase || '').toUpperCase();
  const cleaning = String(job.cleaningPhase || '').toUpperCase();
  if (phase.indexOf('SAFE_RETURN') >= 0) return 7;
  if (phase.indexOf('CLOSE_IV') >= 0) return 6;
  if (cleaning === 'P4' || cleaning === 'P5' || cleaning === 'P6' || phase === 'P4' || phase === 'P5' || phase === 'P6') return 5;
  if (cleaning === 'P3' || phase === 'P3') return 4;
  if (cleaning === 'P2' || phase === 'P2') return 3;
  if (phase.indexOf('OPEN_IV') >= 0) return 2;
  if (phase.indexOf('P1') >= 0 || cleaning === 'P1') return 1;
  return 0;
}

export function renderJob(host, snapshot, workspace) {
  const job = snapshot ? snapshot.activeJob : null;
  const pump = snapshot ? snapshot.pump : null;
  const outcome = snapshot && snapshot.sequence ? snapshot.sequence.lastJobOutcome : null;
  const countdown = workspace ? workspace.countdown : null;
  const preview = workspace ? workspace.preview : null;

  const strip = document.createElement('div');
  strip.className = 'kpi-strip-4';

  const delayVal = countdown && typeof countdown.remainingSeconds === 'number'
    ? String(countdown.remainingSeconds) + ' s'
    : (preview && typeof preview.appliedDelaySeconds === 'number' ? String(preview.appliedDelaySeconds) + ' s' : '--');
  const delaySub = countdown && countdown.lifecycle
    ? text(countdown.lifecycle) + (countdown.frozen ? ' (FROZEN)' : '')
    : (preview ? text(preview.mode) + ' x' + number(preview.zoneFactor, 2) : 'No countdown');

  if (!job) {
    kpiTile(strip, 'TARGET', 'STANDBY', outcome ? 'Last: ' + text(outcome.targetSensorId) + ' (' + text(outcome.outcome) + ')' : 'No active job', 'mute');
    kpiTile(strip, 'PUMP OUTLET', pump ? number(pump.pressure, 1, 'bar') : '--',
      pump ? (pump.ready ? 'READY' : text(pump.state)) + ' | SP ' + number(pump.setpoint, 1, 'b') : 'No pump state',
      pump && pump.ready ? 'ok' : 'warn');
    kpiTile(strip, 'ISOLATION VALVE', 'CLOSED',
      outcome ? text(outcome.valveId) + ' ' + text(outcome.valveCloseResolution) : 'No active valve', 'mute');
    kpiTile(strip, 'NEXT DELAY', delayVal, delaySub, countdown && countdown.frozen ? 'warn' : 'info');
    host.appendChild(strip);

    const standbySeq = document.createElement('div');
    standbySeq.className = 'seq-bar seq-bar-standby num';
    const standbyLabels = ['Home', 'P1', 'IV Open', 'P2', 'P3', 'P4..P6', 'IV Close', 'Safe Return'];
    for (let i = 0; i < standbyLabels.length; i += 1) {
      const step = document.createElement('div');
      step.className = i === standbyLabels.length - 1 ? 'seq-step standby-park' : 'seq-step standby';
      step.textContent = standbyLabels[i];
      standbySeq.appendChild(step);
    }
    host.appendChild(standbySeq);

    bar(host, 0);

    const idleNote = document.createElement('p');
    idleNote.className = 'cockpit-safe-line';
    idleNote.textContent = outcome
      ? 'STANDBY - Parked at Safe Return (125, 125) | Last: ' + text(outcome.jobId) + ' on ' + text(outcome.targetSensorId)
        + ' (' + text(outcome.outcome) + ', finalized ' + timeOf(outcome.finalizedAt) + ')'
      : 'STANDBY - Parked at Safe Return (125, 125) | No cleaning job active at rev ' + text(snapshot ? snapshot.revision : null);
    host.appendChild(idleNote);
    return;
  }

  const safeReturnView = safeReturnFacts(job.safeReturn);
  const pumpTitle = 'Pump outlet ' + number(job.pumpOutletPressureBar, 2, 'bar')
    + ' (' + text(job.pumpPressureQuality) + ' from ' + text(job.pumpPressureSourceId) + ')'
    + ' | Ready setpoint ' + number(job.pumpReadySetpointBar, 2, 'bar');
  const valveTitle = 'Valve ' + text(job.valveId) + ' outlet ' + number(job.valveOutletPressureBar, 2, 'bar')
    + ' (' + text(job.valvePressureQuality) + ' from ' + text(job.valvePressureSourceId) + ')'
    + ' | Command ' + text(job.valveOpenCommand) + ' | Resolution ' + text(job.valveOpenResolution)
    + ' | Diagnosis ' + text(job.valveDiagnosis);

  kpiTile(strip, 'TARGET', text(job.targetSensorId),
    text(job.jetId) + ' | ' + text(job.valveId) + ' | ' + text(job.phaseLabel, text(job.phase)), 'info',
    'Job ' + text(job.jobId) + ' | Lifecycle ' + text(job.lifecycle) + ' | Started ' + timeOf(job.startedAt));
  kpiTile(strip, 'PUMP OUTLET', number(job.pumpOutletPressureBar, 1, 'bar'),
    text(job.pumpPressureQuality) + ' (' + text(job.pumpPressureSourceId) + ')',
    job.pumpPressureInputValid === true ? 'ok' : 'warn', pumpTitle);
  kpiTile(strip, text(job.valveId, 'VALVE') + ' OUTLET', number(job.valveOutletPressureBar, 1, 'bar'),
    text(job.valveOpenCommand) + ' | ' + text(job.valvePressureQuality),
    job.valvePressureInputValid === true ? 'ok' : 'warn', valveTitle);
  kpiTile(strip, 'NEXT DELAY', delayVal, delaySub, 'info');
  host.appendChild(strip);

  // 8-Step Sequence Bar
  const seqBar = document.createElement('div');
  seqBar.className = 'seq-bar num';
  const stepLabels = [
    'Home',
    'P1',
    text(job.valveId, 'IV') + ' Open',
    'P2',
    'P3',
    'P4..P6',
    text(job.valveId, 'IV') + ' Close',
    'Safe Return',
  ];
  const currentIdx = activeSeqIndex(job);
  for (let i = 0; i < stepLabels.length; i += 1) {
    const step = document.createElement('div');
    step.className = i < currentIdx ? 'seq-step done' : (i === currentIdx ? 'seq-step active' : 'seq-step');
    step.textContent = stepLabels[i];
    seqBar.appendChild(step);
  }
  host.appendChild(seqBar);

  bar(host, job.phaseProgress);

  const safeLine = document.createElement('p');
  safeLine.className = 'cockpit-safe-line';
  safeLine.dataset.tone = safeReturnView.tone;
  safeLine.title = safeReturnView.line;
  safeLine.textContent = 'Job ' + text(job.jobId) + ' (' + text(job.phaseLabel, text(job.phase))
    + ', step ' + text(job.phaseIndex) + ', started ' + timeOf(job.phaseStartedAt)
    + ') | Safe Return: ' + safeReturnView.line;
  host.appendChild(safeLine);
}

export function renderPump(host, snapshot) {
  const pump = snapshot ? snapshot.pump : null;
  const status = snapshot ? snapshot.runtime : null;
  section(host, 'Pump summary', (block) => {
    if (!pump) {
      facts(block, [['pump', 'no pump state published', 'bad']]);
      return;
    }
    const ready = pump.ready === true;
    facts(block, [
      ['state', text(pump.state), String(pump.state) === 'TRIPPED' ? 'bad'
        : String(pump.state) === 'RUNNING' ? 'ok' : null],
      ['outlet pressure', number(pump.pressure, 2, 'bar')],
      ['setpoint', number(pump.setpoint, 2, 'bar')],
      ['ready band', number(pump.readyBandLow, 2) + ' to ' + number(pump.readyBandHigh, 2) + ' bar'],
      ['ready', ready ? 'YES - the published pressure is inside the ready band' : 'no',
        ready ? 'ok' : 'warn'],
      ['stop requested at', timeOf(pump.stopRequestedAt)],
      ['host uptime', status && typeof status.uptimeSeconds === 'number'
        ? status.uptimeSeconds.toFixed(0) + ' s' : 'NOT_PROVIDED'],
      ['stage marker', text(snapshot && snapshot.runtime ? snapshot.runtime.stageMarker : null)],
    ]);
    const note = document.createElement('p');
    note.className = 'note';
    note.textContent = 'Pressure is published by the Runtime with its quality and its source identity. '
      + 'A value the host marks unusable is shown as unusable: this screen never substitutes a number.';
    block.appendChild(note);
  });
}

let alarmFilter = 'ALL';
const ALARM_FILTERS = ['ALL', 'HIGH', 'MEDIUM', 'LOW'];

export function alarmFilterValue() {
  return alarmFilter;
}

export function setAlarmFilter(value) {
  alarmFilter = ALARM_FILTERS.indexOf(value) >= 0 ? value : 'ALL';
  return alarmFilter;
}

export function renderAlarmFilters(host, snapshot, onFilter) {
  host.textContent = '';
  const alarms = snapshot ? snapshot.alarms : null;
  const items = alarms && Array.isArray(alarms.items) ? alarms.items : [];
  const group = document.createElement('div');
  group.className = 'toggle-group';
  for (const value of ALARM_FILTERS) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'filter-btn';
    button.dataset.filter = value;
    button.setAttribute('aria-pressed', alarmFilter === value ? 'true' : 'false');
    const matching = value === 'ALL' ? items.length : items.filter((item) => String(item.severity) === value).length;
    button.textContent = value + ' (' + String(matching) + ')';
    button.addEventListener('click', () => {
      setAlarmFilter(value);
      if (onFilter) onFilter(value);
    });
    group.appendChild(button);
  }
  host.appendChild(group);
}

export function renderCritical(host, snapshot) {
  host.textContent = '';
  const sequence = snapshot ? snapshot.sequence : null;
  const latch = sequence ? sequence.critical : null;
  if (!latch) {
    const note = document.createElement('p');
    note.className = 'note';
    note.dataset.tone = 'ok';
    note.textContent = 'no latched Main Pump critical event in this Snapshot';
    host.appendChild(note);
  } else {
    facts(host, [
      ['event', text(latch.eventId) + ' / alarm ' + text(latch.alarmId), 'bad'],
      ['kind', text(latch.kind) + ' at severity ' + text(latch.severity)],
      ['latched at', timeOf(latch.raisedAt) + ' (evidence seq ' + text(latch.evidenceSeq) + ')'],
      ['condition', latch.conditionActive ? 'STILL ACTIVE' : 'CLEARED' + (latch.clearedAt ? ' at ' + timeOf(latch.clearedAt) : ''),
        latch.conditionActive ? 'bad' : 'ok'],
      ['acknowledged', latch.acknowledged ? 'YES at ' + timeOf(latch.acknowledgedAt) : 'NO'],
      ['job at event', text(latch.jobId, 'no Job') + ' / target ' + text(latch.targetSensorId, 'none')
        + ' / phase ' + text(latch.phaseAtEvent, NOT_PUBLISHED)],
      ['safe return required', latch.safeReturnRequired ? 'YES' : 'NO'],
      ['safe return complete', latch.safeReturnComplete ? 'YES' : 'NO'],
      ['safe return failed', latch.safeReturnFailed ? 'YES - the latch stays' : 'NO', latch.safeReturnFailed ? 'bad' : null],
      ['modal', latch.modalOpen ? 'OPEN - the latch projects a modal' : 'closed'
        + (latch.modalClosedAt ? ' at ' + timeOf(latch.modalClosedAt) : ''), latch.modalOpen ? 'warn' : null],
    ]);
  }
  const faults = sequence && Array.isArray(sequence.equipmentFaults) ? sequence.equipmentFaults : null;
  if (!faults || faults.length === 0) {
    const note = document.createElement('p');
    note.className = 'note';
    note.dataset.tone = 'mute';
    note.textContent = 'no SIMULATOR equipment fault latched in this Snapshot';
    host.appendChild(note);
    return;
  }
  const table = document.createElement('table');
  table.className = 'grid-table';
  const head = document.createElement('tr');
  for (const label of ['valve', 'diagnosis', 'raised', 'pressure', 'next dispatch', 'modal', 'upper limit', 'lower limit']) {
    const th = document.createElement('th');
    th.scope = 'col';
    th.textContent = label;
    head.appendChild(th);
  }
  const thead = document.createElement('thead');
  thead.appendChild(head);
  table.appendChild(thead);
  const tbody = document.createElement('tbody');
  for (const fault of faults) {
    const tr = document.createElement('tr');
    for (const value of [
      text(fault.valveId),
      text(fault.diagnosis),
      timeOf(fault.raisedAt),
      number(fault.pressureBar, 2, 'bar') + ' (' + text(fault.pressureQuality, NOT_PUBLISHED) + ')',
      fault.nextDispatchBlocked ? 'BLOCKED' : 'not blocked',
      fault.modalOpen ? 'open' : 'closed',
      fault.upperLimitDetected === true ? 'detected' : fault.upperLimitDetected === false ? 'not detected' : 'not observed',
      fault.lowerLimitDetected === true ? 'detected' : fault.lowerLimitDetected === false ? 'not detected' : 'not observed',
    ]) {
      const td = document.createElement('td');
      td.textContent = value;
      tr.appendChild(td);
    }
    tbody.appendChild(tr);
  }
  table.appendChild(tbody);
  const caption = document.createElement('p');
  caption.className = 'note';
  caption.dataset.tone = 'mute';
  caption.textContent = 'latched SIMULATOR equipment fault: observation only, and this screen holds no clear authority';
  host.appendChild(caption);
  host.appendChild(table);
}

export function renderAlarms(host, snapshot, selectedId) {
  const alarms = snapshot ? snapshot.alarms : null;
  const items = alarms && Array.isArray(alarms.items) ? alarms.items : [];
  const faults = snapshot && snapshot.sequence && Array.isArray(snapshot.sequence.equipmentFaults)
    ? snapshot.sequence.equipmentFaults : [];
  const devices = snapshot && snapshot.communication && Array.isArray(snapshot.communication.devices)
    ? snapshot.communication.devices : [];

  const active = items.filter((item) => String(item.state).indexOf('ACTIVE') === 0);
  section(host, 'Alarms - ' + String(active.length) + ' active of ' + String(items.length)
    + ' retained (unack ' + text(alarms ? alarms.activeUnack : 0) + ')', (block) => {
    const shown = alarmFilter === 'ALL' ? items : items.filter((item) => String(item.severity) === alarmFilter);
    const ack = document.createElement('p');
    ack.className = 'ack-note';
    ack.textContent = 'UI-ONLY ACKNOWLEDGE - NOT SENT TO RUNTIME: this build exposes no acknowledge '
      + 'affordance at all, because a button that only changes a screen would read as a device action on '
      + 'a wall. Acknowledgement belongs to the Runtime, and until a command route exists the count above '
      + 'is the only acknowledgement state this screen can honestly show.';
    block.appendChild(ack);

    if (shown.length === 0) {
      const note = document.createElement('p');
      note.className = 'note';
      note.dataset.tone = items.length === 0 ? 'ok' : 'mute';
      note.textContent = items.length === 0
        ? 'The Runtime retained no alarm for this revision.'
        : 'no alarm of severity ' + alarmFilter + ' in the retained set - ' + String(items.length) + ' row(s) hidden by this display filter';
      block.appendChild(note);
    } else {
      const table = document.createElement('table');
      table.className = 'grid-table num';
      const head = document.createElement('tr');
      for (const label of ['Severity', 'Code', 'Description', 'Sensor / Device', 'State', 'Raised']) {
        const th = document.createElement('th');
        th.scope = 'col';
        th.textContent = label;
        head.appendChild(th);
      }
      const thead = document.createElement('thead');
      thead.appendChild(head);
      table.appendChild(thead);
      const body = document.createElement('tbody');
      for (const item of shown) {
        const tr = document.createElement('tr');
        if (String(item.severity) === 'HIGH') tr.dataset.alarm = 'critical';
        if (selectedId && item.sensorId === selectedId) tr.dataset.selected = 'true';
        const cells = [
          text(item.severity),
          text(item.code),
          text(item.text),
          text(item.sensorId) + ' / ' + text(item.deviceId),
          text(item.state),
          timeOf(item.raisedAt),
        ];
        for (const value of cells) {
          const td = document.createElement('td');
          td.textContent = value;
          tr.appendChild(td);
        }
        body.appendChild(tr);
      }
      table.appendChild(body);
      block.appendChild(table);
    }
  });

  section(host, 'Equipment faults and device links', (block) => {
    if (faults.length === 0) {
      facts(block, [['equipment faults', 'none published at this revision', 'mute']]);
    } else {
      facts(block, faults.map((fault) => [
        text(fault.valveId),
        text(fault.diagnosis) + ' - next dispatch ' + (fault.nextDispatchBlocked === true ? 'BLOCKED' : 'not blocked')
          + ', pressure ' + number(fault.pressureBar, 2, 'bar') + ' ' + text(fault.pressureQuality)
          + ', limits ' + text(fault.upperLimitDetected) + ' / ' + text(fault.lowerLimitDetected)
          + ' at ' + timeOf(fault.raisedAt),
        fault.nextDispatchBlocked === true ? 'bad' : 'warn',
      ]));
    }
    if (devices.length > 0) {
      const rows = devices.map((device) => [
        text(device.deviceId),
        text(device.state) + ' - timeouts ' + text(device.consecutiveTimeouts)
          + ', ok ' + text(device.pollsOk) + ', failed ' + text(device.pollsFailed)
          + ', last ' + number(device.lastLatencyMs, 0, 'ms') + ' at ' + timeOf(device.lastSuccessAt),
        String(device.state) === 'ONLINE' ? 'ok'
          : String(device.state) === 'STALE_SOURCE' ? 'warn' : 'bad',
      ]);
      facts(block, rows);
    } else {
      facts(block, [['device links', 'no device health published', 'mute']]);
    }
  });
}

export function criticalAlert(snapshot) {
  const alarms = snapshot && snapshot.alarms ? snapshot.alarms : null;
  const items = alarms && Array.isArray(alarms.items) ? alarms.items : [];
  const critical = items.find((item) => String(item.severity) === 'HIGH'
    && String(item.state) === 'ACTIVE_UNACK');
  if (critical) {
    return {
      tone: 'alarm',
      message: text(critical.code) + ': ' + text(critical.text)
        + (critical.sensorId ? ' - Sensor ' + String(critical.sensorId) : '')
        + (critical.deviceId ? ' - device ' + String(critical.deviceId) : '')
        + '. Raised ' + timeOf(critical.raisedAt) + ', unacknowledged.',
    };
  }
  const faults = snapshot && snapshot.sequence && Array.isArray(snapshot.sequence.equipmentFaults)
    ? snapshot.sequence.equipmentFaults : [];
  const fault = faults.find((entry) => entry && entry.modalOpen === true);
  if (fault) {
    return {
      tone: 'equipment',
      message: 'Equipment fault on ' + text(fault.valveId) + ': ' + text(fault.diagnosis)
        + '. Next dispatch is ' + (fault.nextDispatchBlocked === true ? 'blocked' : 'not blocked')
        + '. Raised ' + timeOf(fault.raisedAt) + '.',
    };
  }
  return null;
}

export function renderChips(chips, snapshot, status, health, connection) {
  const set = (id, value, tone) => {
    const host = chips.querySelector('#' + id);
    if (!host) return;
    const target = host.querySelector('b') || host;
    target.textContent = value;
    if (tone) host.dataset.tone = tone; else host.removeAttribute('data-tone');
  };

  const profile = status ? status.profile : (snapshot ? snapshot.deviceProfile : null);
  const acquisition = health && health.acquisitionMode ? String(health.acquisitionMode) : null;
  set('chip-profile', text(profile, 'NOT_READ'), null);
  set('chip-acquisition',
    acquisition ? acquisition + (acquisition === 'PHYSICAL_READ_ONLY'
      ? ' / READ ONLY, no device write' : ' / no device access')
      : 'NOT_READ',
    acquisition ? (acquisition === 'PHYSICAL_READ_ONLY' ? 'warn' : 'ok') : null);

  const ready = status ? status.ready === true : null;
  set('chip-readiness', ready === null ? 'NOT_READ'
    : (ready ? 'READY - ' + text(status.readinessCode) : 'NOT READY - ' + text(status.readinessCode)),
    ready === null ? null : (ready ? 'ok' : 'bad'));

  set('chip-revision', snapshot ? text(snapshot.revision) + ' at ' + timeOf(snapshot.generatedAt) : 'NOT_READ',
    connection && connection.phase !== 'live' ? 'warn' : null);

  const phase = connection ? connection.phase : 'booting';
  const detail = connection && connection.failures > 0 ? ' (' + String(connection.failures) + ' failed attempt'
    + (connection.failures === 1 ? '' : 's') + ')' : '';
  set('chip-connection', phase === 'live' ? 'LIVE' : String(phase).toUpperCase() + detail,
    phase === 'live' ? 'ok' : (phase === 'offline' ? 'bad' : 'warn'));
}
