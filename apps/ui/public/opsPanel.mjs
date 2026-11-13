// WJSS Operator presentation - Active Job, Pump, alarms and device links.
//
// Every value on this panel is a published field of the Snapshot. The panel is read-only in the
// strongest sense available to it: it renders no button, no field and no gesture that maps to a
// request other than a GET, because the host exposes no write route at all. Pressures are printed
// with their published quality and source identity, so a value can never be read as a promise the
// instrument did not make.

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

/**
 * The Mandatory Safe Return leg in the contract's own words. The host publishes a step, a trigger, a
 * pending outcome and one leg per device, so this prints exactly those and nothing between them: an
 * absent leg is named as absent rather than as idle, and no state is invented for a device the
 * Snapshot did not report on. A published failure is the only case that reads as bad.
 */
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

export function renderJob(host, snapshot) {
  const job = snapshot ? snapshot.activeJob : null;
  const outcome = snapshot && snapshot.sequence ? snapshot.sequence.lastJobOutcome : null;

  if (!job) {
    section(host, 'Active job', (block) => {
      facts(block, [
        ['active job', 'NONE - no cleaning job is active at revision ' + text(snapshot ? snapshot.revision : null), 'mute'],
      ]);
      if (outcome) {
        facts(block, [
          ['last job outcome', text(outcome.jobId) + ' on ' + text(outcome.targetSensorId)],
          ['outcome', text(outcome.outcome), String(outcome.outcome).indexOf('FAULT') >= 0 ? 'bad' : 'ok'],
          ['trigger / phase at trigger', text(outcome.trigger) + ' at ' + text(outcome.phaseAtTrigger)],
          ['cleaning phases complete', text(outcome.cleaningPhasesComplete)],
          ['valve', text(outcome.valveId) + ' - close resolution ' + text(outcome.valveCloseResolution)],
          ['finalized at', timeOf(outcome.finalizedAt)],
        ]);
      }
    });
    return;
  }

  const safeReturnView = safeReturnFacts(job.safeReturn);
  section(host, 'Active job - ' + text(job.phaseLabel) + ' (' + text(job.phase) + ', step '
    + text(job.phaseIndex) + ')', (block) => {
    facts(block, [
      ['job', text(job.jobId)],
      ['target sensor', text(job.targetSensorId), 'ok'],
      ['water jet / isolation valve', text(job.jetId) + ' / ' + text(job.valveId)],
      ['cleaning phase', text(job.cleaningPhase)],
      ['lifecycle', text(job.lifecycle)],
      ['started / phase started', timeOf(job.startedAt) + ' / ' + timeOf(job.phaseStartedAt)],
    ]);
    bar(block, job.phaseProgress);
    facts(block, [
      ['pump outlet pressure', number(job.pumpOutletPressureBar, 2, 'bar') + ' - '
        + text(job.pumpPressureQuality) + ' from ' + text(job.pumpPressureSourceId),
        job.pumpPressureInputValid === true ? 'ok' : 'warn'],
      ['pump ready setpoint', number(job.pumpReadySetpointBar, 2, 'bar')],
      ['valve outlet pressure', number(job.valveOutletPressureBar, 2, 'bar') + ' - '
        + text(job.valvePressureQuality) + ' from ' + text(job.valvePressureSourceId),
        job.valvePressureInputValid === true ? 'ok' : 'warn'],
      ['valve open command', text(job.valveOpenCommand)],
      ['valve resolution', text(job.valveOpenResolution)],
      ['valve diagnosis', text(job.valveDiagnosis)],
      ['safe return', safeReturnView.line, safeReturnView.tone],
    ]);
  });
}

export function renderPump(host, snapshot) {
  const pump = snapshot ? snapshot.pump : null;
  const status = snapshot ? snapshot.runtime : null;
  section(host, 'Pump', (block) => {
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

export function renderAlarms(host, snapshot, selectedId) {
  const alarms = snapshot ? snapshot.alarms : null;
  const items = alarms && Array.isArray(alarms.items) ? alarms.items : [];
  const faults = snapshot && snapshot.sequence && Array.isArray(snapshot.sequence.equipmentFaults)
    ? snapshot.sequence.equipmentFaults : [];
  const devices = snapshot && snapshot.communication && Array.isArray(snapshot.communication.devices)
    ? snapshot.communication.devices : [];

  const active = items.filter((item) => String(item.state).indexOf('ACTIVE') === 0);
  section(host, 'Alarms - ' + String(active.length) + ' active of ' + String(items.length)
    + ' retained', (block) => {
    facts(block, [
      ['active unacknowledged', text(alarms ? alarms.activeUnack : 0),
        Number(alarms && alarms.activeUnack) > 0 ? 'bad' : null],
      ['active acknowledged', text(alarms ? alarms.activeAck : 0)],
      ['cleared unacknowledged', text(alarms ? alarms.clearedUnack : 0)],
    ]);

    if (items.length === 0) {
      const note = document.createElement('p');
      note.className = 'note';
      note.dataset.tone = 'ok';
      note.textContent = 'The Runtime retained no alarm for this revision.';
      block.appendChild(note);
    } else {
      const table = document.createElement('table');
      table.className = 'grid-table';
      const head = document.createElement('tr');
      for (const label of ['sev', 'code', 'what', 'sensor / device', 'state', 'raised']) {
        const th = document.createElement('th');
        th.scope = 'col';
        th.textContent = label;
        head.appendChild(th);
      }
      const thead = document.createElement('thead');
      thead.appendChild(head);
      table.appendChild(thead);
      const body = document.createElement('tbody');
      for (const item of items) {
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

/**
 * The modal request is the contract's own: an unacknowledged HIGH alarm, or an equipment fault the
 * host marks as demanding a modal. The screen never escalates a state the host did not escalate,
 * and dismissing this dialog acknowledges no device - it only stops covering the wall.
 */
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

/**
 * Header chips: the host facts a standing operator must see without moving. Values are copied out
 * of the published documents; a fact the host has not answered for yet reads NOT_READ and is never
 * shown as if it were current.
 */
export function renderChips(chips, snapshot, status, health, connection) {
  const set = (id, value, tone) => {
    const host = chips.querySelector('#' + id);
    if (!host) return;
    const target = host.querySelector('b') || host;
    target.textContent = value;
    if (tone) host.dataset.tone = tone; else host.removeAttribute('data-tone');
  };

  const profile = status ? status.profile : (snapshot ? snapshot.deviceProfile : null);
  // The acquisition mode is the host's own word for what it may read: DETERMINISTIC names the
  // deterministic path and PHYSICAL_READ_ONLY names the physical one. Neither is a write claim, and
  // the chip says so instead of implying a capability the host does not publish.
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
