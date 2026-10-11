// Stage 0.4C-K2 - source contract of the application-owned Kiosk shell (originated in Stage 0.4C-K1).
//
// The Arena sandbox has no .NET SDK, so this project cannot be compiled or run here. These tests do
// not pretend to replace that: they read the shell SOURCE and assert the protected behaviours the
// Stage requires (approved origin only, no developer tools, no second window, no download, no file
// navigation, a visible reconnecting state, a confirmed exit, no process start, no device write, the
// central version pin and the DPI bootstrap shape). The Owner-local build and visual check remain
// the validation of record; see apps/kiosk/README.md.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { STAGE_ID } from '../../ops-preview/server.mjs';

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

// ---------------------------------------------------------------------------
// Stage 0.4C-K1 zero-warning correction: the kiosk stays a WinForms-only project
// and the WPF-only WebView2 assembly is isolated from the reference graph.
// ---------------------------------------------------------------------------

const csprojTarget = (name) => {
  const match = new RegExp(`<Target Name="${name}"[^>]*>([\\s\\S]*?)</Target>`).exec(source.csproj);
  return match?.[0] ?? null;
};
const buildProps = readFileSync(join(REPO, 'Directory.Build.props'), 'utf8');

test('the kiosk remains a WinForms-only project and never enables WPF', () => {
  assert.match(source.csproj, /<UseWindowsForms>true<\/UseWindowsForms>/);
  assert.equal(
    /<UseWPF[^>]*>\s*true/i.test(source.csproj),
    false,
    'the WinForms-only kiosk must never enable WPF',
  );
  assert.match(source.csproj, /<TargetFramework>net10\.0-windows<\/TargetFramework>/);
  assert.match(source.csproj, /<OutputType>WinExe<\/OutputType>/);
});

test('the correction removes only the WPF-only assembly, before assembly resolution', () => {
  const target = csprojTarget('RemoveWebView2WpfReference');
  assert.notEqual(target, null, 'the correction target must exist in the kiosk project');
  assert.match(target, /AfterTargets="ResolvePackageAssets"/);
  assert.match(target, /BeforeTargets="ResolveAssemblyReferences"/);
  assert.match(target, /Condition="'%\(Reference\.Filename\)' == 'Microsoft\.Web\.WebView2\.Wpf'"/);
  assert.match(target, /<Reference Remove="@\(_WebView2WpfReference\)" \/>/);
  // The remaining-reference guard turns a silent regression into a loud build failure.
  assert.match(target, /<_WebView2WpfReferenceRemaining/);
  assert.match(target, /<Error Condition="'@\(_WebView2WpfReferenceRemaining\)' != ''"/);
  // Scope: the ONLY reference the project removes is the WPF-only one.
  const removals = [...source.csproj.matchAll(/<Reference Remove="([^"]+)"/g)].map((m) => m[1]);
  assert.deepEqual(removals, ['@(_WebView2WpfReference)']);
  assert.equal(/Remove="[^"]*(WebView2\.Core|WebView2\.WinForms)/.test(source.csproj), false);
  // No hand-written WebView2 or WindowsBase reference: the package supplies the graph.
  assert.equal(/<Reference Include="Microsoft\.Web\.WebView2/i.test(source.csproj), false);
  assert.equal(/<Reference Include="WindowsBase/i.test(source.csproj), false);
  // No path or cache assumption anywhere in the project file.
  assert.equal(source.csproj.includes('.nuget'), false);
  assert.equal(/USERPROFILE/i.test(source.csproj), false);
  assert.equal(/[A-Za-z]:\\/.test(source.csproj), false);
  // No assembly path is ever hand-written: the package supplies every HintPath.
  assert.equal(/HintPath/i.test(source.csproj), false);
});

test('the correction keeps Core and WinForms and proves it in the build', () => {
  const proof = csprojTarget('KioskWebView2ReferenceProof');
  assert.notEqual(proof, null, 'the reference proof target must exist in the kiosk project');
  assert.match(proof, /_KioskProofCore/);
  assert.match(proof, /_KioskProofWinForms/);
  assert.match(proof, /_KioskProofWpf/);
  assert.match(proof, /WJSS-KIOSK-WEBVIEW2 CORE=/);
  assert.match(proof, /WJSS-KIOSK-WEBVIEW2 WINFORMS=/);
  assert.match(proof, /WJSS-KIOSK-WEBVIEW2 WPF=/);
  // A missing Core or WinForms reference fails the proof; it can never pass vacuously.
  assert.match(proof, /<Error Condition="'@\(_KioskProofCore\)' == ''"/);
  assert.match(proof, /<Error Condition="'@\(_KioskProofWinForms\)' == ''"/);
  assert.match(proof, /<Error Condition="'@\(_KioskProofWpf\)' != ''"/);
});

test('the correction suppresses nothing and touches no warning setting', () => {
  // Comments are stripped first: the correction's own explanation names the warning it removes,
  // and explaining a warning is not suppressing it.
  const csprojCode = source.csproj.replace(/<!--[\s\S]*?-->/g, '');
  assert.equal(
    /NoWarn|WarningsNotAsErrors|WarningsAsErrors|MSBuildWarningsAsMessages|TreatWarningsAsErrors/i.test(csprojCode),
    false,
    'the correction must not suppress, downgrade or reconfigure any warning',
  );
  assert.equal(/AutoGenerateBindingRedirects|bindingRedirect/i.test(csprojCode), false);
  // The warning code may appear only in explanatory text that FORBIDS silencing it, never in a setting.
  assert.equal(
    /<(NoWarn|WarningsNotAsErrors|WarningsAsErrors|MSBuildWarningsAsMessages|TreatWarningsAsErrors)[^>]*3277/i.test(source.csproj),
    false,
  );
  assert.equal(existsSync(join(KIOSK, 'App.config')), false, 'no binding-redirect configuration file exists');
  assert.equal(existsSync(join(KIOSK, 'packages.config')), false, 'Central Package Management stays in force');
  // The shared build properties still fail the build on a warning, and no suppression names the code.
  assert.match(buildProps, /<TreatWarningsAsErrors>true<\/TreatWarningsAsErrors>/);
  assert.equal(/3277/.test(buildProps), false);
  assert.equal(/MSB3277/.test(buildProps), false);
});

test('the runbook explains the isolation and its honest verification status', () => {
  assert.match(source.readme, /WinForms-only/);
  assert.match(source.readme, /MSB3277/);
  assert.match(source.readme, /RemoveWebView2WpfReference/);
  assert.match(source.readme, /KioskWebView2ReferenceProof/);
  assert.match(source.readme, /not verified in Arena/i);
});

// ---------------------------------------------------------------------------
// Owner Manual Review correction (2026-10-12): the fullscreen 1920x1080 header fit.
// The header was wider than the client area, which pushed the Exit action off the right edge
// and stretched the content cell - and with it the embedded page viewport - past the screen.
// ---------------------------------------------------------------------------

test('both header actions stay present, usable and never hidden', () => {
  assert.match(source.main, /ConfigureButton\(_exitButton, "Exit Kiosk Preview", Err\)/);
  assert.match(source.main, /ConfigureButton\(_windowModeButton, "Windowed \/ full screen", Info\)/);
  assert.match(source.main, /_actionZone\.Controls\.Add\(_windowModeButton\);/);
  assert.match(source.main, /_actionZone\.Controls\.Add\(_exitButton\);/);
  assert.match(source.main, /_exitButton\.Click \+= \(_, _\) => RequestConfirmedExit\(\);/);
  assert.match(source.main, /_windowModeButton\.Click \+= \(_, _\) => ToggleWindowMode\(\);/);
  // Keyboard reachable, and never hidden, disabled or removed by any code path.
  assert.match(source.main, /button\.TabStop = true;/);
  assert.equal(/_exitButton\.Visible\s*=\s*false/.test(source.main), false);
  assert.equal(/_exitButton\.Enabled\s*=\s*false/.test(source.main), false);
  assert.equal(/_windowModeButton\.Visible\s*=\s*false/.test(source.main), false);
  assert.equal(/_actionZone\.Controls\.Remove|Controls\.Remove\(/.test(source.main), false);
  // The two actions share one bounded zone, so neither can be dropped to make room.
  assert.match(source.main, /private readonly FlowLayoutPanel _actionZone = new\(\);/);
});

test('the header is measured, bounded and responsive instead of content-sized', () => {
  // Two columns: the action column is AutoSize, the text column takes the remainder.
  assert.match(source.main, /_statusBar\.ColumnStyles\.Add\(new ColumnStyle\(SizeType\.Percent, 100F\)\);/);
  assert.match(source.main, /_statusBar\.ColumnStyles\.Add\(new ColumnStyle\(SizeType\.AutoSize\)\);/);
  // The outer layout's single column is explicitly bounded (the unstated AutoSize default here is
  // what stretched the content cell and the embedded page past the screen edge).
  assert.match(source.main, /layout\.ColumnStyles\.Add\(new ColumnStyle\(SizeType\.Percent, 100F\)\);/);
  const buildLayout = source.main.slice(
    source.main.indexOf('private void BuildLayout()'),
    source.main.indexOf('/// <summary>', source.main.indexOf('private void BuildLayout()')),
  );
  assert.match(buildLayout, /AutoSize = false,/, 'the outer layout must not be content-sized');
  // Four measured rows, applied as absolute heights from a real text measurement.
  assert.match(source.main, /_statusBar\.RowCount = 4;/);
  assert.match(source.main, /style\.SizeType = SizeType\.Absolute;/);
  assert.match(source.main, /TextRenderer\.MeasureText\(/);
  assert.match(source.main, /TextFormatFlags\.WordBreak \| TextFormatFlags\.NoPadding \| TextFormatFlags\.NoPrefix/);
  // Re-layout on client-size change, on a monitor DPI change, and whenever a header text changes.
  assert.match(source.main, /protected override void OnResize\(EventArgs e\)/);
  assert.match(source.main, /base\.OnResize\(e\);\s*\n\s*LayoutStatusBar\(\);/);
  assert.match(source.main, /DpiChanged \+= \(_, _\) => LayoutStatusBar\(\);/);
  assert.equal(/OnDpiChangedAfterParent/.test(source.main), false, 'one DPI mechanism only: the Form DpiChanged event plus OnResize');
  assert.match(source.main, /LayoutStatusBar\(\);/);
  // No scrollbar is ever introduced for the header, and no minimum width above the Owner's display.
  assert.equal(/AutoScroll/.test(source.main), false);
  const minimums = source.main.match(/MinimumSize = new Size\((\d+), (\d+)\)/g) ?? [];
  assert.deepEqual(minimums, ['MinimumSize = new Size(1024, 600)'], 'the only minimum size is the existing 1024x600, so no larger display is required');
});

test('the left status content cannot force the action area outside the client width', () => {
  // Every text row has no preferred width of its own: AutoSize=false and Dock=Fill.
  assert.match(source.main, /private static void ConfigureTextRow\(Label label, Color colour, float size, FontStyle style\)/);
  const textRow = source.main.slice(source.main.indexOf('private static void ConfigureTextRow'));
  assert.match(textRow.slice(0, 600), /label\.AutoSize = false;/);
  assert.match(textRow.slice(0, 600), /label\.Dock = DockStyle\.Fill;/);
  for (const label of ['_stageLabel', '_connectionState', '_boundaryLabel', '_policyState']) {
    assert.match(source.main, new RegExp(`ConfigureTextRow\\(${label},`), `${label} must be a measured text row`);
  }
  // The action width is reserved FIRST and the identity line gets only the remainder.
  assert.match(source.main, /_windowModeButton\.PreferredSize\.Width \+ _windowModeButton\.Margin\.Horizontal/);
  assert.match(source.main, /\+ _exitButton\.PreferredSize\.Width \+ _exitButton\.Margin\.Horizontal/);
  assert.match(source.main, /var identityWidth = Math\.Max\(120, inner - actionsWidth - BarGapX - 8\);/);
  // The long texts own full-width rows rather than competing for one line.
  assert.match(source.main, /_statusBar\.SetColumnSpan\(_boundaryLabel, 2\);/);
  assert.match(source.main, /_statusBar\.SetColumnSpan\(_policyState, 2\);/);
  assert.match(source.main, /_statusBar\.SetColumnSpan\(_connectionState, 2\);/);
  // The old single-line, non-wrapping layout is gone.
  assert.equal(/WrapContents = false;/.test(source.main), true, 'the action zone stays on one line');
  assert.equal(/_stageLabel\.AutoSize = true|_policyState\.AutoSize = true/.test(source.main), false);
});

test('the native identity is Stage 0.4C-K2 in the shell constants and the window title, and matches the Preview', () => {
  const stageId = /public const string StageId = "([^"]+)";/.exec(source.options)?.[1];
  assert.equal(stageId, '0.4C-K2');
  assert.equal(stageId, STAGE_ID, 'the native shell and the embedded Preview must name the same Stage');
  assert.match(source.options, /public const string WindowTitle = "WaterJet Sentinel Suite - Kiosk Preview \(Stage 0\.4C-K2\)";/);
  assert.match(source.main, /Text = KioskShellOptions\.WindowTitle;/);
  // No K1 identity in any displayed or identity string literal of the shell. Doc comments may keep
  // provenance ("originated in Stage 0.4C-K1"); the window title and header label must not.
  for (const [name, text] of [['KioskShellOptions.cs', source.options], ['MainForm.cs', source.main], ['Program.cs', source.program]]) {
    const literals = text.match(/"[^"\n]*"/g) ?? [];
    assert.equal(literals.some((l) => /0\.4C-K1/.test(l)), false, `${name} must not show a K1 identity in a string literal`);
  }
});

test('the header keeps the Stage boundary, the host state and the policy counters on screen', () => {
  assert.match(source.main, /_boundaryLabel\.Text = KioskShellOptions\.BoundaryText;/);
  assert.match(source.main, /_stageLabel\.Text = "Kiosk Preview  -  Stage " \+ KioskShellOptions\.StageId;/);
  assert.match(source.main, /_connectionState\.Text = "LOCAL HOST: CONNECTED \(READ-ONLY PREVIEW\)";/);
  assert.match(source.main, /_connectionState\.Text = "LOCAL HOST: UNAVAILABLE - RECONNECTING";/);
  assert.match(source.main, /APPROVED ORIGIN ONLY  \|  blocked: \{0\} navigation, \{1\} popup, \{2\} download, \{3\} permission/);
  // The counters are re-measured when they change, so the header never clips them.
  const policy = source.main.slice(source.main.indexOf('private void UpdatePolicyState'));
  assert.match(policy.slice(0, 600), /LayoutStatusBar\(\);/);
});
