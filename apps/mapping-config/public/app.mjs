// Stage 0.4B-2 — Mapping Configuration UI (read-only except the Draft rack).
// Every value is written with textContent. Nothing here connects, polls, reads or
// writes a device, and nothing activates a configuration. Addresses are derived
// and are shown as read-only text; no control accepts an address.
//
// Filters, grouping, density and expand/collapse are presentation only. They choose which rows are shown and how
// they look; they never touch the Draft, a binding, a revision or a derived address.

import {
  DEFAULT_DENSITY, DENSITY, DraftSession, TAG_FILTERS, buildTagRows, compactAddressSummary, createConfiguration,
  filterCounts, filterRows, getProfile, groupRows, groupValidation, issueStatusText, placeholderSummary,
} from '/pkg/index.mjs';

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
  // Presentation state. Never part of the Draft and never part of any revision.
  density: DEFAULT_DENSITY,
  tagFilter: 'all',
  tagView: 'grouped',
  cache: null,
};

/**
 * A native details element whose list is built only while it is open, so collapsed content is not repeated in the
 * page. The list is read-only text.
 */
function lazyDetails({ cls, summary, buildList, open = false }) {
  const details = el('details', { class: cls, open }, []);
  const head = el('summary', {}, summary);
  const fill = () => details.replaceChildren(head, buildList());
  const collapse = () => details.replaceChildren(head);
  details.addEventListener('toggle', () => { if (details.open) fill(); else collapse(); });
  if (open) fill(); else collapse();
  return details;
}

const reasonList = (reasons) => () => el('ul', {}, reasons.map((r) => el('li', {}, r)));

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
    el('p', { class: 'summary-line' }, placeholderSummary(state.reservedInventory).text),
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
const TAG_COLUMNS = 9;

function tagRowNode(row) {
  const classes = ['data-row'];
  if (row.kind === 'reserved') {
    classes.push('st-reserved');
    if (row.direction === 'OUTPUT') classes.push('st-unauthorized');
  } else {
    classes.push(row.enabled ? 'st-enabled' : 'st-disabled');
  }
  if (row.issueErrors > 0) classes.push('has-error');

  const summary = compactAddressSummary(row);
  let addressCell;
  let addressClass = '';
  if (row.kind === 'binding' && row.addressState === 'ADDRESS_UNRESOLVED') {
    addressClass = 'addr-unresolved';
    addressCell = lazyDetails({ cls: 'reasons', summary, buildList: reasonList(row.reasons) });
  } else {
    if (row.kind === 'reserved') addressClass = 'addr-unresolved';
    addressCell = summary;
  }
  const status = issueStatusText(row);
  const statusClass = row.issueErrors > 0 ? 'issue-error' : row.issueWarnings > 0 ? 'issue-warning' : 'cell-secondary';
  const cells = [
    row.tag,
    row.role,
    row.moduleInstanceId ?? '—',
    row.channel === null || row.channel === undefined ? '—' : String(row.channel),
    row.kind === 'reserved' ? 'no (reserved)' : row.enabled ? 'yes' : 'no',
    row.kind === 'reserved' ? '—' : row.analog ? 'NOT APPLICABLE' : (row.activePolarity ?? 'NOT SET'),
    row.kind === 'reserved' ? '—' : row.analog ? 'NOT APPLICABLE' : (row.contactType ?? 'UNKNOWN'),
    addressCell,
    status,
  ];
  return el('tr', { class: classes.join(' '), 'data-kind': row.kind, 'data-tag': row.tag }, cells.map((c, i) => {
    const cls = i === 7 ? addressClass : i === 8 ? statusClass : i === 1 ? 'cell-secondary' : '';
    return el('td', cls ? { class: cls } : {}, c);
  }));
}

function renderTagFilters(counts) {
  const buttons = counts.map((f) => {
    const b = el('button', {
      type: 'button', class: 'toggle', 'data-filter': f.key, 'aria-pressed': state.tagFilter === f.key ? 'true' : 'false',
    }, `${f.label} (${f.count})`);
    b.addEventListener('click', () => { state.tagFilter = f.key; renderTags(state.cache.v); });
    return b;
  });
  $('tag-filters').replaceChildren(...buttons);
}

function renderTags(v) {
  const snap = state.session.snapshot();
  const rows = buildTagRows({
    entries: v.addresses.entries,
    bindings: snap.bindings,
    additionalTags: state.additionalTags,
    issues: [...v.rackIssues, ...v.mappingIssues],
    reservedRows: state.reservedInventory ? state.reservedInventory.rows : [],
  });
  const counts = filterCounts(rows);
  renderTagFilters(counts);
  const shown = filterRows(rows, state.tagFilter);
  const body = [];
  if (state.tagView === 'grouped') {
    for (const g of groupRows(shown)) {
      body.push(el('tr', { class: 'group-row' }, [el('th', { scope: 'colgroup', colspan: TAG_COLUMNS }, `${g.group} · ${g.rows.length} ${g.rows.length === 1 ? 'row' : 'rows'}`)]));
      for (const row of g.rows) body.push(tagRowNode(row));
    }
  } else {
    for (const row of shown) body.push(tagRowNode(row));
  }
  $('tags-body').replaceChildren(...body);
  $('view-grouped').setAttribute('aria-pressed', state.tagView === 'grouped' ? 'true' : 'false');
  $('view-flat').setAttribute('aria-pressed', state.tagView === 'flat' ? 'true' : 'false');
  const label = TAG_FILTERS.find((f) => f.key === state.tagFilter).label;
  $('tags-count').textContent = `Showing ${shown.length} of ${rows.length} rows · filter: ${label} · view: ${state.tagView === 'grouped' ? 'grouped by equipment' : 'flat list'}. Filters change the view only; the Draft and its revisions are not affected.`;
  const r = state.importReport;
  $('outputs-note').textContent = r
    ? `${r.classifications.outputNotAuthorised} other output rows are NOT AUTHORIZED FOR MAPPING IN READ-ONLY STAGE and carry no binding. Reserved output DO-031 is listed under Reserved channels. Addresses are derived from verified Module Profiles only; none is verified in this stage.`
    : 'Addresses are derived from verified Module Profiles only; none is verified in this stage. Addresses cannot be entered here.';
}

// The reserved inventory is read-only, comes from the workbook import only, and is never part of the Draft or any revision.
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
  const ps = placeholderSummary(inv);
  $('placeholder-summary').textContent = `${ps.input} Input + ${ps.output} Output = ${ps.total} Total · ${ps.text}`;
  $('reserved-note').textContent = `Read-only. ${total} rows are USED / RESERVED: INPUT ${ps.input} · OUTPUT ${ps.output} (NOT AUTHORIZED IN READ-ONLY STAGE). They are not enabled bindings and are not SPARE or FREE. They are never auto-bound and have no address. Identity is requested only with new Owner data.`;
}

function renderValidation(v) {
  const issues = [...v.rackIssues, ...v.mappingIssues];
  const counts = v.addresses.entries.reduce((acc, e) => {
    acc[e.state] = (acc[e.state] ?? 0) + 1;
    return acc;
  }, {});
  const groups = groupValidation({
    issues,
    addressEntries: v.addresses.entries,
    reservedRows: state.reservedInventory ? state.reservedInventory.rows : [],
  });
  const groupNodes = groups.map((g) => {
    const sev = g.severity === 'ERROR' ? 'sev-error' : 'sev-warning';
    // Errors are open by default: they are never collapsed out of sight.
    const open = g.key === 'errors' && g.count > 0;
    return lazyDetails({
      cls: `vgroup ${sev}${g.count === 0 ? ' zero' : ''}`,
      summary: [el('span', { class: 'count' }, String(g.count)), g.label],
      buildList: () => (g.items.length
        ? el('ul', {}, g.items.map((i) => el('li', { class: `sev-${i.severity.toLowerCase()}` }, i.text)))
        : el('p', { class: 'note' }, 'None.')),
      open,
    });
  });
  $('validation-body').replaceChildren(
    el('p', {}, `Status ${v.status} · errors ${v.summary.errors} · warnings ${v.summary.warnings} · unresolved addresses ${v.unresolvedAddressCount}`),
    el('div', { class: 'vgroups' }, groupNodes),
    el('p', {}, `Activation ready: ${v.activationReady ? 'yes' : 'no'} · Activation: ${v.activationLabel}`),
    el('p', {}, `Blocking reasons: ${v.blockingReasons.join(', ') || 'none'}`),
    el('p', {}, `Address states: ${Object.entries(counts).map(([k, n]) => `${k} ${n}`).join(' · ') || 'none'}`),
  );
}

function shortId(digest) {
  return digest.slice(0, 12);
}

function renderEvidence() {
  const rep = state.session.evidenceReport();
  const head = rep.head;
  const kind = rep.synthetic ? 'SYNTHETIC TEST RULE (not vendor evidence)' : rep.authoritative ? 'AUTHORITATIVE' : 'NOT AUTHORITATIVE';
  $('evidence-summary').replaceChildren(
    el('p', { class: 'summary-line' }, `${rep.label} · ${kind} · address-capable: ${rep.addressCapable ? 'yes' : 'no'}`),
    el('p', {}, `Head-station profile ${head.modelNumber ?? '—'}: ${head.evidenceState} · ${head.completeness}${head.missingCells.length ? ` · not verified: ${head.missingCells.join(', ')}` : ''}`),
    lazyDetails({
      cls: 'reasons', summary: `Head-station reasons · ${head.reasons.length}`, buildList: reasonList(head.reasons),
    }),
    el('p', {}, `Module profiles: ${rep.summary.completeProfiles} complete · ${rep.summary.incompleteProfiles} incomplete of ${rep.summary.modules} · addresses derived ${rep.summary.derivedModules} · unresolved ${rep.summary.unresolvedModules}`),
    el('p', { class: 'note' }, `Evidence revision ${shortId(rep.fingerprint)} · head-station fingerprint ${shortId(head.fingerprint)}`),
  );
  $('evidence-body').replaceChildren(...rep.modules.map((m) => {
    const widths = m.processInputWidth === null && m.processOutputWidth === null
      ? 'NOT VERIFIED' : `${m.processInputWidth ?? 'NOT VERIFIED'} / ${m.processOutputWidth ?? 'NOT VERIFIED'}`;
    const order = m.processImageOrder.value === null ? m.processImageOrder.state : `${m.processImageOrder.state} · ${m.processImageOrder.value}`;
    let address;
    if (m.addressState === 'ADDRESS_UNRESOLVED') {
      const n = m.reasons.length;
      address = lazyDetails({ cls: 'reasons', summary: `ADDRESS UNRESOLVED · ${n} ${n === 1 ? 'reason' : 'reasons'}`, buildList: reasonList(m.reasons) });
    } else {
      address = m.addressState === 'DERIVED' ? 'DERIVED (read-only)' : 'NOT APPLICABLE';
    }
    return el('tr', { 'data-id': m.moduleInstanceId }, [
      el('td', {}, `${m.modelNumber} · ${m.moduleInstanceId}`),
      el('td', {}, String(m.rackSlot)),
      el('td', {}, m.processModulePosition === null ? '—' : String(m.processModulePosition)),
      el('td', {}, `${m.evidenceState} · ${m.completeness}`),
      el('td', { class: 'cell-secondary' }, m.sourceIds.length ? m.sourceIds.join(', ') : 'NONE PROVIDED'),
      el('td', {}, widths),
      el('td', { class: 'cell-secondary' }, `diagnostic ${m.diagnosticState} · status byte ${m.statusByteState} · byte/word order ${m.byteWordOrderState}`),
      el('td', {}, order),
      el('td', m.addressState === 'ADDRESS_UNRESOLVED' ? { class: 'addr-unresolved' } : {}, address),
      el('td', {}, el('code', { title: m.fingerprint }, shortId(m.fingerprint))),
    ]);
  }));
  const srcNodes = rep.sources.map((s) => el('li', {}, `${s.sourceId} · ${s.evidenceType} · ${s.documentTitle} · ${s.documentSha256 ? `SHA-256 ${s.documentSha256.slice(0, 16)}…` : 'no SHA-256'} · ${s.documentRevision ?? 'no revision'}`));
  const issueNodes = rep.issues.map((i) => el('li', { class: 'sev-warning' }, `${i.code} — ${i.message} (${i.path})`));
  $('evidence-sources').replaceChildren(
    el('h3', {}, `Evidence sources (${rep.sources.length})`),
    el('ul', {}, srcNodes.length ? srcNodes : [el('li', {}, 'No source provided.')]),
    ...(issueNodes.length ? [el('h3', {}, `Evidence issues (${issueNodes.length})`), el('ul', {}, issueNodes)] : []),
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
    row('ProcessImageEvidenceRevision', rev.processImageEvidenceRevision),
    row('DerivedAddressManifestFingerprint', rev.derivedAddressManifestFingerprint),
    el('p', { class: 'note' }, 'Identities exclude timestamps, UI selection and object order. They describe the Draft only and do not feed Runtime or RuntimePublication.'),
  );
}

function refresh() {
  const { v, impact, rev } = evaluateAll();
  state.cache = { v };
  renderHeader(v);
  renderSource();
  renderRack(v);
  renderTags(v);
  renderEvidence();
  renderValidation(v);
  renderImpact(impact);
  renderRevisions(rev);
}

$('btn-undo').addEventListener('click', () => { state.refusal = ''; state.session.undo(); refresh(); });
$('btn-redo').addEventListener('click', () => { state.refusal = ''; state.session.redo(); refresh(); });
$('btn-reset').addEventListener('click', () => { state.refusal = ''; state.session.resetToDefault(); refresh(); });

// Presentation controls. They change how rows are shown, never the Draft or any revision.
function setDensity(density) {
  state.density = density;
  $('app-root').setAttribute('data-density', density);
  $('density-comfortable').setAttribute('aria-pressed', density === DENSITY.COMFORTABLE ? 'true' : 'false');
  $('density-compact').setAttribute('aria-pressed', density === DENSITY.COMPACT ? 'true' : 'false');
}
$('density-comfortable').addEventListener('click', () => setDensity(DENSITY.COMFORTABLE));
$('density-compact').addEventListener('click', () => setDensity(DENSITY.COMPACT));
$('view-grouped').addEventListener('click', () => { state.tagView = 'grouped'; if (state.cache) renderTags(state.cache.v); });
$('view-flat').addEventListener('click', () => { state.tagView = 'flat'; if (state.cache) renderTags(state.cache.v); });

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
