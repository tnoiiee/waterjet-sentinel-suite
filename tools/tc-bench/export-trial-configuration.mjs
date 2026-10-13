// Deterministic, offline build projection. No endpoint input and no network imports.
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CANONICAL_TC_NODE_CONFIGURATION, canonicalTcNodeConfigurationText, validateTcNodeConfiguration,
} from '../../packages/commissioning/src/tcNodeConfiguration.mjs';

export const digest = (bytes) => createHash('sha256').update(bytes).digest('hex');
const sourcePath = fileURLToPath(new URL('../../packages/commissioning/src/tcNodeConfiguration.mjs', import.meta.url));

export function exportCatalog(configuration = CANONICAL_TC_NODE_CONFIGURATION) {
  const validation = validateTcNodeConfiguration(configuration);
  if (!validation.valid) throw new Error('CANONICAL_CONFIGURATION_REJECTED');
  // Validation alone allows no second authority: require the exact committed canonical value.
  const canonical = canonicalTcNodeConfigurationText(configuration);
  if (canonical !== canonicalTcNodeConfigurationText(CANONICAL_TC_NODE_CONFIGURATION)) {
    throw new Error('CANONICAL_IDENTITY_MISMATCH');
  }
  const node = configuration.nodes.find((n) => n.nodeId === 'TC-NODE-01');
  if (!node || node.cabinetId !== 'CAB1') throw new Error('NODE_CABINET_MISMATCH');
  const trial = {
    trialId: 'T01', nodeId: node.nodeId, cabinetId: node.cabinetId, address: node.ipAddress,
    port: 502, functionCode: 3, unitId: 0, start: 0, quantity: 40, endianness: 'BigEndian',
    connectionSettingBasis: 'OWNER_AUTHORIZED_BENCH_SETTING', physicalVerification: 'NOT_VERIFIED',
    executionAuthorized: false,
  };
  const manifest = JSON.stringify(trial) + '\n';
  const catalog = {
    schemaVersion: 'wjss.tc-bench-catalog/1',
    // Normalized UTF-8/LF source hash is reproducible across worktree line-ending conversions.
    sourceSha256: digest(readFileSync(sourcePath, 'utf8').replace(/\r\n/g, '\n')),
    configurationSha256: digest(canonical), trials: [{ manifestSha256: digest(manifest), authorizationSha256: null, trial }],
  };
  const bytes = JSON.stringify(catalog) + '\n';
  return Object.freeze({ bytes, sha256: digest(bytes), manifest, manifestSha256: digest(manifest) });
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  const output = exportCatalog();
  if (args.length === 0) process.stdout.write(output.bytes);
  else if (args.length === 2 && args[0] === '--build-output') {
    const target = resolve(args[1]);
    // Never overwrite source/evidence; generated resources belong only in ignored build output.
    if (!target.split(/[\\/]/).includes('obj')) throw new Error('BUILD_OUTPUT_MUST_BE_OBJ');
    mkdirSync(dirname(target), { recursive: true });
    writeFileSync(target, output.bytes, { encoding: 'utf8' });
    writeFileSync(target + '.sha256', output.sha256 + '\n', { encoding: 'utf8' });
  } else throw new Error('UNAPPROVED_EXPORT_ARGUMENT');
}
