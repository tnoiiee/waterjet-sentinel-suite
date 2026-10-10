// Stage 0.4B-1 — Mapping Configuration UI (read-only except the Draft rack).
// Every value is written with textContent. Nothing here connects, polls, reads or
// writes a device, and nothing activates a configuration. Addresses are derived
// and are shown as read-only text; no control accepts an address.

import { CHANNEL_TYPE, DraftSession, createConfiguration, getProfile, getTagDef } from '/pkg/index.mjs';

const $ = (id) => document.getElementById(id);

function el(tag, attrs = {}, children = []) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === null || v === undefined || v === false) continue;
    if (k === 'class') node.className = v;
    else if (k.startsWith('data-')) node.setAttribute(k, String(v));
    else if (k === 'title') node.title = String(v);
    else node.setAttribute(k, v === true ? '' : String(v));
  }
  for (const c of [].concat(children)) {
    if (c === null || c === undefined) continue;
    node.append(c instanceof Node ? c : document.createTextNode(String(c)));
  }
  return node;
}

function chip(text, cls = '') {
  return el('span', { class: `chip ${cls}` }, text);
}

function table(headers, rows, rowAttrs = () => ({})) {
  const thead = el('thead', {}, [el('tr', {}, headers.map((h) => el('th', { scope: 'col' }, h)))]);
  const tbody = el('tbody', {}, rows.map((cells, i) => el('tr', rowAttrs(i), cells.map((c) => el('td', {}, c)))));
  return el('table', {}, [thead, tbody]);
}

const state = {
  session: null,
  mode: null,
  importReport: null,
  reservedInventory: null,
  additionalTags: {},
  defaultOrderKey: null,
  refusal: '',
};

function modulesKey(modules) {
  return modules.map((m) => m.moduleInstanceId).join('|');
}

function evaluateAll() {
  const s = state.session;
  const v = s.validate();
  const impact = s.impactPreview();
  const rev = s.revisions();
  return { v, impact, rev };
}

function renderHeader(v) {
  const chips = $('chips');
  chips.replaceChildren(
    chip(state.mode, state.mode === 'SYNTHETIC EXAMPLE' ? 'warn' : 'info'),
    chip('DRAFT', 'draft'),
    chip('SIMULATION ONLY', 'ok'),
    chip('NO HARDWARE ACCESS', 'ok'),
    chip('NO WRITE CONTROL', 'ok'),
    chip('Provider: SIMULATOR', 'ok'),
    chip(`Validation: ${v.status}`, v.status === 'VALID' ? 'ok' : 'err'),
    chip('ACTIVATION NOT AUTHORIZED', 'err'),
  );
}

function renderSource() {
  const body = $('source-body');
  const r = state.importReport;
  if (!r) {
    body.replaceChildren(el('p', {}, [
      'Showing the SYNTHETIC EXAMPLE. It is generic demonstration data, not the Excel default and not plant data. ',
      'No workbook is loaded in this session.',
    ]));
    return;
  }
  const c = r.counts;
  const issues = Object.entries(r.issueSummary).map(([code, n]) => el('li', {}, `${code} × ${n}`));
  body.replaceChildren(
    el('p', {}, `Excel default · sheet ${r.source.sheetName} · ${r.source.dataRowCount} data rows · USED ${c.USED} · SPARE ${c.SPARE} · modules ${r.modelCount}`),
    el('p', {}, `Inputs: ${r.classifications.inputUnambiguous} unambiguous read-only inputs · ${r.classifications.inputAmbiguous} ambiguous · ${r.classifications.outputNotAuthorised} other outputs NOT AUTHORIZED FOR MAPPING IN READ-ONLY STAGE · ${r.classifications.reservedUnresolved} reserved placeholder rows (${state.reservedInventory?.summary.overlays.outputNotAuthorised ?? 0} of them an output, DO-031), listed under Reserved channels`),
    issues.length ? el('details', {}, [el('summary', {}, `Import issues (${r.issues.length})`), el('ul', {}, issues)]) : el('p', {}, 'No import issues.'),
  );
}

function renderRack(v) {
  const s = state.session;
  const modules = s.snapshot().modules;
  const view = v.rackView;
  const rows = view.map((r, i) => {
    const profile = getProfile(r.modelNumber);
    const reorderable = profile ? profile.reorderable !== false : false;
    const cells = [
      String(r.rackSlot),
      r.moduleInstanceId,
      r.modelNumber,
      r.displayName ?? '—',
      r.processModulePosition === null ? '—' : String(r.processModulePosition),
      String(r.channelCapacity),
      r.profileStatus,
      r.addressStatus === 'ADDRESS_UNRESOLVED' ? 'ADDRESS UNRESOLVED' : 'NOT APPLICABLE',
    ];
    return { cells, reorderable, id: modules[i].moduleInstanceId };
  });
  const tbody = $('rack-body');
  tbody.replaceChildren(...rows.map((row, i) => {
    const tr = el('tr', {
      'data-id': row.id,
      'data-index': i,
      draggable: row.reorderable ? 'true' : 'false',
      tabindex: row.reorderable ? '0' : '-1',
      'aria-label': `${row.cells[1]}, RackSlot ${row.cells[0]}${row.reorderable ? '' : ', fixed by profile'}`,
      class: row.reorderable ? 'movable' : 'fixed',
    }, row.cells.map((c) => el('td', {}, c)));
    if (row.reorderable) {
      tr.addEventListener('dragstart', (e) => {
        e.dataTransfer.setData('text/plain', row.id);
        e.dataTransfer.effectAllowed = 'move';
        tr.classList.add('dragging');
      });
      tr.addEventListener('dragend', () => tr.classList.remove('dragging'));
      tr.addEventListener('dragover', (e) => { e.preventDefault(); e.dataTransfer.dropEffect = 'move'; });
      tr.addEventListener('drop', (e) => {
        e.preventDefault();
        const id = e.dataTransfer.getData('text/plain');
        if (id) moveTo(id, i, id);
      });
      tr.addEventListener('keydown', (e) => {
        if (!e.altKey || (e.key !== 'ArrowUp' && e.key !== 'ArrowDown')) return;
        e.preventDefault();
        const target = e.key === 'ArrowUp' ? i - 1 : i + 1;
        moveTo(row.id, target, row.id);
      });
    }
    return tr;
  }));
  const session = state.session;
  $('btn-undo').disabled = session.undoStack.length === 0;
  $('btn-redo').disabled = session.redoStack.length === 0;
  $('btn-reset').disabled = modulesKey(session.snapshot().modules) === state.defaultOrderKey;
  $('refusal').textContent = state.refusal;
}

function moveTo(moduleInstanceId, toIndex, focusId) {
  const result = state.session.moveModule(moduleInstanceId, toIndex);
  state.refusal = result.ok ? '' : `Refused (${result.refusal.code}): ${result.refusal.message}`;
  refresh();
  const row = document.querySelector(`#rack-body tr[data-id="${focusId}"]`);
  if (row && document.activeElement !== row) row.focus();
}

// Analog rows have no contact polarity or contact type. They read NOT APPLICABLE (presentation only), never NOT SET
// or UNKNOWN. Digital limits read their workbook values (ACTIVE_WHEN_CLOSED, NO).
function isAnalogTag(tagName) {
  const def = getTagDef(tagName);
  if (def) return def.channelType === CHANNEL_TYPE.ANALOG;
  return state.additionalTags?.[tagName]?.channelType === CHANNEL_TYPE.ANALOG;
}

function renderTags(v) {
  const s = state.session;
  const snap = s.snapshot();
  const bindingByTag = new Map(snap.bindings.map((b) => [b.tagName, b]));
  const entries = v.addresses.entries;
  const rows = entries.map((e) => {
    const b = bindingByTag.get(e.tagName) ?? {};
    const def = getTagDef(e.tagName);
    const role = def ? def.role : 'READ-ONLY WORKBOOK INPUT';
    const analog = isAnalogTag(e.tagName);
    const enabled = b.enabled !== false;
    let address;
    let addressClass = '';
    if (e.state === 'DERIVED') {
      address = `${e.displayNotation} (zero-based bit ${e.bitOffsetAbsolute})`;
    } else if (e.state === 'NOT_ACTIVE') {
      address = 'not active';
    } else {
      address = `ADDRESS UNRESOLVED · ${e.reasons.join(', ')}`;
      addressClass = 'addr-unresolved';
    }
    return {
      cls: enabled ? 'st-enabled' : 'st-disabled',
      cells: [
        e.tagName,
        role,
        e.moduleInstanceId ?? '—',
        e.channel === null || e.channel === undefined ? '—' : String(e.channel),
        enabled ? 'yes' : 'no',
        analog ? 'NOT APPLICABLE' : (b.activePolarity ?? 'NOT SET'),
        analog ? 'NOT APPLICABLE' : (b.contactType ?? 'UNKNOWN'),
        address,
      ],
      addressClass,
    };
  });
  $('tags-body').replaceChildren(...rows.map((row) => el('tr', { class: row.cls }, row.cells.map((c, i) => el('td', i === 7 && row.addressClass ? { class: row.addressClass } : {}, c)))));
  const r = state.importReport;
  $('outputs-note').textContent = r
    ? `${r.classifications.outputNotAuthorised} other output rows are NOT AUTHORIZED FOR MAPPING IN READ-ONLY STAGE and carry no binding. Reserved output DO-031 is listed under Reserved channels. Addresses are derived from verified Module Profiles only; none is verified in this stage.`
    : 'Addresses are derived from verified Module Profiles only; none is verified in this stage. Addresses cannot be entered here.';
}

// The reserved inventory is read-only, comes from the workbook import only, and is never part of the Draft or any revision.
const RESERVED_HEADERS = ['Workbook I/O Tag', 'ModuleInstanceId', 'Channel', 'Direction', 'Physical Status', 'Signal Identity',
  'Binding Status', 'Owner Input Status', 'Auto-binding Eligibility', 'Address Status', 'Mapping Authorization'];

function renderReserved() {
  const inv = state.reservedInventory;
  const total = inv ? inv.summary.total : 0;
  $('reserved-title').textContent = `Reserved channels awaiting Owner identity (${total})`;
  const rows = inv ? inv.rows : [];
  $('reserved-body').replaceChildren(...rows.map((x) => {
    const output = x.direction === 'OUTPUT';
    return el('tr', { class: output ? 'st-reserved st-unauthorized' : 'st-reserved', 'data-tag': x.workbookTag }, [
      el('td', {}, x.workbookTag),
      el('td', {}, x.moduleInstanceId ?? '—'),
      el('td', {}, String(x.channel)),
      el('td', { class: output ? 'st-unauthorized' : '' }, output ? 'OUTPUT · NOT AUTHORIZED' : 'INPUT'),
      el('td', {}, x.physicalStatus),
      el('td', {}, x.signalIdentity),
      el('td', {}, x.bindingStatus),
      el('td', {}, x.ownerInputStatus),
      el('td', {}, x.autoBindingEligibility),
      el('td', { class: 'addr-unresolved' }, x.addressStatus),
      el('td', {}, x.mappingAuthorization),
    ]);
  }));
  const byDir = inv ? inv.summary.byDirection : { INPUT: 0, OUTPUT: 0 };
  $('reserved-note').textContent = `Read-only. ${total} rows are USED / RESERVED: INPUT ${byDir.INPUT} · OUTPUT ${byDir.OUTPUT} (NOT AUTHORIZED IN READ-ONLY STAGE). They are not enabled bindings and are not SPARE or FREE. They are never auto-bound and have no address. Identity is requested only with new Owner data.`;
}

function renderValidation(v) {
  const issues = [...v.rackIssues, ...v.mappingIssues];
  const counts = v.addresses.entries.reduce((acc, e) => {
    acc[e.state] = (acc[e.state] ?? 0) + 1;
    return acc;
  }, {});
  const list = issues.length
    ? el('ul', { class: 'issues' }, issues.map((i) => el('li', { class: `sev-${i.severity.toLowerCase()}` }, [
      el('strong', {}, `${i.severity} ${i.code}`), ` — ${i.message}`,
    ])))
    : el('p', {}, 'No validation issues.');
  $('validation-body').replaceChildren(
    el('p', {}, `Status ${v.status} · errors ${v.summary.errors} · warnings ${v.summary.warnings} · unresolved addresses ${v.unresolvedAddressCount}`),
    el('p', {}, `Activation ready: ${v.activationReady ? 'yes' : 'no'} · Activation: ${v.activationLabel}`),
    el('p', {}, `Blocking reasons: ${v.blockingReasons.join(', ') || 'none'}`),
    el('p', {}, `Address states: ${Object.entries(counts).map(([k, n]) => `${k} ${n}`).join(' · ') || 'none'}`),
    list,
  );
}

function renderImpact(impact) {
  const modules = table(
    ['ModuleInstanceId', 'Old RackSlot', 'New RackSlot', 'Old ProcessModulePosition', 'New ProcessModulePosition', 'Classification'],
    impact.modules.map((m) => [
      m.moduleInstanceId,
      String(m.oldRackSlot ?? '—'),
      String(m.newRackSlot),
      m.oldProcessModulePosition === null ? '—' : String(m.oldProcessModulePosition),
      m.newProcessModulePosition === null ? '—' : String(m.newProcessModulePosition),
      m.classification,
    ]),
  );
  const tags = table(
    ['Tag', 'Classification', 'Moved', 'Address unresolved', 'Old address', 'New address'],
    impact.tags.map((t) => [
      t.tagName,
      t.classification,
      t.moved ? 'yes' : 'no',
      t.addressUnresolved ? 'yes' : 'no',
      t.oldAddress === null ? '—' : String(t.oldAddress),
      t.newAddress === null ? '—' : String(t.newAddress),
    ]),
  );
  $('impact-body').replaceChildren(
    el('p', {}, `Validation ${impact.validationResult} · blocking status ${impact.blockingStatus} · ${impact.activationLabel}`),
    el('h3', {}, 'Modules'),
    el('div', { class: 'table-wrap' }, modules),
    el('h3', {}, 'Tags'),
    el('div', { class: 'table-wrap' }, tags),
  );
}

function renderRevisions(rev) {
  const row = (label, digest) => el('div', { class: 'rev' }, [
    el('span', { class: 'rev-label' }, label),
    el('code', { title: digest }, digest.slice(0, 16)),
    el('span', { class: 'rev-full' }, digest),
  ]);
  $('revisions-body').replaceChildren(
    row('RackTopologyRevision', rev.rackTopologyRevision),
    row('TagMappingRevision', rev.tagMappingRevision),
    row('ModuleProfileRevision', rev.moduleProfileRevision),
    row('DerivedAddressManifestFingerprint', rev.derivedAddressManifestFingerprint),
    el('p', { class: 'note' }, 'Identities exclude timestamps, UI selection and object order. They describe the Draft only and do not feed Runtime or RuntimePublication.'),
  );
}

function refresh() {
  const { v, impact, rev } = evaluateAll();
  renderHeader(v);
  renderSource();
  renderRack(v);
  renderTags(v);
  renderValidation(v);
  renderImpact(impact);
  renderRevisions(rev);
}

$('btn-undo').addEventListener('click', () => { state.refusal = ''; state.session.undo(); refresh(); });
$('btn-redo').addEventListener('click', () => { state.refusal = ''; state.session.redo(); refresh(); });
$('btn-reset').addEventListener('click', () => { state.refusal = ''; state.session.resetToDefault(); refresh(); });

async function boot() {
  const res = await fetch('/api/configuration', { cache: 'no-store', headers: { Accept: 'application/json' } });
  if (!res.ok) throw new Error(`configuration unavailable (${res.status})`);
  const data = await res.json();
  state.mode = data.mode;
  state.importReport = data.importReport;
  state.reservedInventory = data.reservedInventory ?? null;
  state.additionalTags = data.configuration.additionalTags ?? {};
  renderReserved();
  const config = createConfiguration(data.configuration);
  state.session = new DraftSession(config);
  state.defaultOrderKey = modulesKey(config.modules);
  refresh();
}

boot().catch((err) => {
  $('source-body').replaceChildren(el('p', { class: 'refusal' }, `Could not load the configuration: ${err.message}`));
});
