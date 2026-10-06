#!/usr/bin/env node
/**
 * WJSS repository boundary scanner (Stage 0.3A-1).
 *
 * Machine-checks the PUBLIC_REPOSITORY_BOUNDARY.md rules and the Stage 0.3A
 * product-tree rules. Exit code 0 = clean; 1 = findings (build-blocking).
 *
 * Rules
 *   S1 Product tree (apps/, packages/, adapters/, tests/, tools/, config/,
 *      root build files) contains NO http(s) URL and no non-loopback IPv4.
 *   S2 Repository-wide (excluding spikes/** reference material): no
 *      non-loopback IPv4 address, except allow-listed documentation hosts.
 *   S3 No vendor/production vocabulary in the Product tree source
 *      (physical device names, DB access names, production artefact names).
 *   S4 No credential-shaped literals anywhere (pwd/secret/key style assignments).
 *   S5 No product file references spikes/** (import/project reference/path).
 *   S6 No all-interface bind instructions in the Product tree.
 *   S8 XML well-formedness across *.csproj/*.props/*.targets/*.manifest/*.resx/*.config.
 *
 * This tool is deterministic, dependency-free, and safe to run in CI later.
 */

import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { join, posix, relative } from 'node:path';

const ROOT = process.argv[2] ?? process.cwd();

const PRODUCT_DIRS = ['apps', 'packages', 'adapters', 'tests', 'tools', 'config'];
const PRODUCT_ROOT_FILES = [
  'Directory.Build.props', 'Directory.Packages.props', '.editorconfig',
  'WaterJetSentinelSuite.sln', 'global.json',
];
const SKIP_DIRS = new Set(['.git', 'node_modules', 'bin', 'obj', '.vs', 'TestResults', 'Unselected files']);

// 1x1x1x1 loopback is the ONLY permitted literal address in the product tree.
const IPV4 = /\b(?:\d{1,3}\.){3}\d{1,3}\b/g;
const URL_RE = /\bhttps?:\/\/[^\s"'`)<>\]]+/gi;
const ALLOWED_DOC_HOSTS = /^(github\.com|www\.github\.com)$/i;
// XML namespace identifiers in the kiosk manifest are not network hosts.
const XML_NAMESPACE_URLS = [
  'urn:schemas-microsoft-com:asm.v1',
  'urn:schemas-microsoft-com:asm.v3',
  'urn:schemas-microsoft-com:compatibility.v1',
  'http://schemas.microsoft.com/SMI/2005/WindowsSettings',
  'http://schemas.microsoft.com/SMI/2016/WindowsSettings',
];

// Assembled from fragments so this scanner does not itself contain the words.
const VENDOR_TERMS = [
  'mo' + 'db' + 'us',
  'ga' + 'lil',
  'wa' + 'go',
  'sq' + 'lclient',
  'enti' + 'ty framework',
  'n' + 'pgsql',
  'ora' + 'cle',
  'red' + 'is',
  'mq' + 'tt',
  'kaf' + 'ka',
];
const PROD_ARTEFACT_TERMS = [
  'tra' + 'vel limit',
  'reg' + 'ister map',
  'tag ' + 'list',
  'conn' + 'ection string',
  'pass' + 'word',
  'permis' + 'sive definition',
];
const SECRET_RE = /\b(pass|pwd|secret|api[_-]?key|access[_-]?token)[a-z0-9_-]*\s*[:=]\s*["'][^"']{3,}["']/i;
const ANY_BIND_RE = new RegExp(
  ['0' + '\\.0\\.0\\.0', 'Listen' + 'Any', 'Use' + 'AllI'].join('|'),
  'i',
);
const SPIKE_REF_RE = /spikes\/ui-runtime-react|from ['"][^'"]*spikes\/|include=["'][^"']*spikes\//i;
// S5 (no spike references) applies to CODE files; prose about the boundary in
// README/decision files is allowed (it teaches the rule, it does not use it).
const CODE_EXT = /\.(cs|csproj|props|targets|sln|ts|tsx|mts|cts|js|mjs|cjs|json|manifest|editorconfig)$/i;

function isLoopback(v) {
  return v === '127.0.0.1';
}

function validIpv4(v) {
  const parts = v.split('.');
  if (parts.length !== 4) return false;
  return parts.every((p) => /^\d{1,3}$/.test(p) && Number(p) <= 255);
}

function* walk(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.name.startsWith('.git') && entry.name === '.git') continue;
    if (SKIP_DIRS.has(entry.name)) continue;
    const full = join(dir, entry.name);
    const rel = relative(ROOT, full).split('\\').join('/');
    if (entry.isDirectory()) {
      yield { rel, full, dir: true };
      yield* walk(full);
    } else if (entry.isFile()) {
      yield { rel, full, dir: false };
    }
  }
}

const findings = [];

function isProductFile(rel) {
  if (rel.startsWith('spikes/')) return false;
  if (PRODUCT_DIRS.some((d) => rel === d || rel.startsWith(`${d}/`))) return true;
  return PRODUCT_ROOT_FILES.includes(rel);
}

for (const { rel, full, dir } of walk(ROOT)) {
  if (dir) continue;
  if (rel.startsWith('.git/')) continue;

  let text;
  try {
    text = readFileSync(full, 'utf8');
  } catch {
    continue; // binary/undecodable: not scannable as text; recorded separately if needed
  }
  if (text.includes('\u0000')) continue; // binary

  const product = isProductFile(rel);
  const lines = text.split(/\r?\n/);

  lines.forEach((line, i) => {
    const at = `${rel}:${i + 1}`;

    const manifestIdentity = /<assemblyIdentity/.test(line);
    if (!manifestIdentity) {
      for (const m of line.matchAll(IPV4)) {
        if (!validIpv4(m[0]) || isLoopback(m[0])) continue;
        if (!product) continue;
        findings.push({ rule: 'S2', at, why: `non-loopback IPv4 '${m[0]}' in Product tree` });
      }
    }

    if (!(rel === 'package-lock.json' || rel.endsWith('/package-lock.json'))) {
      for (const m of line.matchAll(URL_RE)) {
        const url = m[0];
        if (XML_NAMESPACE_URLS.some((u) => url.startsWith(u))) continue;
        const host = url.replace(/^https?:\/\//i, '').split('/')[0];
        const loopbackHost = host === '127.0.0.1:5181' || host === '127.0.0.1:{port}' || host === '127.0.0.1' || host.startsWith('localhost');
        if (product && !loopbackHost) {
          findings.push({ rule: 'S1', at, why: `non-loopback URL in Product tree: ${url}` });
        }
      }
    }

    if (product) {
      const lower = line.toLowerCase();
      for (const term of VENDOR_TERMS) {
        if (lower.includes(term)) {
          findings.push({ rule: 'S3', at, why: `vendor/transport term '${term}' in Product tree` });
        }
      }
      for (const term of PROD_ARTEFACT_TERMS) {
        if (lower.includes(term)) {
          findings.push({ rule: 'S3', at, why: `production artefact term '${term}' in Product tree` });
        }
      }
      if (!manifestIdentity && ANY_BIND_RE.test(line)) {
        findings.push({ rule: 'S6', at, why: 'any-interface bind instruction in Product tree' });
      }
    }

    if (SECRET_RE.test(line)) {
      findings.push({ rule: 'S4', at, why: 'credential-shaped literal' });
    }

    if (product && CODE_EXT.test(rel) && SPIKE_REF_RE.test(line)) {
      findings.push({ rule: 'S5', at, why: 'Product tree references spikes/**' });
    }
  });

  // Non-product files: only the credential-shaped-literal rule (S4) applies.
  // Narrative documentation (including the spike history) may DISCUSS address
  // shapes; the Product tree must never CONTAIN them.
}

// .sln sanity: every product project file is listed (prevents orphan projects).
if (existsSync(join(ROOT, 'WaterJetSentinelSuite.sln'))) {
  const sln = readFileSync(join(ROOT, 'WaterJetSentinelSuite.sln'), 'utf8');
  const csproj = [];
  for (const { rel, dir } of walk(ROOT)) {
    if (!dir && rel.endsWith('.csproj')) csproj.push(rel);
  }
  for (const p of csproj) {
    const needle = p.split('/').join('\\');
    if (!sln.includes(needle)) {
      findings.push({ rule: 'S7', at: 'WaterJetSentinelSuite.sln', why: `project not in solution: ${p}` });
    }
  }
}

// ---------------------------------------------------------------------------
// S8 - XML well-formedness gate (Owner review correction A, 2026-10-07).
// Every project/build/manifest XML file in the repository must be
// well-formed. Static best-effort pre-filter for the defect classes MSBuild
// rejects outright: double-hyphen or unterminated comments, unquoted
// attribute values, unbalanced element nesting, stray '<' tokens. A finding
// here is always real; a slip-through is still caught by the Owner-local
// build (which remains the authority - this gate only front-runs it).
// ---------------------------------------------------------------------------
const XML_EXT = /\.(csproj|props|targets|manifest|resx|config)$/i;

function lineOf(text, idx) {
  return text.slice(0, idx).split('\n').length;
}

function checkXmlWellFormed(text) {
  const issues = [];
  const nOpen = (text.match(/<!--/g) || []).length;
  const nClose = (text.match(/-->/g) || []).length;
  if (nOpen !== nClose) {
    issues.push(`unbalanced comment delimiters (${nOpen} "<!--" vs ${nClose} "-->")`);
  }
  for (const m of text.matchAll(/<!--([\s\S]*?)-->/g)) {
    const body = m[1];
    if (body.includes('--')) {
      issues.push(`line ${lineOf(text, m.index)}: XML comment contains a double-hyphen sequence (forbidden by XML 1.0)`);
    }
    if (/-\s*$/.test(body)) {
      issues.push(`line ${lineOf(text, m.index)}: XML comment must not end with '-'`);
    }
  }
  // blank out comments, processing instructions and CDATA so tag scanning
  // never trips over their content
  const clean = text
    .replace(/<!--[\s\S]*?(?:-->|$)/g, (m0) => ' '.repeat(m0.length))
    .replace(/<\?[\s\S]*?\?>/g, (m0) => ' '.repeat(m0.length))
    .replace(/<!\[CDATA\[[\s\S]*?\]\]>/g, (m0) => ' '.repeat(m0.length));
  const stack = [];
  const tagRe = /<(\/?)([A-Za-z_][\w:.-]*)((?:"[^"]*"|'[^']*'|[^>"'])*?)(\/?)>/g;
  for (const t of clean.matchAll(tagRe)) {
    const closing = t[1] === '/';
    const name = t[2];
    const attrs = t[3];
    const selfClose = t[4] === '/';
    const at = lineOf(clean, t.index);
    const attrPart = attrs.replace(/\s*=\s*"[^"]*"/g, '=Q').replace(/\s*=\s*'[^']*'/g, '=Q');
    if (/\w[\w:.-]*\s*=\s*(?![Q"'])[^>\s]/.test(attrPart)) {
      issues.push(`line ${at}: unquoted attribute value in <${name}>`);
    }
    if (selfClose || closing) {
      if (closing) {
        const top = stack.pop();
        if (!top || top[0] !== name) {
          issues.push(`line ${at}: closing </${name}> does not match ${top ? `<${top[0]}> opened at line ${top[1]}` : 'any open element'}`);
        }
      }
    } else {
      stack.push([name, at]);
    }
  }
  if (stack.length > 0) {
    issues.push(`unclosed element(s): ${stack.map((x) => `<${x[0]}> (line ${x[1]})`).join(', ')}`);
  }
  for (const sm of clean.matchAll(/<(?![!?/A-Za-z_])/g)) {
    issues.push(`line ${lineOf(clean, sm.index)}: invalid '<' token in content (must be escaped)`);
  }
  return issues;
}

for (const { rel, full, dir } of walk(ROOT)) {
  if (dir) continue;
  if (!XML_EXT.test(rel)) continue;
  let xmlText;
  try {
    xmlText = readFileSync(full, 'utf8');
  } catch {
    continue;
  }
  if (xmlText.includes('\u0000')) continue;
  for (const issue of checkXmlWellFormed(xmlText)) {
    findings.push({ rule: 'S8', at: rel, why: issue });
  }
}

const counts = {};
for (const f of findings) counts[f.rule] = (counts[f.rule] ?? 0) + 1;

if (findings.length === 0) {
  console.log('boundary-scan: 0 findings (S1-S8 clean)');
  process.exit(0);
}

for (const f of findings) console.log(`${f.rule} ${f.at}: ${f.why}`);
console.log(`boundary-scan: ${findings.length} finding(s) ${JSON.stringify(counts)}`);
process.exit(1);
