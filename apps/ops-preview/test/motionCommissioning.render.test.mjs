// Stage 0.4C-K2 - real-page contract of the read-only Motion Commissioning STATIC CATALOGUE.
//
// The REAL public/app.mjs runs against the REAL Preview host through the stub DOM. This proves the
// application logic and the rendered text; it does not prove CSS, layout or a real browser.
//
// The catalogue is static: it is served by the dedicated read-only route and it is NOT read from the
// Runtime Snapshot. The harness therefore needs no Runtime host at all, and the section must render
// in full without one.

import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { allText, bootApp, cellTexts, html, rowsOf } from './helpers/fakeDom.mjs';
import {
  MOTION_AXIS_ROLE, MOTION_WATER_JET_BINDINGS, axisPresentation,
} from '../../../packages/commissioning/src/index.mjs';

let app;
let closed = false;

before(async () => {
  app = await bootApp({});
});

after(() => { if (!closed) app.close(); });

const section = () => html.slice(html.indexOf('<section id="motion-commissioning"'),
  html.indexOf('</section>', html.indexOf('<section id="motion-commissioning"')));

const tableOf = (id) => app.byId[id].childNodes.find((node) => node.tagName === 'table');
const bodyRows = (id) => tableOf(id).childNodes.find((node) => node.tagName === 'tbody').childNodes;
const headerCells = (id) => cellTexts(tableOf(id).childNodes.find((node) => node.tagName === 'thead').childNodes[0]);
const column = (id, label) => {
  const index = headerCells(id).indexOf(label);
  assert.notEqual(index, -1, `column '${label}' must exist in ${id}`);
  return bodyRows(id).map((row) => cellTexts(row)[index]);
};

test('the page boots ready and the static catalogue is requested as a local GET', () => {
  assert.equal(app.byId['app-root'].attributes['data-app-state'], 'ready');
  assert.equal(app.byId['boot-error'].hidden, true);
  const catalogue = app.requests.find((r) => r.path === '/api/motion-commissioning/catalogue');
  assert.ok(catalogue, 'the static catalogue must be requested at boot');
  assert.equal(catalogue.status, 200);
  assert.equal(catalogue.method, 'GET');
  for (const request of app.requests) {
    assert.equal(request.method, 'GET', request.path);
    assert.equal(request.path.startsWith('/'), true, 'every request is a local route');
  }
});

test('the section is in the served page, carries the boundary text and offers no control at all', () => {
  const markup = section();
  assert.match(markup, /Motion Commissioning/);
  assert.match(markup, /NO HARDWARE ACCESS \/ NO MOTION COMMAND/);
  assert.match(markup, /id="motion-gate-status"/);
  assert.match(html, /href="#motion-commissioning">Motion Commissioning</);
  // The catalogue is static, so the section needs no control whatsoever: there is no refresh, no
  // Jog, Move, Home, Stop, brake, encoder-reset or connect control on this page.
  assert.equal((markup.match(/<button/g) ?? []).length, 0, 'the static catalogue section has no control');
  assert.doesNotMatch(markup, /<input|<form|<select/);
  assert.doesNotMatch(markup, /motion-refresh/);
  for (const forbidden of ['jog', 'move', 'home', 'release brake', 'reset encoder', 'connect']) {
    assert.doesNotMatch(markup.toLowerCase(), new RegExp(`>\\s*${forbidden}\\s*<`), `no '${forbidden}' control`);
  }
});

test('the authorization gate is rendered as CLOSED with its reason, never as a bare code', () => {
  const gate = app.byId['motion-gate-status'].textContent;
  assert.match(gate, /Authorization gate: CLOSED \(Closed\)/);
  assert.match(gate, /MOTION_COMMISSIONING_GATE_NOT_OPEN/);
  assert.match(gate, /The separate motion commissioning execution gate is not open/);
  assert.match(gate, /refused before any hardware path/i);
});

test('the summary counts render, with nothing homed, nothing ready and all eight mappings confirmed', () => {
  const cells = rowsOf(app.byId, 'motion-summary');
  const text = cells.map(allText).join(' | ');
  assert.match(text, /Motion controllers 4/);
  assert.match(text, /Controller axes 16/);
  assert.match(text, /Water Jets mapped 8/);
  assert.match(text, /Axes homed 0/);
  assert.match(text, /Controllers ready 0 of 4/);
  assert.match(text, /Motion commands issued 0/);
  assert.match(text, /Authorization gate CLOSED/);
  // Canonical mapping: all eight confirmed, and the Drawing discrepancy is an evidence note only.
  assert.match(text, /Water Jet mappings confirmed 8 of 8/);
  assert.match(text, /Drawing evidence notes 1/);
  assert.doesNotMatch(text, /Mapping conflicts/);
});

test('all eight Water Jets render the canonical mapping: role first, physical axis letter as identity', () => {
  assert.deepEqual(column('motion-waterjets-body', 'Water Jet'), MOTION_WATER_JET_BINDINGS.map(([id]) => id));
  assert.deepEqual(column('motion-waterjets-body', 'Cabinet'), MOTION_WATER_JET_BINDINGS.map(([, cabinet]) => cabinet));
  assert.deepEqual(column('motion-waterjets-body', 'Controller'), MOTION_WATER_JET_BINDINGS.map(([, cabinet]) => `MC-${cabinet}`));

  const horizontal = column('motion-waterjets-body', 'Horizontal Axis');
  const vertical = column('motion-waterjets-body', 'Vertical Axis');
  MOTION_WATER_JET_BINDINGS.forEach(([, , h, v], index) => {
    // The ROLE leads; the physical controller axis letter follows as diagnostic identity only.
    assert.equal(horizontal[index], axisPresentation(MOTION_AXIS_ROLE[h], h));
    assert.equal(vertical[index], axisPresentation(MOTION_AXIS_ROLE[v], v));
    assert.match(horizontal[index], /^Horizontal Axis · [ABCD]$/);
    assert.match(vertical[index], /^Vertical Axis · [ABCD]$/);
  });

  // The canonical values the Owner ruled for the two Water Jets of the recorded Drawing discrepancy.
  assert.equal(horizontal[1], 'Horizontal Axis · C');
  assert.equal(vertical[1], 'Vertical Axis · D');
  assert.equal(horizontal[3], 'Horizontal Axis · A');
  assert.equal(vertical[3], 'Vertical Axis · B');

  // Every row is canonically confirmed with Owner-confirmed provenance: no open decision survives.
  for (const status of column('motion-waterjets-body', 'Mapping status')) {
    assert.equal(status, 'CONFIRMED_OWNER (Confirmed by the Owner)');
  }
  for (const basis of column('motion-waterjets-body', 'Provenance')) {
    assert.equal(basis, 'OWNER_CONFIRMED (Owner confirmed)');
  }
});

test('all four controllers render, none configured, none connected, none ready', () => {
  const header = headerCells('motion-controllers-body');
  assert.deepEqual(header, ['Attribute', 'MC-WSB1', 'MC-WSB2', 'MC-WSB3', 'MC-WSB4']);
  const rowFor = (label) => {
    const row = bodyRows('motion-controllers-body').find((r) => cellTexts(r)[0] === label);
    assert.ok(row, `row '${label}' must render`);
    return cellTexts(row).slice(1);
  };
  assert.deepEqual(rowFor('Cabinet'), ['WSB1', 'WSB2', 'WSB3', 'WSB4']);
  assert.deepEqual(rowFor('Dispatch ready'), ['FALSE', 'FALSE', 'FALSE', 'FALSE']);
  for (const value of rowFor('Endpoint')) assert.equal(value, 'ENDPOINT_NOT_CONFIGURED (Not configured)');
  for (const value of rowFor('Communication')) assert.equal(value, 'NOT_CONNECTED (Not connected)');
  for (const value of rowFor('Blocked by')) assert.match(value, /MOTION_COMMISSIONING_GATE_NOT_OPEN/);
  for (const value of rowFor('Identity basis')) assert.match(value, /Inferred from the confirmed mapping/);
});

test('all sixteen axes render: not homed, brake unknown, absolute move refused', () => {
  const rows = bodyRows('motion-axes-body');
  assert.equal(rows.length, 16);
  const roles = column('motion-axes-body', 'Axis role');
  assert.equal(roles.filter((role) => role === 'Horizontal Axis').length, 8);
  assert.equal(roles.filter((role) => role === 'Vertical Axis').length, 8);
  // The physical axis letter is present as diagnostic identity, and only ever beside the role.
  const physical = column('motion-axes-body', 'Physical axis');
  assert.deepEqual(physical.filter((axis) => axis === 'A').length, 4);
  for (const axis of physical) assert.match(axis, /^[ABCD]$/);
  for (const value of column('motion-axes-body', 'Home')) assert.equal(value, 'NOT_HOMED (Not homed)');
  for (const value of column('motion-axes-body', 'Brake command')) assert.equal(value, 'None');
  for (const value of column('motion-axes-body', 'Brake state')) assert.equal(value, 'UNKNOWN (Unknown)');
  for (const value of column('motion-axes-body', 'Motion state')) assert.equal(value, 'UNKNOWN (Unknown)');
  for (const value of column('motion-axes-body', 'Absolute move')) assert.equal(value, 'REFUSED (Refused)');
  for (const value of column('motion-axes-body', 'Commissioning')) assert.equal(value, 'Not commissioned');
});

test('the brake output plan renders A-1, B-2, C-3, D-4', () => {
  assert.deepEqual(column('motion-brake-body', 'Controller axis'), ['A', 'B', 'C', 'D']);
  assert.deepEqual(column('motion-brake-body', 'Brake output'), ['Output 1', 'Output 2', 'Output 3', 'Output 4']);
  for (const value of column('motion-brake-body', 'Allocation basis')) assert.equal(value, 'Owner confirmed');
});

test('the three limit inputs of every axis render as unconfirmed and unread', () => {
  const header = headerCells('motion-limits-body');
  assert.deepEqual(header.slice(0, 3), ['Controller axis', 'Axis role', 'Sense']);
  assert.deepEqual(header.slice(3, 6), ['Reverse', 'Home', 'Forward']);
  for (const role of ['Reverse', 'Home', 'Forward']) {
    for (const value of column('motion-limits-body', role)) assert.equal(value, 'UNKNOWN (Unknown)');
  }
  for (const value of column('motion-limits-body', 'Polarity')) assert.equal(value, 'Unconfirmed');
});

test('every motion intent is rendered as refused by the closed gate', () => {
  const intents = column('motion-intents-body', 'Motion intent');
  assert.equal(intents.length, 6);
  assert.ok(intents.includes('JOG (Jog)'));
  assert.ok(intents.includes('RESET_ENCODER (Reset encoder)'));
  for (const value of column('motion-intents-body', 'Command state')) assert.equal(value, 'Refused');
  for (const value of column('motion-intents-body', 'Refused by (gate)')) {
    assert.equal(value, 'MOTION_COMMISSIONING_GATE_NOT_OPEN');
  }
});

test('the Jog Speed renders as a display value in controller units that is not executable', () => {
  const text = allText(app.byId['motion-jog-body']);
  assert.match(text, /Jog Speed: 10 controller units/);
  assert.match(text, /Legacy candidate, unverified/);
  assert.match(text, /NOT EXECUTABLE/);
  assert.doesNotMatch(text, /mm\/s/);
});

test('the Drawing evidence note, the not-verified claims and the legend render', () => {
  const notes = rowsOf(app.byId, 'motion-evidence-notes').map(allText).join(' | ');
  assert.match(notes, /MOTION-EVIDENCE-01/);
  assert.match(notes, /WJ2, WJ4/);
  assert.match(notes, /WSB1, WSB2/);
  // Recorded as evidence: never as an open decision, and explicitly not physically resolved.
  assert.match(notes, /DRAWING_REV1_CONFLICT_RECORDED/);
  assert.match(notes, /EVIDENCE_ONLY_NOT_CANONICAL/);
  assert.match(notes, /NOT_PHYSICALLY_RESOLVED/);
  assert.doesNotMatch(notes, /OWNER_DECISION_REQUIRED/);
  assert.doesNotMatch(notes, /MOTION-CONFLICT-01/);

  const notVerified = rowsOf(app.byId, 'motion-not-verified').map(allText).join(' | ');
  for (const claim of ['NO_CONTROLLER_CONTACTED', 'NO_MOTION_COMMAND_ISSUED', 'NO_UNIT_CONVERSION_VERIFIED']) {
    assert.ok(notVerified.includes(claim), claim);
  }

  const legend = rowsOf(app.byId, 'motion-legend').map(allText).join(' | ');
  for (const reason of ['MOTION_COMMISSIONING_GATE_NOT_OPEN', 'GCLIB_NOT_INSTALLED', 'BRAKE_STATE_UNKNOWN',
    'HARDWARE_COMMANDS_NOT_AUTHORIZED']) {
    assert.ok(legend.includes(reason), reason);
  }
  assert.ok(legend.split(' | ').length >= 10, 'every refusal reason is worded in the legend');
});

test('the catalogue is static: no Runtime Snapshot is read, and the section renders in full anyway', () => {
  // No request in the whole page boot reads the Runtime Snapshot for this section: the catalogue
  // route is the only motion request, and it carries no Runtime read flag.
  for (const request of app.requests.filter((r) => r.path.includes('motion'))) {
    assert.equal(request.path, '/api/motion-commissioning/catalogue');
  }
  // The section rendered the whole catalogue without a Runtime host.
  assert.equal(bodyRows('motion-axes-body').length, 16);
  assert.equal(column('motion-waterjets-body', 'Water Jet').length, 8);

  const authority = app.byId['motion-authority'].textContent;
  assert.match(authority, /NO HARDWARE ACCESS \/ NO MOTION COMMAND \/ NO BRAKE OUTPUT \/ NO HOME \/ NO MOVE \/ NO ENCODER RESET/);
  assert.match(authority, /NOT the Cleaning-process interlocks/);
  // The separation is stated on the screen, not only in the payload.
  assert.match(authority, /static: it is served by the dedicated read-only catalogue route/);
  assert.match(authority, /carried by neither the Runtime Snapshot nor a Runtime Delta/);
});
