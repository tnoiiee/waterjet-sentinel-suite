// Test helper - one VALID H3-A Cleaning Job Workspace document, shaped exactly like the Runtime's
// projection contract. It is test input for the Preview transport seam, never a second decision
// authority: no delay is computed here, the numbers are the frozen values a Runtime would publish.

const STAGES = [
  ['PRECHECK', 'Precheck'],
  ['MOVE_TO_P1', 'Move to P1'],
  ['OPEN_IV', 'Open IV'],
  ['P2', 'P2'],
  ['P3', 'P3'],
  ['P4', 'P4'],
  ['P5', 'P5'],
  ['P6', 'P6'],
  ['CLOSE_IV', 'Close IV'],
  ['MANDATORY_SAFE_RETURN', 'Mandatory Safe Return'],
  ['COMPLETE', 'Complete'],
  ['NEXT_DISPATCH_COUNTDOWN', 'Next dispatch countdown'],
  ['DISPATCHED', 'Dispatched'],
];

/** The frozen decision of an AUTO_RH_LINEAR_FROZEN countdown (RHTemp mid-range, Upper zone). */
export function frozenAutoDecision(overrides = {}) {
  return {
    decisionId: 'SYN-TD-1',
    countdownGeneration: 1,
    queueGeneration: 7,
    queueEntryId: 'SYN-QE-2',
    nextSensorId: 'G+202',
    nextWall: 'LEFT',
    nextSensorOrderWall: 2,
    canonicalWaterJetId: 'WJ1',
    effectiveWaterJetId: 'WJ1',
    effectiveWaterJetProvenance: 'CANONICAL_ASSIGNMENT',
    isolationValveId: 'IV1',
    cabinetId: 'WSB1',
    controllerId: 'MC-WSB1',
    horizontalAxisId: 'A',
    verticalAxisId: 'B',
    rhInput: {
      rhTemperature: 525,
      quality: 'GOOD',
      observedAt: '2026-10-08T00:00:18.000Z',
      generation: 19,
    },
    boilerCondition: 'VERY_CLEAN',
    mode: 'AUTO_RH_LINEAR_FROZEN',
    configurationVersion: 'SIM-H3A-LEGACY-SEED-1',
    manualDelaySeconds: 0,
    rhTempMin: 515,
    rhTempMax: 535,
    rhInputWindowSeconds: 30,
    autoDelayMinSeconds: 600,
    autoDelayMaxSeconds: 660,
    baseDelaySeconds: 630,
    zone: 'UPPER',
    zoneFactor: 0.75,
    appliedDelaySeconds: 473,
    createdAt: '2026-10-08T00:00:18.000Z',
    ...overrides,
  };
}

/** The frozen decision of a MANUAL_FIXED countdown: no RH reading and a 1.00 zone factor. */
export function frozenManualDecision(overrides = {}) {
  return frozenAutoDecision({
    decisionId: 'SYN-TD-2',
    countdownGeneration: 2,
    rhInput: null,
    mode: 'MANUAL_FIXED',
    manualDelaySeconds: 5,
    baseDelaySeconds: 5,
    zone: 'LOWER',
    zoneFactor: 1,
    appliedDelaySeconds: 5,
    ...overrides,
  });
}

/** The configuration section of the simulation surface. */
export function timeDelayConfiguration(overrides = {}) {
  return {
    mode: 'AUTO_RH_LINEAR_FROZEN',
    manualDelaySeconds: 0,
    rhTempMin: 515,
    rhTempMax: 535,
    rhInputWindowSeconds: 30,
    activeBoilerCondition: 'VERY_CLEAN',
    boilerRanges: [
      { condition: 'VERY_CLEAN', autoDelayMinSeconds: 600, autoDelayMaxSeconds: 660 },
      { condition: 'NORMAL_CLEAN', autoDelayMinSeconds: 500, autoDelayMaxSeconds: 550 },
      { condition: 'MEDIUM_DIRTY', autoDelayMinSeconds: 340, autoDelayMaxSeconds: 380 },
      { condition: 'VERY_DIRTY', autoDelayMinSeconds: 240, autoDelayMaxSeconds: 280 },
    ],
    configurationVersion: 'SIM-H3A-LEGACY-SEED-1',
    ...overrides,
  };
}

function timeline(stage) {
  const index = STAGES.findIndex(([id]) => id === stage);
  return {
    stage,
    steps: STAGES.map(([id, label], position) => ({
      stage: id,
      ordinal: position + 1,
      label,
      state: position < index ? 'COMPLETE' : position === index ? 'CURRENT' : 'PENDING',
    })),
    lastDispatchJobId: 'SYN-JOB-1',
    lastDispatchedAt: '2026-10-08T00:00:12.000Z',
  };
}

/** One valid workspace document, with deep, explicit overrides for the sections a test varies. */
export function dispatchWorkspaceDocument({
  revision = 12,
  countdown = {},
  configuration = timeDelayConfiguration(),
  stage = 'NEXT_DISPATCH_COUNTDOWN',
  entries = [
    { position: 1, entryId: 'SYN-QE-2', sensorId: 'G+202', sourceReason: 'AUTO_SEQUENCE', secondsSinceLastClean: 41 },
    { position: 2, entryId: 'SYN-QE-3', sensorId: 'G+203', sourceReason: 'AUTO_SEQUENCE', secondsSinceLastClean: 7 },
  ],
  activeJob = null,
  preview = {},
  queue = {},
} = {}) {
  return {
    kind: 'dispatch-workspace',
    schema: 'wjss.dispatch-workspace/1',
    apiVersion: 1,
    revision,
    generatedAt: '2026-10-08T00:00:18.000Z',
    deviceProfile: 'SIMULATOR',
    stageMarker: 'STAGE_03A3C_RUNTIME_TOPOLOGY',
    simulation: 'SIMULATION - no Hardware access, no device write, no field verification',
    queue: {
      label: 'GLOBAL', capacity: 8, totalQueued: entries.length, revision: 7, entries,
      autoSequence: 'READY_TO_DISPATCH', lastDispatch: {
        dispatchId: 'SYN-DP-1', queueRevisionBefore: 6, queueRevisionAfter: 7, queueEntryId: 'SYN-QE-1',
        positionBefore: 1, sensorId: 'G+201', sourceReason: 'AUTO_SEQUENCE', jobId: 'SYN-JOB-1',
        origin: 'AUTOSEQUENCE_START', dispatchedAt: '2026-10-08T00:00:12.000Z',
      },
      ...queue,
    },
    activeJob,
    autoSequence: 'READY_TO_DISPATCH',
    mode: 'RUNNING',
    countdown: {
      lifecycle: 'ACTIVE_FROZEN',
      generation: 1,
      remainingSeconds: 473,
      decision: frozenAutoDecision(),
      frozen: true,
      dispatchAdmissible: false,
      supersededReason: null,
      refusalCode: null,
      createdAt: '2026-10-08T00:00:18.000Z',
      completedAt: null,
      dispatchedAt: null,
      ...countdown,
    },
    timeline: timeline(stage),
    configuration,
    preview: {
      available: true,
      affectsExecution: false,
      refusalCode: 'NONE',
      mode: 'AUTO_RH_LINEAR_FROZEN',
      rhTemperature: 517.5,
      rhQuality: 'GOOD',
      rhObservedAt: '2026-10-08T00:00:20.000Z',
      rhGeneration: 21,
      clampedRhTemperature: 517.5,
      mappedDelaySeconds: 652.5,
      baseDelaySeconds: 652,
      zone: 'UPPER',
      zoneFactor: 0.75,
      appliedDelaySeconds: 489,
      boilerCondition: 'VERY_CLEAN',
      ...preview,
    },
  };
}
