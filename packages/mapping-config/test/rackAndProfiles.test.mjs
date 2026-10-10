// Stage 0.4B-1 — rack topology, Module Profiles and reorder rules.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildModuleInstances, deriveRackView, moveModule, validateRack } from '../src/rack.mjs';
import { getProfile, listProfiles } from '../src/moduleProfiles.mjs';
import { syntheticExampleConfiguration } from '../src/syntheticExample.mjs';

const EXPECTED_MODELS = ['750-362', '750-601', '750-430', '750-530', '750-613', '750-471', '750-554', '750-600'];

test('the Module Profile catalogue covers exactly the eight required models', () => {
  assert.deepEqual(listProfiles().map((p) => p.modelNumber).sort(), [...EXPECTED_MODELS].sort());
});

test('every Module Profile is INCOMPLETE with no address rule', () => {
  for (const p of listProfiles()) {
    assert.equal(p.profileStatus, 'INCOMPLETE', p.modelNumber);
    assert.equal(p.addressRule, null, p.modelNumber);
    assert.ok(p.missing.length > 0, `${p.modelNumber} must list what is unverified`);
  }
});

test('channel capacities match the Excel default topology', () => {
  assert.equal(getProfile('750-430').channelCount, 8);
  assert.equal(getProfile('750-530').channelCount, 8);
  assert.equal(getProfile('750-471').channelCount, 4);
  assert.equal(getProfile('750-554').channelCount, 2);
  assert.equal(getProfile('750-362').channelCount, 0);
  assert.equal(getProfile('750-600').channelCount, 0);
});

test('an unknown model has no profile and cannot enter a rack', () => {
  assert.equal(getProfile('999-999'), null);
  assert.throws(() => buildModuleInstances(['750-362', '999-999']), /no Module Profile/);
});

test('ModuleInstanceIds are assigned per category in rack order', () => {
  const ids = buildModuleInstances(['750-362', '750-601', '750-430', '750-430', '750-471', '750-471', '750-600'])
    .map((m) => m.moduleInstanceId);
  assert.deepEqual(ids, ['COUPLER-01', 'SUPPLY-01', 'DI-MODULE-01', 'DI-MODULE-02', 'AI-MODULE-01', 'AI-MODULE-02', 'END-MODULE-01']);
});

test('RackSlot is 1-based and ProcessModulePosition counts only channel modules, in slot order', () => {
  const cfg = syntheticExampleConfiguration();
  const view = deriveRackView(cfg.modules);
  assert.deepEqual(view.map((r) => r.rackSlot), [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
  assert.deepEqual(view.map((r) => r.processModulePosition),
    [null, null, 1, 2, 3, 4, 5, 6, 7, null]);
  for (const r of view.filter((x) => x.processModulePosition !== null)) {
    assert.equal(r.processModulePositionStatus, 'PROVISIONAL_SLOT_ORDER');
    assert.equal(r.addressStatus, 'ADDRESS_UNRESOLVED');
  }
});

test('the derived rack view is frozen and carries no numeric address field', () => {
  const view = deriveRackView(syntheticExampleConfiguration().modules);
  assert.ok(Object.isFrozen(view[0]));
  for (const r of view) {
    for (const key of Object.keys(r)) {
      assert.doesNotMatch(key, /offset|address(?!Status|Reasons)/i, key);
    }
  }
});

test('the synthetic rack has no structural error and warns on incomplete profiles', () => {
  const cfg = syntheticExampleConfiguration();
  const issues = validateRack(cfg.modules, cfg.declaredChannelCounts);
  assert.equal(issues.filter((i) => i.severity === 'ERROR').length, 0);
  assert.ok(issues.some((i) => i.code === 'MODULE_PROFILE_INCOMPLETE'));
});

test('a declared channel count that differs from the profile is an error', () => {
  const cfg = syntheticExampleConfiguration();
  const issues = validateRack(cfg.modules, { 'AI-MODULE-01': 3 });
  assert.ok(issues.some((i) => i.code === 'CHANNEL_COUNT_MISMATCH' && i.severity === 'ERROR'));
});

test('a rack without a Coupler or without an End Module is refused by validation', () => {
  const modules = buildModuleInstances(['750-601', '750-430', '750-600']);
  const codes = validateRack(modules).map((i) => i.code);
  assert.ok(codes.includes('COUPLER_COUNT'));
  const noEnd = buildModuleInstances(['750-362', '750-430']);
  assert.ok(validateRack(noEnd).map((i) => i.code).includes('END_COUNT'));
});

test('ModuleInstanceId survives a reorder while RackSlot changes', () => {
  const cfg = syntheticExampleConfiguration();
  const before = deriveRackView(cfg.modules).find((r) => r.moduleInstanceId === 'AI-MODULE-03');
  const result = moveModule(cfg.modules, 'AI-MODULE-03', 6);
  assert.equal(result.ok, true);
  const after = deriveRackView(result.modules).find((r) => r.moduleInstanceId === 'AI-MODULE-03');
  assert.equal(after.moduleInstanceId, before.moduleInstanceId);
  assert.notEqual(after.rackSlot, before.rackSlot);
  assert.equal(after.modelNumber, before.modelNumber);
});

test('moveModule returns a new array and leaves the source order untouched', () => {
  const cfg = syntheticExampleConfiguration();
  const original = cfg.modules.map((m) => m.moduleInstanceId).join(',');
  const result = moveModule(cfg.modules, 'DI-MODULE-02', 5);
  assert.equal(result.ok, true);
  assert.notEqual(result.modules, cfg.modules);
  assert.equal(cfg.modules.map((m) => m.moduleInstanceId).join(','), original);
});

test('the Coupler and the End Module are fixed and cannot be reordered', () => {
  const cfg = syntheticExampleConfiguration();
  const coupler = moveModule(cfg.modules, 'COUPLER-01', 3);
  assert.equal(coupler.ok, false);
  assert.equal(coupler.refusal.code, 'REORDER_REFUSED_FIXED_MODULE');
  const end = moveModule(cfg.modules, 'END-MODULE-01', 0);
  assert.equal(end.refusal.code, 'REORDER_REFUSED_FIXED_MODULE');
});

test('moving a module ahead of the Coupler is refused', () => {
  const cfg = syntheticExampleConfiguration();
  const r = moveModule(cfg.modules, 'SUPPLY-01', 0);
  assert.equal(r.ok, false);
  assert.equal(r.refusal.code, 'COUPLER_NOT_FIRST');
});

test('moving a module after the End Module is refused', () => {
  const cfg = syntheticExampleConfiguration();
  const r = moveModule(cfg.modules, 'AI-MODULE-01', cfg.modules.length - 1);
  assert.equal(r.ok, false);
  assert.equal(r.refusal.code, 'END_NOT_LAST');
});

test('an out-of-range target and an unknown module are refused without partial state', () => {
  const cfg = syntheticExampleConfiguration();
  assert.equal(moveModule(cfg.modules, 'AI-MODULE-01', 99).refusal.code, 'TARGET_INDEX_OUT_OF_RANGE');
  assert.equal(moveModule(cfg.modules, 'AI-MODULE-99', 2).refusal.code, 'MODULE_NOT_FOUND');
  assert.equal(moveModule(cfg.modules, 'COUPLER-01', 3).modules, null);
});
