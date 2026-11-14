// WJSS Operator presentation - paired thermal diagnostics, presentation-only.
//
// The Owner ruling for this Stage is that the paired Front-minus-Rear difference is NOT computed
// in the browser. No .NET contract publishes a Front temperature, a Rear temperature or their
// difference for a Sensor: the Runtime publishes the two channel IDENTITIES of each Product Sensor
// (tcFrontChannel and tcRearChannel), the Sensor-level score, classification and quality that the
// acquisition produced, and one TC acquisition state for the node. Decoding the copied words into
// temperatures is an unverified step recorded by the host itself, so this panel reports the
// difference as unavailable instead of deriving a number the host refuses to publish.
//
// What the panel DOES show is the acquisition evidence, field by field, including the host's own
// list of what stays not verified. Quarantined, non-physical or deterministic evidence is labelled
// as such and is never presented as a field reading.

import { text, timeOf } from './feed.mjs';

/** The literal the screen prints while the difference has no published source. */
export const DFR_UNAVAILABLE = 'DFR_NOT_PUBLISHED_BY_RUNTIME';

export function dfrStatement(snapshot, sensorId) {
  const sensor = findSensor(snapshot, sensorId);
  if (!sensor) return { value: DFR_UNAVAILABLE, present: false };
  const front = text(sensor.tcFrontChannel, '');
  const rear = text(sensor.tcRearChannel, '');
  if (front === '' || rear === '') return { value: DFR_UNAVAILABLE, present: false };
  // Two channel identities exist and no temperature of either is published: the statement names
  // the pair it is missing a value for, so the operator can see what is withheld and why.
  return { value: DFR_UNAVAILABLE + ' (front ' + front + ', rear ' + rear + ')', present: false };
}

export function renderDfrStatement(host, statement) {
  host.textContent = statement.value;
  host.dataset.state = statement.present ? 'present' : 'absent';
  host.title = 'The paired difference is a derived quantity. It has no field in the Runtime '
    + 'Snapshot, so the presentation states that plainly rather than computing one.';
}

function findSensor(snapshot, sensorId) {
  const sensors = snapshot && Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  return sensors.find((entry) => entry && entry.sensorId === sensorId) || null;
}

function fact(host, label, value, tone) {
  const term = document.createElement('dt');
  term.textContent = label;
  const entry = document.createElement('dd');
  entry.textContent = value;
  if (tone) entry.dataset.tone = tone;
  host.appendChild(term);
  host.appendChild(entry);
  return entry;
}

export function renderThermal(host, snapshot, sensorId) {
  host.textContent = '';
  const sensor = findSensor(snapshot, sensorId);
  const tc = snapshot ? snapshot.tcAcquisition : null;

  if (sensor) {
    fact(host, 'sensor', text(sensor.sensorId));
    fact(host, 'front channel', text(sensor.tcFrontChannel));
    fact(host, 'rear channel', text(sensor.tcRearChannel));
    fact(host, 'front minus rear', DFR_UNAVAILABLE, 'warn');
    fact(host, 'dirty score', text(sensor.dirtyScore === null || sensor.dirtyScore === undefined
      ? 'NOT_PROVIDED' : Number(sensor.dirtyScore).toFixed(1)));
    fact(host, 'classification', text(sensor.classification));
    fact(host, 'quality', text(sensor.quality));
  } else {
    fact(host, 'selection', 'no Sensor selected - the paired view follows the wall-map selection', 'mute');
  }

  if (!tc) {
    fact(host, 'tc acquisition', 'no acquisition state published', 'mute');
    return;
  }

  const physical = tc.physical === true;
  const quarantine = tc.quarantined === true;
  fact(host, 'node / cabinet', text(tc.nodeId) + ' / ' + text(tc.cabinetId));
  fact(host, 'provenance', text(tc.provenance), physical ? 'ok' : 'mute');
  fact(host, 'physical', physical ? 'YES - read from the physical path' : 'no - deterministic simulated evidence',
    physical ? 'ok' : 'warn');
  fact(host, 'quarantined', text(tc.quarantined), quarantine ? 'bad' : null);
  fact(host, 'acquisition state', text(tc.acquisitionState),
    String(tc.acquisitionState) === 'ACQUIRED' ? 'ok' : 'warn');
  fact(host, 'acquired at', timeOf(tc.acquiredAtUtc));
  fact(host, 'quality', text(tc.quality), String(tc.quality) === 'GOOD' ? 'ok' : 'warn');
  fact(host, 'diagnostic state', text(tc.diagnosticState));
  fact(host, 'mapping status', text(tc.mappingStatus));
  fact(host, 'decode status', text(tc.decodeStatus),
    String(tc.decodeStatus).indexOf('NOT_VERIFIED') >= 0 ? 'warn' : null);
  fact(host, 'words copied', text(tc.copiedWordCount));

  const evidence = tc.physicalEvidence;
  if (evidence && typeof evidence === 'object') {
    fact(host, 'endpoint identity', text(evidence.endpointIdentity));
    fact(host, 'read profile', text(evidence.readProfile));
    fact(host, 'mapping evidence', text(evidence.mappingEvidenceState));
    fact(host, 'commissioning state', text(evidence.commissioningState));
  } else {
    fact(host, 'physical evidence', 'absent from this publication (serialized only for the physical path)', 'mute');
  }

  const notVerified = Array.isArray(tc.notVerified) ? tc.notVerified : [];
  const list = document.createElement('dt');
  list.textContent = 'not verified';
  const items = document.createElement('dd');
  if (notVerified.length === 0) {
    items.textContent = 'the host declared nothing outstanding';
    items.dataset.tone = 'mute';
  } else {
    const box = document.createElement('span');
    for (const item of notVerified) {
      const pill = document.createElement('span');
      pill.className = 'pill pill-UNCERTAIN';
      pill.textContent = String(item);
      box.appendChild(pill);
    }
    items.appendChild(box);
  }
  host.appendChild(list);
  host.appendChild(items);

  const note = document.createElement('dt');
  note.textContent = 'reading rule';
  const noteValue = document.createElement('dd');
  noteValue.dataset.tone = 'mute';
  noteValue.textContent = 'Copied words are process-image state, not proof of a field device. A decode '
    + 'status that says NOT_VERIFIED is shown as it is: the presentation adds no engineering unit '
    + 'and no temperature of its own.';
  host.appendChild(note);
  host.appendChild(noteValue);
}
