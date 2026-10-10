// Stage 0.4B-1 — configuration states and the Draft session.
//
//   ActivatedDefaultRack   deep-frozen; never overwritten; initially active for
//                          SIMULATION only. Label: DEFAULT FROM EXCEL (or the
//                          SYNTHETIC EXAMPLE label for demo data).
//   DraftRack              an editable clone. Supports reorder and binding edits,
//                          undo/redo, reset to default, validation, impact
//                          preview and draft revision export. It cannot activate.
//
// There is no activation method in this module. ACTIVATION NOT AUTHORIZED is
// returned for every draft, whatever its validation state.

import { FORBIDDEN_BINDING_KEYS, SEVERITY, LABEL, ADDRESS_STATE, IMPACT, REASON, STAGE } from './constants.mjs';
import { canonicalJson } from './canonical.mjs';
import { deriveRackView, moveModule, validateRack } from './rack.mjs';
import { validateMapping, summariseIssues } from './mappingValidation.mjs';
import { deriveAddresses } from './addressDerivation.mjs';
import { AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE } from './processImageEvidence.mjs';
import { actualRackEvidenceFor } from './actualRackEvidence.mjs';
import { buildEvidenceReport } from './evidenceReport.mjs';
import { buildCandidateProcessImage } from './candidateProcessImage.mjs';
import {
  rackTopologyRevision, tagMappingRevision, moduleProfileRevision, processImageEvidenceRevision,
  derivedAddressManifestFingerprint,
} from './revisions.mjs';

const EDITABLE_FIELDS = Object.freeze(['moduleInstanceId', 'channel', 'enabled', 'engineering', 'activePolarity', 'contactType']);
// IVn pressure and limit identities are fixed by the workbook, where the '#n' label equals IVn. The Draft may not
// move them to another module or Channel, because that would bypass the import checks.
// Pump Inlet (AI-002) and Pump Outlet (AI-003) are NOT locked by module or Channel. The workbook gives the default,
// and the Draft may change it when the change is compatible. Validation enforces capacity, signal type, duplicate
// physical binding and the semantic boundaries. The Pump source identity (tagName, sourceWorkbookTag and
// declaredSourceIdentity) is never editable in the Draft, so the semantic identity cannot change.
const WORKBOOK_FIXED_TAG_RE = /^IV[1-8]_(OUTLET_PRESSURE|LOWER_LIMIT|UPPER_LIMIT)$/;
const WORKBOOK_FIXED_FIELDS = Object.freeze(['moduleInstanceId', 'channel']);

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function deepFreeze(value) {
  if (value && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.freeze(value);
    for (const v of Object.values(value)) deepFreeze(v);
  }
  return value;
}

/**
 * Creates an immutable configuration. The input is copied, so later changes to
 * the caller's object cannot reach the configuration.
 */
export function createConfiguration(input) {
  const cfg = clone(input);
  if (!cfg.label || !Array.isArray(cfg.modules) || !Array.isArray(cfg.bindings)) {
    throw new Error('configuration needs label, modules and bindings');
  }
  return deepFreeze({
    label: cfg.label,
    modules: cfg.modules,
    bindings: cfg.bindings,
    declaredChannelCounts: cfg.declaredChannelCounts ?? {},
    additionalTags: cfg.additionalTags ?? {},
  });
}

export class DraftSession {
  /**
   * @param defaultConfig  a configuration created by createConfiguration
   * @param options.evidence  optional process-image evidence set. The default is the authoritative set, which
   *                          holds no verified evidence, so every address stays ADDRESS_UNRESOLVED. Tests may pass a
   *                          SYNTHETIC TEST RULE set; it is never verified and never activation-ready.
   */
  constructor(defaultConfig, options = {}) {
    this.defaultConfig = createConfiguration(defaultConfig);
    this.evidence = options.evidence ?? AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE;
    this.draft = clone({ modules: this.defaultConfig.modules, bindings: this.defaultConfig.bindings });
    this.undoStack = [];
    this.redoStack = [];
  }

  label() {
    return LABEL.DRAFT;
  }

  /** Read-only copy of the draft (callers cannot mutate session state through it). */
  snapshot() {
    return deepFreeze(clone({ modules: this.draft.modules, bindings: this.draft.bindings }));
  }

  defaultSnapshot() {
    return this.defaultConfig;
  }

  _commit(next) {
    const before = clone(this.draft);
    const after = clone(next);
    if (canonicalJson(before) === canonicalJson(after)) return false;
    this.undoStack.push(before);
    this.redoStack = [];
    this.draft = after;
    return true;
  }

  moveModule(moduleInstanceId, toIndex) {
    const result = moveModule(this.draft.modules, moduleInstanceId, toIndex);
    if (!result.ok) return result;
    this._commit({ modules: result.modules, bindings: this.draft.bindings });
    return result;
  }

  /**
   * Changes editable fields of one binding. Address keys are refused outright;
   * source identity and tag identity cannot be edited.
   */
  setBinding(tagName, patch) {
    for (const key of Object.keys(patch)) {
      if (FORBIDDEN_BINDING_KEYS.includes(key)) {
        return { ok: false, refusal: { code: 'MANUAL_ADDRESS_REFUSED', message: `'${key}' is derived and read-only` } };
      }
      if (!EDITABLE_FIELDS.includes(key)) {
        return { ok: false, refusal: { code: 'FIELD_NOT_EDITABLE', message: `'${key}' cannot be edited in the Draft` } };
      }
    }
    if (WORKBOOK_FIXED_TAG_RE.test(tagName)) {
      for (const key of Object.keys(patch)) {
        if (WORKBOOK_FIXED_FIELDS.includes(key)) {
          return { ok: false, refusal: { code: 'WORKBOOK_IDENTITY_FIXED', message: `'${key}' of ${tagName} is fixed by the workbook import and cannot be changed in the Draft` } };
        }
      }
    }
    const next = clone(this.draft);
    let binding = next.bindings.find((b) => b.tagName === tagName);
    if (!binding) {
      binding = { tagName, moduleInstanceId: null, channel: null, enabled: true };
      next.bindings.push(binding);
    }
    Object.assign(binding, clone(patch));
    this._commit(next);
    return { ok: true, refusal: null };
  }

  undo() {
    if (this.undoStack.length === 0) return false;
    this.redoStack.push(clone(this.draft));
    this.draft = this.undoStack.pop();
    return true;
  }

  redo() {
    if (this.redoStack.length === 0) return false;
    this.undoStack.push(clone(this.draft));
    this.draft = this.redoStack.pop();
    return true;
  }

  /** Restores the Excel default into the Draft. The default itself is untouched. */
  resetToDefault() {
    return this._commit({ modules: this.defaultConfig.modules, bindings: this.defaultConfig.bindings });
  }

  _evaluate(state, config) {
    const rackIssues = validateRack(state.modules, config.declaredChannelCounts);
    const mappingIssues = validateMapping(state.modules, state.bindings, config.additionalTags);
    // Actual-rack evidence belongs to the ModuleInstance, so a reorder never changes what the real rack shows.
    const rackView = deriveRackView(state.modules, actualRackEvidenceFor(state.modules));
    const addresses = deriveAddresses(rackView, state.bindings, this.evidence);
    return { rackIssues, mappingIssues, rackView, addresses };
  }

  validate() {
    const ev = this._evaluate(this.draft, this.defaultConfig);
    const allIssues = [...ev.rackIssues, ...ev.mappingIssues];
    const errors = allIssues.filter((i) => i.severity === SEVERITY.ERROR);
    const enabledUnresolved = ev.addresses.entries.filter((e) => e.state === ADDRESS_STATE.UNRESOLVED);
    const blocking = [];
    for (const e of errors) blocking.push(e.code);
    if (enabledUnresolved.length > 0) blocking.push(REASON.NO_VERIFIED_PROCESS_IMAGE_RULE);
    const valid = errors.length === 0;
    return {
      status: valid ? LABEL.VALID : LABEL.INVALID,
      // A synthetic rule can derive numbers, but it is never verified, so it can never make a Draft ready.
      activationReady: valid && enabledUnresolved.length === 0 && ev.addresses.verified === true,
      activationAuthorized: false,
      activationLabel: LABEL.ACTIVATION_NOT_AUTHORIZED,
      blockingReasons: [...new Set(blocking)].sort(),
      rackIssues: ev.rackIssues,
      mappingIssues: ev.mappingIssues,
      summary: summariseIssues(allIssues),
      unresolvedAddressCount: enabledUnresolved.length,
      addresses: ev.addresses,
      rackView: ev.rackView,
    };
  }

  revisions(state = this.draft, config = this.defaultConfig) {
    const ev = this._evaluate(state, config);
    const rackRevision = rackTopologyRevision(state.modules);
    const mappingRevision = tagMappingRevision(state.bindings);
    const profileRevision = moduleProfileRevision();
    const evidenceRevision = processImageEvidenceRevision(this.evidence);
    return {
      rackTopologyRevision: rackRevision,
      tagMappingRevision: mappingRevision,
      moduleProfileRevision: profileRevision,
      processImageEvidenceRevision: evidenceRevision,
      derivedAddressManifestFingerprint: derivedAddressManifestFingerprint({
        rackRevision, mappingRevision, profileRevision, evidenceRevision, addresses: ev.addresses,
      }),
    };
  }

  /**
   * Read-only evidence review for the current Draft rack: per module profile state, sources, widths, order and
   * address state with reasons. Recomputed from the Draft order, so a reorder is reflected. Nothing is editable.
   */
  evidenceReport() {
    const rackView = deriveRackView(this.draft.modules, actualRackEvidenceFor(this.draft.modules));
    return buildEvidenceReport(this.evidence, rackView);
  }

  /**
   * Read-only CANDIDATE process image for the current Draft rack. It is never authoritative, never verified and
   * never a basis for a write. Every offset in it is unresolved until the actual Process Data image and the
   * head-station I/O Config are supplied.
   */
  candidateProcessImage() {
    const rackView = deriveRackView(this.draft.modules, actualRackEvidenceFor(this.draft.modules));
    return buildCandidateProcessImage(rackView);
  }

  /**
   * Impact of the Draft against the ActivatedDefaultRack. Nothing is applied.
   */
  impactPreview() {
    const before = this._evaluate(this.defaultConfig, this.defaultConfig);
    const after = this._evaluate(this.draft, this.defaultConfig);
    const oldModule = new Map(before.rackView.map((r) => [r.moduleInstanceId, r]));
    const newModule = new Map(after.rackView.map((r) => [r.moduleInstanceId, r]));
    const oldAddr = new Map(before.addresses.entries.map((e) => [e.tagName, e]));
    const newAddr = new Map(after.addresses.entries.map((e) => [e.tagName, e]));
    const errorTags = new Set(after.mappingIssues.filter((i) => i.severity === SEVERITY.ERROR && i.tagName).map((i) => i.tagName));
    const rackBlocked = after.rackIssues.some((i) => i.severity === SEVERITY.ERROR);

    const modules = [...newModule.values()].map((n) => {
      const o = oldModule.get(n.moduleInstanceId);
      return {
        moduleInstanceId: n.moduleInstanceId,
        modelNumber: n.modelNumber,
        oldRackSlot: o ? o.rackSlot : null,
        newRackSlot: n.rackSlot,
        oldProcessModulePosition: o ? o.processModulePosition : null,
        newProcessModulePosition: n.processModulePosition,
        classification: o && o.rackSlot !== n.rackSlot ? IMPACT.MOVED : IMPACT.UNCHANGED,
      };
    });
    const movedIds = new Set(modules.filter((m) => m.classification === IMPACT.MOVED).map((m) => m.moduleInstanceId));

    const tags = [...newAddr.values()].map((n) => {
      const o = oldAddr.get(n.tagName);
      const moved = movedIds.has(n.moduleInstanceId);
      const unresolved = n.state === ADDRESS_STATE.UNRESOLVED;
      const changed = o !== undefined && o.bitOffsetAbsolute !== n.bitOffsetAbsolute;
      let classification;
      if (errorTags.has(n.tagName)) classification = IMPACT.BINDING_INVALID;
      else if (rackBlocked) classification = IMPACT.BLOCKED;
      else if (changed) classification = IMPACT.ADDRESS_CHANGED;
      else if (moved) classification = IMPACT.MOVED;
      else if (unresolved) classification = IMPACT.ADDRESS_UNRESOLVED;
      else classification = IMPACT.UNCHANGED;
      return {
        tagName: n.tagName,
        moduleInstanceId: n.moduleInstanceId,
        channel: n.channel,
        oldRackSlot: o ? o.rackSlot : null,
        newRackSlot: n.rackSlot,
        oldAddress: o && o.state === ADDRESS_STATE.DERIVED ? o.bitOffsetAbsolute : null,
        newAddress: n.state === ADDRESS_STATE.DERIVED ? n.bitOffsetAbsolute : null,
        oldState: o ? o.state : null,
        newState: n.state,
        newReasons: [...n.reasons],
        moved,
        addressUnresolved: unresolved,
        classification,
      };
    });

    const blockingStatus = tags.some((t) => t.classification === IMPACT.BINDING_INVALID
      || t.classification === IMPACT.BLOCKED || t.classification === IMPACT.ADDRESS_UNRESOLVED) || rackBlocked
      ? 'BLOCKING' : 'NON_BLOCKING';
    return {
      modules,
      tags,
      validationResult: rackBlocked || after.mappingIssues.some((i) => i.severity === SEVERITY.ERROR) ? LABEL.INVALID : LABEL.VALID,
      blockingStatus,
      activationReady: false,
      activationLabel: LABEL.ACTIVATION_NOT_AUTHORIZED,
    };
  }

  /**
   * Deterministic Draft revision document. It contains no timestamps, no UI
   * state and no default-configuration overwrite. The caller decides where to
   * store it; this module performs no I/O.
   */
  saveDraftRevision() {
    const revisions = this.revisions();
    const document = {
      kind: 'WJSS_MAPPING_DRAFT_REVISION',
      stage: STAGE,
      label: LABEL.DRAFT,
      simulationOnly: true,
      activationAuthorized: false,
      defaultLabel: this.defaultConfig.label,
      modules: this.draft.modules,
      bindings: this.draft.bindings,
      revisions,
    };
    return { text: canonicalJson(document), revisions };
  }
}
