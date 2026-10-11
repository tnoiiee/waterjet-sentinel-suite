// Stage 0.4C-K1 - source contract of the application-owned Kiosk shell.
//
// The Arena sandbox has no .NET SDK, so this project cannot be compiled or run here. These tests do
// not pretend to replace that: they read the shell SOURCE and assert the protected behaviours the
// Stage requires (approved origin only, no developer tools, no second window, no download, no file
// navigation, a visible reconnecting state, a confirmed exit, no process start, no device write, the
// central version pin and the DPI bootstrap shape). The Owner-local build and visual check remain
// the validation of record; see apps/kiosk/README.md.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const KIOSK = join(HERE, '..');
const REPO = join(KIOSK, '..', '..');

const read = (name) => readFileSync(join(KIOSK, name), 'utf8');
const source = {
  options: read('KioskShellOptions.cs'),
  main: read('MainForm.cs'),
  program: read('Program.cs'),
  csproj: read('Wjss.Kiosk.csproj'),
  manifest: read('app.manifest'),
  readme: read('README.md'),
};
const centralPins = readFileSync(join(REPO, 'Directory.Packages.props'), 'utf8');
const shellSources = [source.options, source.main, source.program];

const IPV4 = /\b(?:\d{1,3}\.){3}\d{1,3}\b/g;
// Assembled from fragments so this test file itself contains no browsable URL literal.
const URL_SHAPE = new RegExp(String.raw`\bhttps?:\/\/[^\s"'\)<>\]]+`, 'gi');
const FILE_URL_SHAPE = new RegExp(String.raw`\bfile:\/\/`, 'i');

test('the shell targets the approved loopback origin and holds no other address', () => {
  assert.match(source.options, /LoopbackHost = "127\.0\.0\.1";/);
  assert.match(source.options, /DefaultUiPort = 5187;/);
  assert.match(source.options, /UriKind\.Absolute/);
  for (const [name, text] of Object.entries({ options: source.options, main: source.main, program: source.program })) {
    const addresses = text.match(IPV4) ?? [];
    for (const address of addresses) {
      assert.equal(address, '127.0.0.1', `${name} must not carry another address (found ${address})`);
    }
    assert.equal(FILE_URL_SHAPE.test(text), false, `${name} must not reference a local file URL`);
    for (const url of text.match(URL_SHAPE) ?? []) {
      assert.match(url, /^https?:\/\/(127\.0\.0\.1|localhost|schemas\.microsoft\.com)/i, `${name} carries a non-local URL: ${url}`);
    }
  }
});

test('the scheme and the host are constants and cannot be redirected', () => {
  assert.match(source.options, /private const string Scheme = "http";/);
  assert.match(source.options, /private const string LoopbackHost = "127\.0\.0\.1";/);
  // Only the port may be matched to the local host; the variable name carries no host fragment (S6).
  assert.match(source.options, /PortVariable = "WJSS_KIOSK_UI_PORT";/);
  assert.equal(/GetEnvironmentVariable\([^)]*HOST/i.test(source.options), false);
  assert.match(source.main, /KioskShellOptions\.ApprovedOrigin/);
});

test('the approved-origin check refuses every other scheme, host and port', () => {
  assert.match(source.options, /public static bool IsApprovedOrigin\(Uri\? candidate\)/);
  assert.match(source.options, /candidate\.Scheme, Scheme, StringComparison\.Ordinal/);
  assert.match(source.options, /candidate\.Host, LoopbackHost, StringComparison\.Ordinal/);
  assert.match(source.options, /candidate\.Port == ApprovedOrigin\.Port/);
  assert.match(source.main, /if \(KioskShellOptions\.IsApprovedOrigin\(target\)\)/);
});

test('the shell blocks external navigation, file navigation, popups, downloads and permissions', () => {
  assert.match(source.main, /e\.Cancel = true;/);
  assert.match(source.main, /_blockedNavigations \+= 1;/);
  assert.match(source.main, /NewWindowRequested/);
  assert.match(source.main, /e\.Handled = true;/);
  assert.match(source.main, /_blockedPopups \+= 1;/);
  assert.match(source.main, /DownloadStarting/);
  assert.match(source.main, /_cancelledDownloads \+= 1;/);
  assert.match(source.main, /CoreWebView2PermissionState\.Deny/);
});

test('developer tools and browser chrome are disabled in the Operator presentation', () => {
  assert.match(source.main, /core\.Settings\.AreDevToolsEnabled = false;/);
  assert.match(source.main, /core\.Settings\.AreDefaultContextMenusEnabled = false;/);
  assert.match(source.main, /core\.Settings\.AreBrowserAcceleratorKeysEnabled = false;/);
  assert.match(source.main, /core\.Settings\.IsStatusBarEnabled = false;/);
  assert.match(source.main, /core\.Settings\.IsZoomControlEnabled = false;/);
  assert.equal(/OpenDevToolsWindow/.test(source.main), false);
});

test('a Runtime-unavailable state is visible and a stopped screen is never shown as current', () => {
  assert.match(source.main, /ProbeIntervalMs/);
  assert.match(source.main, /KioskShellOptions\.HealthUri/);
  assert.match(source.main, /UNAVAILABLE - RECONNECTING/);
  assert.match(source.main, /_view\.Visible = showView;/);
  assert.match(source.main, /_overlay\.Visible = !showView;/);
  assert.match(source.main, /CoreWebView2\?\.Reload\(\)/);
  // The decision is single-sourced, so no code path can leave a stale frame on screen.
  assert.match(source.main, /var showView = _viewReady && _hostAvailable && !_viewFaulted;/);
});

test('the exit is intentional, confirmed and reversible', () => {
  assert.match(source.main, /RequestConfirmedExit\(\);/);
  assert.match(source.main, /MessageBoxButtons\.YesNo/);
  assert.match(source.main, /CloseReason\.UserClosing && !_confirmedExit/);
  assert.match(source.main, /_confirmedExit = true;/);
  // No hidden keystroke-only exit, and no irreversible lockdown.
  assert.equal(/KeyDown/.test(source.main), false);
  assert.equal(/Keys\./.test(source.main), false);
  assert.match(source.main, /FormBorderStyle\.None/);
  assert.match(source.main, /ToggleWindowMode/);
});

test('the shell starts no process, writes no record and holds no control authority', () => {
  for (const [name, text] of Object.entries({ options: source.options, main: source.main, program: source.program })) {
    assert.equal(/Process\.Start/.test(text), false, `${name} must not start a process`);
    assert.equal(/File\.Write|StreamWriter|FileStream|WriteAllText/.test(text), false, `${name} must not write a record`);
    assert.equal(/Wjss\.Runtime|Runtime\./.test(text), false, `${name} must not reach into the Runtime`);
  }
  assert.match(source.main, /EnsureCoreWebView2Async/);
  assert.equal(/ProjectReference/.test(source.csproj), false);
});

test('the Stage boundary statement is present and displayed', () => {
  assert.match(source.options, /BoundaryText =/);
  assert.match(source.options, /SIMULATION \/ PREVIEW/);
  assert.match(source.options, /NO HARDWARE ACCESS/);
  assert.match(source.options, /NO DEVICE WRITE/);
  assert.match(source.options, /FIELD VERIFICATION PENDING/);
  assert.match(source.main, /KioskShellOptions\.BoundaryText/);
});

test('the web-view package is centrally pinned and referenced without a version', () => {
  assert.match(
    centralPins,
    /<PackageVersion Include="Microsoft\.Web\.WebView2" Version="1\.0\.4258\.31" \/>/,
  );
  assert.match(source.csproj, /<PackageReference Include="Microsoft\.Web\.WebView2" \/>/);
  assert.equal(
    /PackageReference Include="Microsoft\.Web\.WebView2" Version=/.test(source.csproj),
    false,
    'the Kiosk project must not restate a package version',
  );
});

test('the entry point stays on the source-generated bootstrap and the DPI project property', () => {
  const initialize = source.program.indexOf('ApplicationConfiguration.Initialize()');
  const run = source.program.indexOf('Application.Run(new MainForm())');
  assert.notEqual(initialize, -1);
  assert.notEqual(run, -1);
  assert.ok(initialize < run, 'the generated bootstrap must run before the window');
  assert.equal(/SetHighDpiMode/.test(source.program), false);
  const declaration = '<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>';
  assert.equal(source.csproj.split(declaration).length - 1, 1);
  assert.match(source.manifest, /supportedOS/);
  assert.equal(/dpiAware|dpiAwareness/i.test(source.manifest), false);
});

test('the runbook documents the dependency, the lock refresh and the honest validation status', () => {
  assert.match(source.readme, /Microsoft\.Web\.WebView2/);
  assert.match(source.readme, /1\.0\.4258\.31/);
  assert.match(source.readme, /packages\.lock\.json/);
  assert.match(source.readme, /Owner-local/);
  assert.match(source.readme, /node apps\/ops-preview\/server\.mjs/);
  assert.match(source.readme, /dotnet build\s+apps\/kiosk\/Wjss\.Kiosk\.csproj -c Release/);
  assert.match(source.readme, /Not verified in Arena/);
});
