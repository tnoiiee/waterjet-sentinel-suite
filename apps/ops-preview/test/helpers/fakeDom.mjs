// Test helper - a stub DOM that runs the real public/app.mjs against the real Preview server.
// It exercises the application logic and the rendered text; it does not exercise layout, CSS or a
// real browser, and no browser behaviour is claimed from it.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { register } from 'node:module';
import { createPreviewServer } from '../../server.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
export const html = readFileSync(join(HERE, '..', '..', 'public', 'index.html'), 'utf8');

export class FakeNode {
  constructor(tag) {
    this.tagName = tag; this.attributes = {}; this.childNodes = []; this.listeners = {};
    this.textContent = ''; this.disabled = false; this.className = ''; this.title = '';
    this.hidden = false; this.value = '';
    this.classList = { add() {}, remove() {} };
  }
  setAttribute(k, v) { this.attributes[k] = String(v); }
  getAttribute(k) { return this.attributes[k] ?? null; }
  append(...nodes) { this.childNodes.push(...nodes); }
  replaceChildren(...nodes) { this.childNodes = nodes; this.textContent = ''; }
  addEventListener(t, f) { (this.listeners[t] ??= []).push(f); }
  focus() {}
}

export class FakeText extends FakeNode {
  constructor(t) { super('#text'); this.textContent = t; this.data = t; }
}

export const allText = (node) => (node instanceof FakeText ? node.textContent : node.childNodes.map(allText).join(' '));
export const walk = (node, visit) => { visit(node); for (const c of node.childNodes) walk(c, visit); };
export const fire = (node, type, evt = {}) => { for (const f of node.listeners[type] ?? []) f({ preventDefault() {}, ...evt }); };
export const click = (node) => fire(node, 'click');

/** Finds the first node carrying a data-action attribute, anywhere under `root`. */
export function byAction(root, action) {
  let found = null;
  walk(root, (node) => {
    if (!found && node.attributes?.['data-action'] === action) found = node;
  });
  return found;
}

/** Finds the first button/filter carrying a data-filter attribute. */
export function byFilter(root, key) {
  let found = null;
  walk(root, (node) => {
    if (!found && node.attributes?.['data-filter'] === key) found = node;
  });
  return found;
}

export function rowsOf(byId, id) { return byId[id].childNodes; }

/** Waits until `predicate()` is true, or fails with a clear message. Never used to hide a defect. */
const DEFAULT_WAIT_MS = 20000;

export async function waitFor(predicate, message = 'condition', timeoutMs = DEFAULT_WAIT_MS) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (predicate()) return true;
    await new Promise((r) => setTimeout(r, 10));
  }
  throw new Error(`timed out waiting for ${message}`);
}

export function cellTexts(row) { return row.childNodes.map((td) => allText(td)); }

/** Index of a column by its header label, so tests never hard-code an ordinal. */
export function columnIndex(headerLabels, label) {
  const index = headerLabels.indexOf(label);
  if (index === -1) throw new Error(`no column labelled '${label}'`);
  return index;
}

/**
 * Boots the real application against a real Preview server on an ephemeral loopback port.
 * Returns { byId, requests, close }, where `requests` records every request the app made.
 */
export async function bootApp({ waitId = 'monitor-table-body', extraWindow = null } = {}) {
  const realFetch = globalThis.fetch;
  const byId = {};
  const requests = [];
  register(new URL('./pkgResolveHook.mjs', import.meta.url));
  for (const m of html.matchAll(/\bid="([^"]+)"/g)) byId[m[1]] = new FakeNode(m[1]);
  globalThis.Node = FakeNode;
  globalThis.document = {
    getElementById: (id) => (byId[id] ??= new FakeNode(id)),
    createElement: (tag) => new FakeNode(tag),
    createTextNode: (t) => new FakeText(t),
    querySelector: () => null,
    body: new FakeNode('body'),
    activeElement: null,
  };
  const server = createPreviewServer();
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  globalThis.fetch = (path, opts = {}) => {
    requests.push({ path: String(path), method: opts.method ?? 'GET' });
    return realFetch(`${origin.origin}${path}`, opts);
  };
  if (extraWindow) globalThis.window = extraWindow;
  await import('../../public/app.mjs');
  for (let i = 0; i < 400 && byId[waitId].childNodes.length === 0; i += 1) {
    await new Promise((r) => setTimeout(r, 10));
  }
  return {
    byId,
    requests,
    origin: origin.origin,
    close: () => {
      server.close();
      globalThis.fetch = realFetch;
      if (extraWindow) delete globalThis.window;
    },
  };
}
