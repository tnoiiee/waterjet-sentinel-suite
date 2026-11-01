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
  // The Overview table frame and the Commissioning Wall Map frame. Both carry bounded HORIZONTAL
  // containment (max-width:100% + min-width:0) so a wide child can never widen the page: the map
  // frame is the second and final bounded frame, and no other element may scroll sideways. The map
  // frame deliberately does not cap its height - see the static-vertical-view contract below.
  assert.deepEqual(flat.sort(), ['.table-wrap', '.table-wrap.scroll', '.wallmap-frame'].sort(),
    'only the table frame and the Wall Map frame may scroll sideways');
  // The page itself never scrolls: it is hidden, not auto.
  assert.equal(/html\s*,[^}]*overflow(-x)?\s*:\s*(auto|scroll)/.test(css), false);
});

test('the Commissioning Wall Map is bounded, fixed-geometry and cannot widen the page', () => {
  // The map frame is the second bounded frame, and it is bounded HORIZONTALLY only: the horizontal
  // containment keeps a wide map from widening the page, while the vertical axis is static (the
  // static-vertical-view contract is asserted separately below).
  const frame = ruleBody(css, '.wallmap-frame');
  assert.notEqual(frame, null, 'the Wall Map frame rule must exist');
  assert.match(frame, /overflow-x\s*:\s*auto/);
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

test('the Wall Map frame is a STATIC vertical view: horizontal containment only, never a nested vertical scrollbar', () => {
  // Owner Manual Review correction. The U-shaped map is a spatial reference and must stay visually
  // continuous: the document vertical scroll is the single vertical scrolling surface, and the Wall
  // Map frame must never grow a vertical scrollbar of its own. This test separates the two axes
  // explicitly, because "contained" only ever meant HORIZONTAL containment.
  const frame = ruleBody(css, '.wallmap-frame');
  assert.notEqual(frame, null, 'the Wall Map frame rule must exist');
  const decls = frame.replace(/\s+/g, ' ').toLowerCase();

  // Horizontal containment is retained: it is the only axis the frame may scroll.
  assert.match(decls, /overflow-x\s*:\s*auto/, 'the frame must keep the bounded horizontal containment');
  assert.match(decls, /max-width\s*:\s*100%/);
  assert.match(decls, /min-width\s*:\s*0/);
  assert.ok(/overflow-x\s*:\s*(auto|scroll)/.test(decls), 'horizontal scrolling must be available when the map cannot fit');

  // The vertical axis never scrolls, by declaration.
  assert.equal(/overflow-y\s*:\s*(auto|scroll)/.test(decls), false,
    'the Wall Map frame must not declare a scrolling vertical axis');
  assert.equal(/overflow\s*:/.test(decls), false,
    'the overflow SHORTHAND must not be used on the Wall Map frame: it would scroll both axes');
  // `overflow-y:visible` is deliberately not used. Next to a non-visible `overflow-x` its USED value
  // becomes `auto` (CSS Overflow 3), so the illustrative pairing would silently reintroduce the very
  // nested vertical scrollbar this correction removes. `overflow-y:hidden` is the explicit contract.
  assert.equal(/overflow-y\s*:\s*visible/.test(decls), false,
    'overflow-y:visible next to overflow-x:auto computes to auto - the nested scrollbar would return');
  assert.match(decls, /overflow-y\s*:\s*hidden/, 'the frame must state the no-vertical-scrolling behaviour explicitly');

  // No height cap of any kind: the frame always expands to the map's complete natural height.
  assert.equal(/max-height\s*:/.test(decls), false, 'no max-height may cap the Wall Map frame');
  assert.equal(/(?:^|[;{]\s*)height\s*:/.test(decls), false, 'no fixed height may cap the Wall Map frame');
  // No viewport-relative height either (a vh cap is the same defect in another spelling).
  assert.equal(/\d+\s*vh/.test(decls), false, 'no viewport-relative height may cap the Wall Map frame');

  // The map keeps its complete fixed geometry: nothing may be hidden to avoid the scrollbar.
  const map = ruleBody(css, '.wallmap');
  assert.match(map, /width\s*:\s*max-content/, 'the map must keep its intrinsic fixed geometry');
  assert.equal(/display\s*:\s*none/.test(map), false);
  const cells = ruleBody(css, '.wj-cell');
  assert.match(cells, /height\s*:\s*var\(--wj-cell-h\)/, 'cells keep their approved fixed height');
  assert.match(cells, /width\s*:\s*var\(--wj-cell-w\)/, 'cells keep their approved fixed width');
  // All four walls stay rendered as one continuous map: no wall may be hidden, paged or collapsed.
  assert.notEqual(ruleBody(css, '.wj-band'), null, 'the wall band rule must still exist');
  assert.match(ruleBody(css, '.wj-band'), /display\s*:\s*flex/, 'bands must keep their block layout, never a collapsed one');
  for (const wall of ['LEFT', 'REAR', 'RIGHT', 'FRONT']) {
    assert.notEqual(ruleBody(css, `.wj-band[data-wall="${wall}"] .wj-cells`), null, `${wall} wall must keep its grid`);
  }
  assert.equal(/--wj-cell-h\s*:\s*0/.test(css), false, 'no cell compression may be used to avoid the scrollbar');
  assert.equal(/--wj-cell-w\s*:\s*0/.test(css), false, 'no cell compression may be used to avoid the scrollbar');
  // The map frame is the only element that gained this contract: the page itself still never scrolls
  // horizontally, and the Overview table keeps its own bounded local scroll frame.
  const htmlRule = ruleBody(css, 'html');
  const bodyRule = ruleBody(css, 'body');
  assert.ok(hasDeclaration(htmlRule, 'overflow-x:hidden'));
  assert.ok(hasDeclaration(bodyRule, 'overflow-x:hidden'));
  assert.match(ruleBody(css, '.table-wrap'), /overflow-x\s*:\s*auto/);
  // Structure: only the map is inside the frame, so the controls, the notes and the legend can never
  // be clipped or scrolled away by the frame.
  const frameIndex = html.indexOf('id="wallmap-frame"');
  const mapIndex = html.indexOf('id="wallmap"');
  const noteIndex = html.indexOf('id="wallmap-selection-note"');
  const legendIndex = html.indexOf('id="wallmap-legend"');
  const controlsIndex = html.indexOf('id="wall-prev"');
  assert.ok(frameIndex > 0 && mapIndex > frameIndex, 'the map must sit inside the frame');
  assert.ok(noteIndex > mapIndex && legendIndex > mapIndex, 'the notes and the legend stay outside the frame');
  assert.ok(controlsIndex > 0 && controlsIndex < frameIndex, 'the presentation controls stay above the frame');
  // No second internal viewport: the frame contains the map element and nothing else, so there is no
  // nested scroll surface to hide part of the U-shape behind and no extra wrapper to clip it.
  assert.match(html, /<div class="wallmap-frame" id="wallmap-frame" data-wallmap-selection="NONE" data-measurement-view="BOTH">\s*<div class="wallmap" id="wallmap"><\/div>\s*<\/div>/,
    'the frame must contain only the map element - never an extra scrolling wrapper');
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
  // Scoped to the Overview table on purpose: the page now carries a second, much narrower table
  // (the H3-A GlobalQueue), and only the Overview table owns the 20-column contract.
  const tableStart = html.indexOf('id="monitor-table"');
  const tableEnd = html.indexOf('</table>', tableStart);
  const monitorTable = html.slice(tableStart, tableEnd);
  const headerCells = [...monitorTable.matchAll(/<th scope="col">([^<]+)<\/th>/g)].map((m) => m[1]);
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
