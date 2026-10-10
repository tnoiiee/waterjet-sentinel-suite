// Stage 0.4B-1 — local, read-only host for the Mapping Configuration UI.
//
//   GET /                      the UI shell
//   GET /app.mjs, /styles.css  UI assets (whitelist)
//   GET /pkg/<module>.mjs      browser-safe modules of packages/mapping-config/src (whitelist)
//   GET /api/configuration     the configuration the UI starts from
//
// Every other method is refused with 405. There is no route that writes, connects,
// polls, reads a device or activates anything. The server binds to 127.0.0.1 only:
// the bind host is a constant, and no environment variable, argument or config can
// change it. The Excel path is optional and local: when MAPPING_EXCEL_DEFAULT_PATH is
// set, the authoritative default bindings load from that workbook with no seed.
// MAPPING_BINDING_SEED_PATH is an optional authorised override and is accepted only
// together with MAPPING_EXCEL_DEFAULT_PATH. Both files must live OUTSIDE the repository.

import { createServer } from 'node:http';
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, extname, isAbsolute, join, relative, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { syntheticExampleConfiguration } from '../../packages/mapping-config/src/syntheticExample.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, '..', '..');
const PUBLIC_DIR = join(HERE, 'public');
// S6: the only bind host. A constant, never read from the environment or arguments.
export const UI_BIND_HOST = '127.0.0.1';
const PKG_SRC = join(REPO, 'packages', 'mapping-config', 'src');
const NODE_ONLY = new Set(['workbookImport.mjs', 'nodeImport.mjs']);
const PUBLIC_FILES = new Set(['index.html', 'app.mjs', 'styles.css']);
const MIME = Object.freeze({
  '.html': 'text/html; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
});
const SECURITY_HEADERS = Object.freeze({
  'Content-Security-Policy': "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'",
  'X-Content-Type-Options': 'nosniff',
  'Referrer-Policy': 'no-referrer',
  'Cache-Control': 'no-store',
});

const BROWSER_MODULES = new Set(readdirSync(PKG_SRC).filter((f) => f.endsWith('.mjs') && !NODE_ONLY.has(f)));

function isInsideRepo(p) {
  const rel = relative(REPO, resolve(p));
  return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel));
}

/**
 * Returns { configuration, importReport, mode }. The synthetic example is the
 * default. MAPPING_EXCEL_DEFAULT_PATH alone loads the authoritative default bindings
 * (26, derived from explicit workbook identifiers) with no seed. MAPPING_BINDING_SEED_PATH
 * is an optional override that requires MAPPING_EXCEL_DEFAULT_PATH.
 * The import report carries counts and issues only; it never carries per-row records.
 */
export async function loadConfiguration(env = process.env) {
  const xlsxPath = env.MAPPING_EXCEL_DEFAULT_PATH;
  const seedPath = env.MAPPING_BINDING_SEED_PATH;
  if (!xlsxPath && !seedPath) {
    const cfg = syntheticExampleConfiguration();
    return { mode: 'SYNTHETIC EXAMPLE', importReport: null, configuration: plain(cfg) };
  }
  if (!xlsxPath) {
    throw new Error('MAPPING_BINDING_SEED_PATH requires MAPPING_EXCEL_DEFAULT_PATH (the seed is an optional override)');
  }
  const inputs = seedPath ? [xlsxPath, seedPath] : [xlsxPath];
  for (const p of inputs) {
    if (isInsideRepo(p)) throw new Error('local Excel inputs must live outside the repository');
  }
  const { importWorkbook } = await import('../../packages/mapping-config/src/nodeImport.mjs');
  const bindingSeed = seedPath ? JSON.parse(readFileSync(seedPath, 'utf8')) : undefined;
  const r = importWorkbook(readFileSync(xlsxPath), bindingSeed === undefined ? {} : { bindingSeed });
  const issueSummary = {};
  for (const i of r.issues) issueSummary[i.code] = (issueSummary[i.code] ?? 0) + 1;
  return {
    mode: 'DEFAULT FROM EXCEL',
    importReport: {
      label: r.label,
      source: r.source,
      seedOverride: seedPath !== undefined,
      counts: r.counts,
      classifications: r.classifications,
      modelCount: r.modules.length,
      slots: r.modules.map((m) => ({ moduleInstanceId: m.moduleInstanceId, modelNumber: m.modelNumber })),
      issueSummary,
      issues: r.issues.map((i) => ({ code: i.code, severity: i.severity, row: i.row ?? null, slot: i.slot ?? null, channel: i.channel ?? null, message: i.message })),
    },
    configuration: plain({
      label: r.label,
      modules: r.modules,
      bindings: r.bindings,
      declaredChannelCounts: r.declaredChannelCounts,
      additionalTags: r.additionalTags,
    }),
  };
}

function plain(cfg) {
  return JSON.parse(JSON.stringify({
    label: cfg.label,
    modules: cfg.modules,
    bindings: cfg.bindings,
    declaredChannelCounts: cfg.declaredChannelCounts ?? {},
    additionalTags: cfg.additionalTags ?? {},
  }));
}

function send(res, status, body, type, extra = {}) {
  res.writeHead(status, { ...SECURITY_HEADERS, 'Content-Type': type, ...extra });
  res.end(body);
}

export function createUiServer({ loader = () => loadConfiguration() } = {}) {
  let cached = null;
  const configOnce = async () => {
    if (!cached) cached = await loader();
    return cached;
  };

  return createServer(async (req, res) => {
    if (req.method !== 'GET' && req.method !== 'HEAD') {
      return send(res, 405, 'method not allowed', 'text/plain; charset=utf-8', { Allow: 'GET, HEAD' });
    }
    const url = new URL(req.url ?? '/', 'http://localhost');
    const path = decodeURIComponent(url.pathname);
    try {
      if (path === '/' || path === '/index.html') {
        return send(res, 200, readFileSync(join(PUBLIC_DIR, 'index.html')), MIME['.html']);
      }
      if (path === '/api/configuration') {
        const { mode, importReport, configuration } = await configOnce();
        return send(res, 200, JSON.stringify({ mode, importReport, configuration }), MIME['.json']);
      }
      if (path.startsWith('/pkg/')) {
        const name = path.slice('/pkg/'.length);
        if (!BROWSER_MODULES.has(name)) return send(res, 404, 'not found', 'text/plain; charset=utf-8');
        return send(res, 200, readFileSync(join(PKG_SRC, name)), MIME[extname(name)]);
      }
      const asset = path.slice(1);
      if (PUBLIC_FILES.has(asset)) {
        return send(res, 200, readFileSync(join(PUBLIC_DIR, asset)), MIME[extname(asset)]);
      }
      return send(res, 404, 'not found', 'text/plain; charset=utf-8');
    } catch (err) {
      // Never echo internal detail to the browser; the local console keeps it.
      console.error('mapping-config:', err.message);
      return send(res, 500, 'configuration unavailable', 'text/plain; charset=utf-8');
    }
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const port = Number(process.env.MAPPING_UI_PORT ?? 5186);
  const server = createUiServer();
  server.listen(port, UI_BIND_HOST, () => {
    // Report the address the socket actually bound, not the requested one.
    const bound = server.address();
    console.log(`Mapping Configuration (SIMULATION ONLY) listening on ${bound.address}:${bound.port}`);
  });
}
