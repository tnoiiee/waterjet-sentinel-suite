// Stage 0.4C-K1 - Owner Manual Review correction: the 1920x1080 width-fit contract.
//
// These are SOURCE and STRUCTURE contracts, not rendering checks. A stub DOM does not lay out, so
// nothing here proves how the page looks; what it proves is that every width rule that can produce a
// page-level horizontal overflow is absent or explicitly bounded, that the one intentionally wide
// element (the 20-column Overview table) is contained in its own scroll frame, and that the
// documented launch command exists. The Owner-local fullscreen visual review remains the validation
// of record (apps/kiosk/README.md).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { bootApp } from './helpers/fakeDom.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const APP = join(HERE, '..');
const REPO = join(APP, '..', '..');

const cssSource = readFileSync(join(APP, 'public', 'styles.css'), 'utf8');
const css = cssSource.replace(/\/\*[\s\S]*?\*\//g, '');
const html = readFileSync(join(APP, 'public', 'index.html'), 'utf8');
const packageJson = JSON.parse(readFileSync(join(APP, 'package.json'), 'utf8'));

/** Declarations of the first rule whose selector list contains the given selector, exactly. */
function ruleBody(source, selector) {
  const withoutComments = source.replace(/\/\*[\s\S]*?\*\//g, '');
  for (const match of withoutComments.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const selectors = match[1].split(',').map((s) => s.trim());
    if (selectors.includes(selector)) return match[2];
  }
  return null;
}

const hasDeclaration = (body, declaration) =>
  body !== null && body.replace(/\s+/g, ' ').toLowerCase().includes(declaration.toLowerCase());

test('the page itself can never scroll horizontally', () => {
  const htmlRule = ruleBody(css, 'html');
  const bodyRule = ruleBody(css, 'body');
  assert.notEqual(htmlRule, null, 'an html rule must exist');
  assert.notEqual(bodyRule, null, 'a body rule must exist');
  assert.ok(hasDeclaration(htmlRule, 'overflow-x:hidden') || hasDeclaration(bodyRule, 'overflow-x:hidden'));
  assert.ok(hasDeclaration(htmlRule, 'max-width:100%'));
  assert.ok(hasDeclaration(bodyRule, 'max-width:100%'));
  // No viewport-relative width anywhere: 100vw plus padding is a classic overflow source.
  assert.equal(/100vw/.test(css), false, 'no 100vw width may be used');
  assert.equal(/width\s*:\s*100vw/i.test(css), false);
});

test('the main grid column is bounded so a wide child cannot size the page', () => {
  const mainRule = ruleBody(css, 'main');
  assert.notEqual(mainRule, null);
  assert.match(mainRule, /grid-template-columns\s*:\s*minmax\(0,\s*1fr\)/);
  const sectionRule = ruleBody(css, 'section');
  assert.notEqual(sectionRule, null);
  assert.match(sectionRule, /min-width\s*:\s*0/);
});

test('the stylesheet is intact and only the two bounded frames may scroll sideways', () => {
  assert.equal((css.match(/\{/g) ?? []).length, (css.match(/\}/g) ?? []).length, 'the stylesheet must be brace-balanced');
  if (!css.includes('.table-wrap')) {
    assert.fail('the stylesheet must still define the table frame');
  }
  const sideways = [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)]
    .filter(([, , body]) => /overflow(-x)?\s*:\s*(auto|scroll)/.test(body))
    .map(([, selectors]) => selectors.split(',').map((x) => x.trim()));
  const flat = sideways.flat();
  // The Overview table frame and the Commissioning Wall Map frame. Both are bounded (max-height and
  // max-width) so a wide child can never widen the page: the map frame is the second and final
  // bounded frame, and no other element may scroll sideways.
  assert.deepEqual(flat.sort(), ['.table-wrap', '.table-wrap.scroll', '.wallmap-frame'].sort(),
    'only the table frame and the Wall Map frame may scroll sideways');
  // The page itself never scrolls: it is hidden, not auto.
  assert.equal(/html\s*,[^}]*overflow(-x)?\s*:\s*(auto|scroll)/.test(css), false);
});

test('the Commissioning Wall Map is bounded, fixed-geometry and cannot widen the page', () => {
  // The map frame is the second bounded frame: it owns any scrolling the map needs.
  const frame = ruleBody(css, '.wallmap-frame');
  assert.notEqual(frame, null, 'the Wall Map frame rule must exist');
  assert.match(frame, /overflow\s*:\s*auto/);
  assert.match(frame, /max-height\s*:/);
  assert.match(frame, /max-width\s*:\s*100%/);
  assert.match(frame, /min-width\s*:\s*0/);
  // The map itself is a fixed-geometry block: max-content tracks with fixed-size cells, so it sizes
  // to its own content and never to the viewport, and the frame above contains the excess.
  const map = ruleBody(css, '.wallmap');
  assert.notEqual(map, null, 'the Wall Map grid rule must exist');
  assert.match(map, /width\s*:\s*max-content/);
  assert.match(map, /grid-template-columns\s*:\s*max-content/);
  assert.match(map, /grid-template-rows\s*:\s*max-content/);
  // Cell geometry is a fixed pixel token in both densities, in the approved compact range, and no
  // viewport-relative width is involved anywhere in the map.
  const root = ruleBody(css, ':root');
  assert.match(root, /--wj-cell-w\s*:\s*\d+px/);
  assert.match(root, /--wj-cell-h\s*:\s*\d+px/);
  const compact = ruleBody(css, '[data-density="compact"]');
  assert.match(compact, /--wj-cell-w\s*:\s*\d+px/);
  assert.match(compact, /--wj-cell-h\s*:\s*\d+px/);
  assert.equal(/100vw/.test(css), false, 'no viewport-relative width may be used');
  // The approved U geometry: Rear on top, Front at the bottom, Left and Right spanning the sides
  // vertically centred, compact centre - the presentation of Stage 0.2.1A.
  const placement = (selector) => ruleBody(css, selector) ?? '';
  assert.match(placement('.wj-band-rear'), /grid-area\s*:\s*1\s*\/\s*2/);
  assert.match(placement('.wj-band-front'), /grid-area\s*:\s*3\s*\/\s*2/);
  for (const side of ['.wj-band-left', '.wj-band-right']) {
    const body = placement(side);
    assert.match(body, /grid-area\s*:\s*1\s*\/\s*[13]\s*\/\s*span\s*3/, `${side} must span the full height of the U`);
    assert.match(body, /align-self\s*:\s*center/, `${side} must be vertically centred like the approved side walls`);
  }
  assert.match(ruleBody(css, '.wallmap-hollow'), /grid-area\s*:\s*2\s*\/\s*2/);
  // Wall grids keep the approved 6-deep geometry with 4 / 4 and 5 / 5 wall columns.
  assert.match(css, /\.wj-band\[data-wall="LEFT"\] \.wj-cells, \.wj-band\[data-wall="RIGHT"\] \.wj-cells \{ grid-template-columns:repeat\(4, var\(--wj-cell-w\)\); \}/);
  assert.match(css, /\.wj-band\[data-wall="REAR"\] \.wj-cells, \.wj-band\[data-wall="FRONT"\] \.wj-cells \{ grid-template-columns:repeat\(5, var\(--wj-cell-w\)\); \}/);
  // The map lives inside its frame in the served page, and nothing on the page carries an inline
  // style (the content security policy allows none).
  const frameIndex = html.indexOf('id="wallmap-frame"');
  const mapIndex = html.indexOf('id="wallmap"');
  assert.ok(frameIndex > 0 && mapIndex > frameIndex, 'the Wall Map must sit inside its bounded frame');
  assert.equal(/style="/.test(html), false, 'the served page must contain no inline style attribute');
});

test('the Overview table is contained in its own scroll frame', () => {
  const wrap = ruleBody(css, '.table-wrap');
  assert.notEqual(wrap, null);
  assert.match(wrap, /overflow-x\s*:\s*auto/);
  assert.match(wrap, /max-width\s*:\s*100%/);
  assert.match(wrap, /min-width\s*:\s*0/);
  const scrollFrame = ruleBody(css, '.table-wrap.scroll');
  assert.notEqual(scrollFrame, null, 'the bounded scroll frame must exist');
  assert.match(scrollFrame, /overflow\s*:\s*auto/);
  assert.match(scrollFrame, /max-height\s*:/);
  // The table has no fixed/intrinsic width of its own and no min-width that could beat the frame.
  const tableRule = ruleBody(css, 'table');
  assert.notEqual(tableRule, null);
  assert.equal(/min-width\s*:\s*\d+px/.test(tableRule), false);
  // Structure: the 20-column table lives INSIDE the scroll frame, never as a direct child of a card.
  const wrapIndex = html.indexOf('id="monitor-wrap"');
  const tableIndex = html.indexOf('id="monitor-table"');
  assert.ok(wrapIndex > 0 && tableIndex > wrapIndex, 'the table must sit inside the scroll frame');
  assert.match(html, /<div class="table-wrap scroll" id="monitor-wrap">/);
  assert.ok(html.indexOf('</div>', tableIndex) > tableIndex, 'the frame must enclose the table');
});

test('every flex or grid container that can hold wide content may shrink and wrap', () => {
  const expectations = [
    ['.banner', /flex-wrap\s*:\s*wrap/],
    ['.banner-actions', /flex-wrap\s*:\s*wrap/],
    ['.chips', /flex-wrap\s*:\s*wrap/],
    ['.sections', /flex-wrap\s*:\s*wrap/],
    ['.viewbar', /flex-wrap\s*:\s*wrap/],
    ['.view-controls', /flex-wrap\s*:\s*wrap/],
    ['.toolbar', /flex-wrap\s*:\s*wrap/],
    ['.filters', /flex-wrap\s*:\s*wrap/],
    ['.legend', /flex-wrap\s*:\s*wrap/],
  ];
  for (const [selector, pattern] of expectations) {
    const body = ruleBody(css, selector);
    assert.notEqual(body, null, `${selector} rule must exist`);
    assert.match(body, pattern, `${selector} must wrap rather than overflow`);
  }
  for (const selector of ['.banner .title', '.banner-actions', '.chips', '.sections', '.view-controls', '.detail-card', '.detail-grid', '.table-wrap', '.toolbar', '.wallmap-frame', '.wj-band', '.wallmap-legend-item', '.wj-m']) {
    const body = ruleBody(css, selector);
    assert.notEqual(body, null, `${selector} rule must exist`);
    assert.match(body, /min-width\s*:\s*0/, `${selector} must carry min-width:0`);
  }
  // Auto-fit grids may never demand more than the width they are given.
  assert.match(ruleBody(css, '.summary'), /minmax\(min\(150px,\s*100%\),\s*1fr\)/);
  assert.match(ruleBody(css, '.detail-grid'), /minmax\(min\(320px,\s*100%\),\s*1fr\)/);
  assert.match(ruleBody(css, '.detail-card dl'), /minmax\(0,\s*180px\)\s*minmax\(0,\s*1fr\)/);
  // The modal and the toolbar children stay inside the viewport at any window size.
  assert.match(ruleBody(css, '.modal'), /max-width\s*:\s*min\(620px,\s*calc\(100%\s*-\s*32px\)\)/);
  assert.match(ruleBody(css, '.toolbar > *'), /max-width\s*:\s*100%/);
});

test('the density control and the navigation are inside the wrapping view bar', () => {
  const viewControls = html.slice(html.indexOf('class="view-controls"'));
  assert.match(viewControls.slice(0, 600), /id="density-comfortable"/);
  assert.match(viewControls.slice(0, 600), /id="density-compact"/);
  assert.match(viewControls.slice(0, 600), /Comfortable/);
  assert.match(viewControls.slice(0, 600), /Compact/);
  const nav = html.slice(html.indexOf('class="viewbar"'), html.indexOf('<main>'));
  for (const section of ['Operations', 'Sensor Map', 'Commissioning Monitor', 'Mapping', 'Diagnostics']) {
    assert.ok(nav.includes(`>${section}</a>`), `the navigation must offer ${section}`);
  }
  assert.ok(nav.indexOf('density-comfortable') > nav.indexOf('class="sections"'), 'density sits after the navigation inside the same bar');
});

test('the rendered page keeps both density controls live and the bounded table frame present', async () => {
  // The stub DOM does not lay out and does not build a tree from the served HTML, so this asserts the
  // rendered CONTROLS and their wired state, not their pixel position (that is the Owner review).
  const app = await bootApp({});
  try {
    const comfortable = app.byId['density-comfortable'];
    const compact = app.byId['density-compact'];
    assert.ok(comfortable, 'the Comfortable density control must exist in the rendered page');
    assert.ok(compact, 'the Compact density control must exist in the rendered page');
    assert.equal(comfortable.attributes['aria-pressed'], 'true');
    assert.equal(compact.attributes['aria-pressed'], 'false');
    assert.ok(app.byId['monitor-wrap'], 'the bounded table frame must exist in the rendered page');
    assert.ok(app.byId['monitor-table'], 'the Overview table must exist in the rendered page');
  } finally {
    app.close();
  }
});

test('the Overview table keeps every column inside its own frame contract', () => {
  const headerCells = [...html.matchAll(/<th scope="col">([^<]+)<\/th>/g)].map((m) => m[1]);
  assert.equal(headerCells.length, 20, 'the Overview table must keep its 20 columns');
  assert.equal(headerCells[0], 'Sensor ID');
  assert.equal(headerCells[19], 'Field verification');
  // The wide element is the table; it must be the ONLY things that scroll sideways.
  assert.match(html, /the table scrolls inside its own frame/);
});

test('every documented node/npm command in the runbook and checkpoint exists and works', () => {
  assert.equal(packageJson.scripts.start, 'node server.mjs');
  assert.equal(existsSync(join(APP, 'server.mjs')), true, 'the start command must point at a real file');

  const docs = ['apps/kiosk/README.md', 'docs/STAGE_0.4C-K1_CHECKPOINT.md'];
  let checked = 0;
  for (const doc of docs) {
    const text = readFileSync(join(REPO, doc), 'utf8');
    const commands = (text.match(/^\s*(?:node|npm)\s+[^\n]*$/gm) ?? [])
      .flatMap((line) => line.trim().split('#')[0].split(';'))
      .map((c) => c.trim())
      .filter(Boolean);
    assert.ok(commands.length > 0, `${doc} must document runnable commands`);
    for (const command of commands) {
      // The Owner handoff is documented for PowerShell, so a documented path may use Windows
      // separators; both forms must resolve to the same real file.
      const asPosix = (p) => p.split('\\').join('/');
      const tokens = command.split(/\s+/).map((t) => (t.startsWith('-') ? t : asPosix(t)));
      const tool = tokens[0];
      if (tool === 'node') {
        const args = tokens.slice(1);
        const paths = args[0].startsWith('--') ? args.slice(1).filter((t) => !t.startsWith('-')) : args.filter((t) => !t.startsWith('-'));
        assert.ok(paths.length > 0, `no path in documented command: ${command}`);
        for (const raw of paths) {
          if (raw.includes('*')) {
            const dir = join(REPO, dirname(raw));
            const pattern = raw.slice(raw.lastIndexOf('/') + 1);
            assert.equal(existsSync(dir), true, `documented directory missing for: ${command}`);
            const hit = readdirSync(dir).some((name) => new RegExp(`^${pattern.replace('.', '\\.').replace('*', '.*')}$`).test(name));
            assert.equal(hit, true, `documented glob matched nothing: ${command}`);
          } else {
            assert.equal(existsSync(join(REPO, raw)), true, `documented path missing: ${command} -> ${raw}`);
          }
        }
      } else {
        const prefixAt = tokens.indexOf('--prefix');
        assert.notEqual(prefixAt, -1, `npm command must use --prefix: ${command}`);
        const prefix = tokens[prefixAt + 1];
        const manifestPath = join(REPO, prefix, 'package.json');
        assert.equal(existsSync(manifestPath), true, `documented npm prefix missing: ${command}`);
        const scripts = JSON.parse(readFileSync(manifestPath, 'utf8')).scripts ?? {};
        const rest = tokens.slice(prefixAt + 2);
        const scriptName = rest[0] === 'run' ? rest[1] : rest[0];
        assert.equal(typeof scripts[scriptName], 'string', `documented npm script missing: ${command}`);
        // A `start` script must also point at a file that exists in that package.
        if (scriptName === 'start') {
          const file = (scripts.start.match(/(\S+\.mjs)/) ?? [])[1];
          assert.ok(file, `the start script must name a file: ${scripts.start}`);
          assert.equal(existsSync(join(REPO, prefix, file)), true, `start script file missing: ${file}`);
        }
      }
      checked += 1;
    }
  }
  assert.ok(checked >= 5, `expected several documented commands, checked ${checked}`);
});
