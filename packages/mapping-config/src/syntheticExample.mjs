// Stage 0.4B-1 — SYNTHETIC EXAMPLE configuration.
//
// A generic, obviously fake rack used by the demonstration surface and the
// tests. It is NOT the Excel default, NOT plant data and NOT a production
// configuration. Its arrangement is invented for demonstration and is not
// derived from any workbook. Engineering ranges are UNCONFIGURED except the
// two pump pressure ranges (0–40 bar), which are the Owner-confirmed domain values.

import { createConfiguration } from './draftSession.mjs';
import { buildModuleInstances } from './rack.mjs';
import { listSimulationTags, getTagDef } from './tagCatalogue.mjs';
import { POLARITY, CONTACT } from './constants.mjs';

const MODELS = ['750-362', '750-601', '750-430', '750-430', '750-530', '750-471', '750-471', '750-471', '750-554', '750-600'];

export function syntheticExampleConfiguration() {
  const modules = buildModuleInstances(MODELS);
  const byCategory = (prefix) => modules.filter((m) => m.moduleInstanceId.startsWith(prefix));
  const ai = byCategory('AI-MODULE');
  const di = byCategory('DI-MODULE');
  // Pressure channels: pump inlet, pump outlet, IV1..IV8 across the three AI modules (4 channels each).
  // Every pressure measurement is a separate channel. Pump and IV identities are never shared. No Main Valve
  // pressure is synthesised: no workbook measurement exists for it.
  const pressureSlots = [];
  for (const m of ai) for (let c = 1; c <= 4; c += 1) pressureSlots.push({ moduleInstanceId: m.moduleInstanceId, channel: c });
  const bindings = [];
  const pressureNames = ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE',
    ...[1, 2, 3, 4, 5, 6, 7, 8].map((n) => `IV${n}_OUTLET_PRESSURE`)];
  pressureNames.forEach((name, i) => {
    const at = pressureSlots[i];
    const def = getTagDef(name);
    const range = def.confirmedEngineeringRange;
    // The synthetic workbook tag of each pressure row: the pump identifiers are the Owner's; IVn is EX-AI-(n+2).
    const n = /^IV([1-8])_/.exec(name)?.[1];
    bindings.push({
      tagName: name,
      moduleInstanceId: at.moduleInstanceId,
      channel: at.channel,
      enabled: true,
      engineering: range ? { min: range.min, max: range.max, unit: range.unit } : null,
      activePolarity: null,
      contactType: null,
      declaredSourceIdentity: def.sourceIdentity,
      sourceWorkbookTag: def.workbookTag ?? (n ? `EX-AI-${String(Number(n) + 2).padStart(2, '0')}` : undefined),
    });
  });
  // Digital limits: 16 channels across two DI modules.
  const digitalSlots = [];
  for (const m of di) for (let c = 1; c <= 8; c += 1) digitalSlots.push({ moduleInstanceId: m.moduleInstanceId, channel: c });
  listSimulationTags().filter((t) => t.channelType === 'DIGITAL').forEach((t, i) => {
    const at = digitalSlots[i];
    const [, n, group] = /^IV([1-8])_(LOWER|UPPER)_LIMIT$/.exec(t.tagName);
    bindings.push({
      tagName: t.tagName,
      moduleInstanceId: at.moduleInstanceId,
      channel: at.channel,
      enabled: true,
      engineering: null,
      activePolarity: POLARITY.ACTIVE_WHEN_CLOSED,
      contactType: CONTACT.NO,
      declaredSourceIdentity: t.tagName,
      sourceWorkbookTag: `EX-DI-${String(group === 'LOWER' ? Number(n) : Number(n) + 8).padStart(2, '0')}`,
    });
  });
  bindings.sort((a, b) => a.tagName.localeCompare(b.tagName));
  return createConfiguration({
    label: 'SYNTHETIC EXAMPLE',
    modules,
    bindings,
    declaredChannelCounts: {},
    additionalTags: {},
  });
}
