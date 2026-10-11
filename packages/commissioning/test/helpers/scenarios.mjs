// Test helper: scenario observations built over the real parsed evidence schedule.
import { buildObservations as build, getScenario, SCENARIOS } from '../../src/scenarios.mjs';
import { schedule } from './schedule.mjs';

export const buildObservations = (scenarioKey, { tick = 0, nodeId = null } = {}) => build(schedule, { scenarioKey, tick, nodeId });
export const scenarioObservationOf = (observations, channelKey) => observations.get(channelKey);
export const firstScenarioChannel = (observations, predicate) => {
  for (const [key, observation] of observations.entries()) if (predicate(observation)) return key;
  return null;
};
export const scenarioRows = (scenarioKey, ctx) => schedule.rows.filter((r) => getScenario(scenarioKey).appliesTo(r, ctx));
export const observedChannels = (observations, predicate) => [...observations.entries()]
  .filter(([, o]) => predicate(o)).map(([key]) => key);
export const allScenarioKeys = SCENARIOS.map((s) => s.key);
