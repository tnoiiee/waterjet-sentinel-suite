// Test helper — a stub DOM that runs the real public/app.mjs against the real server. Layout is not exercised.
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { register } from 'node:module';
import { createUiServer } from '../../server.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
export const html = readFileSync(join(HERE, '..', '..', 'public', 'index.html'), 'utf8');

export class FakeNode {
  constructor(tag) {
    this.tagName = tag; this.attributes = {}; this.childNodes = []; this.listeners = {};
    this.textContent = ''; this.disabled = false; this.className = ''; this.title = '';
    this.classList = { add() {}, remove() {} };
  }
  setAttribute(k, v) { this.attributes[k] = String(v); }
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

/** Boots the real app. Returns { byId, close }. `loader` supplies the configuration to the real server. */
export async function bootApp({ loader, waitId = 'rack-body' }) {
  const realFetch = globalThis.fetch;
  const byId = {};
  register(new URL('./pkgResolveHook.mjs', import.meta.url));
  for (const m of html.matchAll(/\bid="([^"]+)"/g)) byId[m[1]] = new FakeNode(m[1]);
  globalThis.Node = FakeNode;
  globalThis.document = {
    getElementById: (id) => (byId[id] ??= new FakeNode(id)),
    createElement: (tag) => new FakeNode(tag),
    createTextNode: (t) => new FakeText(t),
    querySelector: () => null,
    activeElement: null,
  };
  const server = createUiServer({ loader });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  globalThis.fetch = (path, opts) => realFetch(`${origin.origin}${path}`, opts);
  await import('../../public/app.mjs');
  for (let i = 0; i < 300 && byId[waitId].childNodes.length === 0; i += 1) await new Promise((r) => setTimeout(r, 10));
  return { byId, close: () => { server.close(); globalThis.fetch = realFetch; } };
}
