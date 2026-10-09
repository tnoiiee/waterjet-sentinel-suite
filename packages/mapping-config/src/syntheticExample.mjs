// Stage 0.4B-1 — SYNTHETIC EXAMPLE configuration.
//
// A generic, obviously fake rack used by the demonstration surface and the
// tests. It is NOT the Excel default, NOT plant data and NOT a production
// configuration. Its arrangement is invented for demonstration and is not
// derived from any workbook. Engineering ranges are UNCONFIGURED except the
// pump range, which is the Owner-confirmed domain value.

import { createConfiguration } from './draftSession.mjs';
import { buildModuleInstances } from './rack.mjs';
import { listSimulationTags } from './tagCatalogue.mjs';
import { POLARITY, CONTACT } from './constants.mjs';

const MODELS = ['750-362', '750-601', '750-430', '750-430', '750-530', '750-471', '750-471', '750-471', '750-554', '750-600'];

export function syntheticExampleConfiguration() {
  const modules = buildModuleInstances(MODELS);
  const byCategory = (prefix) => modules.filter((m) => m.moduleInstanceId.startsWith(prefix));
  const ai = byCategory('AI-MODULE');
  const di = byCategory('DI-MODULE');
  // Pressure channels: pump, main valve, IV1..IV8 across the three AI modules (4 channels each).
  const pressureSlots = [];
  for (const m of ai) for (let c = 1; c <= 4; c += 1) pressureSlots.push({ moduleInstanceId: m.moduleInstanceId, channel: c });
  const bindings = [];
  const pressureNames = ['PUMP_OUTLET_PRESSURE', 'MAIN_VALVE_OUTLET_PRESSURE',
    ...[1, 2, 3, 4, 5, 6, 7, 8].map((n) => `IV${n}_OUTLET_PRESSURE`)];
  const sourceFor = (name) => (name === 'PUMP_OUTLET_PRESSURE' ? 'PUMP_OUTLET'
    : name === 'MAIN_VALVE_OUTLET_PRESSURE' ? 'MAIN_VALVE_OUTLET' : name.replace('_PRESSURE', ''));
  pressureNames.forEach((name, i) => {
    const at = pressureSlots[i];
    bindings.push({
      tagName: name,
      moduleInstanceId: at.moduleInstanceId,
      channel: at.channel,
      enabled: true,
      engineering: name === 'PUMP_OUTLET_PRESSURE' ? { min: 0, max: 40, unit: 'bar' } : null,
      activePolarity: null,
      contactType: null,
      declaredSourceIdentity: sourceFor(name),
    });
  });
  // Digital limits: 16 channels across two DI modules.
  const digitalSlots = [];
  for (const m of di) for (let c = 1; c <= 8; c += 1) digitalSlots.push({ moduleInstanceId: m.moduleInstanceId, channel: c });
  listSimulationTags().filter((t) => t.channelType === 'DIGITAL').forEach((t, i) => {
    const at = digitalSlots[i];
    bindings.push({
      tagName: t.tagName,
      moduleInstanceId: at.moduleInstanceId,
      channel: at.channel,
      enabled: true,
      engineering: null,
      activePolarity: POLARITY.ACTIVE_WHEN_CLOSED,
      contactType: CONTACT.NO,
      declaredSourceIdentity: t.tagName,
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

