# Trial A and Trial B — Exact Hardware Execution Package

**Scope ID:** `TRIAL-A-B-HARDWARE-EXECUTION-PACKAGE`
**Authorization:** DOCUMENTATION AND EXECUTION-PACKAGE PREPARATION ONLY
**Validated software baseline:** `c9c4a2d0ee1cca38bf6dee08ae8fa76f5c6ce6c7` (PR #45)
**Approved main baseline:** `619904efe0a99c7b5fae2262e68c901e30f29957`

| Declaration | State |
|---|---|
| PHYSICAL HARDWARE | **NOT EXECUTED / NOT VERIFIED** |
| PRODUCTION | **NOT AUTHORIZED** |
| MERGE | **HOLD** |
| FINAL READINESS REVIEW | **NOT STARTED** |

---

## 1. What this package is, and what it is not

This document is the **execution package**: the exact environment contract, the exact PowerShell
sequence, the exact evidence fields, the acceptance rule and the STOP conditions for the two physical
trials that already exist as validated software. Every variable name, literal, exit code, output field
and call ordering below was read from the final Source at the validated baseline, not from an earlier
report.

It authorizes **nothing**. Preparing this package is not a hardware authorization, not a Pump
authorization, not a valve authorization and not a merge. Each run below requires a **separate**
Owner authorization, executed once, then classified, then closed.

### 1.1 The two executions are separate evidence classes

| | Trial A | Trial B |
|---|---|---|
| Purpose | physical Pump-ready read | physical IV open/close |
| Runner | `ProcessIoPumpReadyReadTrial` | `DryIvTrial` |
| Selector | `WJSS_PUMP_READY_READ_TRIAL=AI-003_ONLY` | `WJSS_DRY_IV_TRIAL_MODE=SOFTWARE_SIMULATED_AI003` |
| Profile | `TEST_HARDWARE` | `TEST_HARDWARE_DRY` |
| Pump | **running, under Owner authority** | **stopped, Owner-attested** |
| Pump-ready input | **physical `AI-003+`** | **software simulated** |
| Valve command | **none, structurally impossible** | one `IV1` OPEN then CLOSE |
| Evidence class | `PHYSICAL` | `SOFTWARE_SIMULATED` (Pump-ready) |

The two records are never combined, never aggregated and never summarized as one run. See §5.

### 1.2 Source documents this package was derived from

`apps/runtime/ProcessIoPumpReadyReadTrial.cs` · `apps/runtime/DryIvTrial.cs` · `apps/runtime/Program.cs` ·
`apps/runtime/ProcessIoEndpointConfiguration.cs` · `apps/runtime/CleaningPathDryComposition.cs` ·
`packages/application/ProcessIo/ProcessIoFoundation.cs` ·
`packages/application/CleaningPath/Composition/ProcessIoEquipmentBoundary.cs` ·
`packages/contracts/ProcessIoContracts.cs` · `docs/ACCELERATED_CLEANING_IV_TWO_STAGE_EVIDENCE.md` ·
`docs/TRIAL_A_PHYSICAL_PUMP_READY_ENABLEMENT_ANALYSIS.md`.

### 1.3 Owner-local validation of record

| Owner-local check | Result |
|---|---|
| Normal restore | PASS |
| Locked restore | PASS |
| Restore and lock drift | NONE |
| Full Release build | PASS |
| Focused Trial A (`ProcessIoPumpReadyReadTrialTests`) | 91 / 91 PASS |
| Focused Trial B (`DryIvTrialTests`) | 57 / 57 PASS |
| Process-I/O Foundation | 28 / 28 PASS |
| Process-I/O Dry-cycle Matrix | 64 / 64 PASS |
| Runtime API | 269 / 269 PASS |
| Runtime Core | 842 / 842 PASS |
| Motion Library | 368 / 368 PASS |
| Full .NET matrix | 1736 / 1736 PASS |

These results belong to `c9c4a2d0ee1cca38bf6dee08ae8fa76f5c6ce6c7` and to nothing else. A different head
invalidates them and every step below.

---

## 2. The one executable, and how a trial is selected

Both trials are inside the single host executable `Wjss.Runtime`, target framework `net10.0`, built by
`WaterJetSentinelSuite.sln` in `Release`:

```text
<repo>\apps\runtime\bin\Release\net10.0\Wjss.Runtime.exe
```

**This host has no trial command-line parser.** `Program.cs` reads the trial selectors from the
**process environment** only; the published tokens `--pump-ready-read-trial` and `--dry-iv-trial` are
printed *inside* the run as the surface identity. They are not switches.

* Launch the executable with **no arguments**.
* Selecting a trial and passing the token as an argument are not equivalent, and passing the token
  alone reaches the web host instead.
* The token appears in the evidence as `trial=--pump-ready-read-trial` / `trial=--dry-iv-trial`.

### 2.1 Routing order in `Program.cs` (exact, and asymmetric)

1. `WJSS_TC_ACQUISITION_MODE` is read;
2. `WJSS_DRY_COMMAND_MODE` is read;
3. `WJSS_DEVICE_PROFILE` is read;
4. **`WJSS_DRY_IV_TRIAL_MODE`** — present and exact ⇒ `DryIvTrial.Run` and **the process returns**;
5. **`WJSS_PUMP_READY_READ_TRIAL`** — present and exact ⇒ `ProcessIoPumpReadyReadTrial.Run`;
6. otherwise the ordinary host continues.

Two consequences that a report-derived package would miss, and that this package enforces:

* **Trial A refuses if Trial B is also selected** — `PUMP_READY_TRIAL_REFUSED_IV_TRIAL_SELECTED`. The
  Product enforces this itself.
* **Trial B does not read the Trial A selector at all.** `DryIvTrial` never references
  `WJSS_PUMP_READY_READ_TRIAL`, and routing reaches Trial B first. If both selectors are set, Trial B
  runs and Trial A is silently ignored. Therefore **the absence of the Trial A selector before a
  Trial B run is an operator pre-flight obligation, not a Product refusal.** It is a hard STOP below.

---

## 3. Common contract

### 3.1 Session hygiene — run every step in ONE PowerShell process

Both runners read the **process** environment. Environment changes made in one shell are not visible to
another. Every numbered step for a given trial must run in the same PowerShell window, in order.

### 3.2 Clearing before setting

Before any trial, the Operator clears **all** `WJSS_*` variables and `TargetPath`:

```powershell
Get-ChildItem Env: | Where-Object { $_.Name -like 'WJSS_*' } |
    ForEach-Object { Remove-Item -LiteralPath ('Env:' + $_.Name) -ErrorAction SilentlyContinue }
Remove-Item -LiteralPath 'Env:TargetPath' -ErrorAction SilentlyContinue
```

`TargetPath` is an MSBuild-injected variable that can shadow build-time identity; it is cleared so no
inherited value can influence the run. Clearing is not a security measure — it is what makes "set only
these variables" a fact rather than an intention.

### 3.3 Master variable contract

Every name below was verified against Source. `—` means the runner never reads it.

| Variable | Trial A | Trial B | Source authority | Mandatory |
|---|---|---|---|---|
| `WJSS_DEVICE_PROFILE` | `TEST_HARDWARE` | `TEST_HARDWARE_DRY` | `RuntimeHostOptions.ProfileVariable` | yes |
| `WJSS_PUMP_READY_READ_TRIAL` | `AI-003_ONLY` | **must be absent** | `ProcessIoPumpReadyReadTrial.ModeVariable` | Trial A only |
| `WJSS_DRY_IV_TRIAL_MODE` | **must be absent** | `SOFTWARE_SIMULATED_AI003` | `DryIvTrial.ModeVariable` | Trial B only |
| `WJSS_DRY_COMMAND_MODE` | **must be absent** | **must be absent** | `ProcessIoEndpointConfiguration.DryModeVariable` | selection must not appear |
| `WJSS_TC_ACQUISITION_MODE` | **must be absent** | **must be absent** | `TcEndpointConfiguration.ModeVariable` | selection must not appear |
| `WJSS_PROCESS_IO_HOST` | Owner-local | Owner-local | `ProcessIoEndpointConfiguration.HostVariable` | yes |
| `WJSS_PROCESS_IO_PORT` | Owner-local / empty | Owner-local / empty | `ProcessIoEndpointConfiguration.PortVariable` | no — empty uses adopted `502` |
| `WJSS_PROCESS_IO_UNIT_ID` | Owner-local / empty | Owner-local / empty | `ProcessIoEndpointConfiguration.UnitIdVariable` | no — empty uses adopted `1` |
| `WJSS_PROCESS_IO_CATALOGUE` | Owner-local, fully qualified | Owner-local, fully qualified | `CleaningPathDryComposition.CatalogueVariable` | yes |
| `WJSS_PROCESS_IO_CONSENT` | read-only phrase | single-DO phrase | `ProcessIoEndpointConfiguration.ConsentVariable` | yes |
| `WJSS_PUMP_READY_SETPOINT_BAR` | Owner-local finite bar | Owner-local finite bar | `CleaningPathDryComposition.PumpReadySetpointVariable` | yes |
| `WJSS_PUMP_READY_FRESHNESS_MS` | `1`…`600000` | — | `ProcessIoPumpReadyReadTrial.FreshnessVariable` | Trial A only |
| `WJSS_PRESSURE_LOW_BAR` | optional, default `0` | finite bar | `CleaningPathDryComposition.PressureLowVariable` | Trial B only |
| `WJSS_PRESSURE_HIGH_BAR` | optional, derived | finite bar | `CleaningPathDryComposition.PressureHighVariable` | Trial B only |
| `WJSS_VALVE_SUPERVISION_MS` | — | `1`…`600000` | `CleaningPathDryComposition.ValveSupervisionVariable` | Trial B only |
| `WJSS_DRY_IV_TRIAL_PUMP_STOPPED` | — | `PUMP_STOPPED_OWNER_ATTESTED` | `DryIvTrial.PumpStoppedVariable` | Trial B only |
| `WJSS_DRY_IV_TRIAL_VALVE` | — | `IV1` | `DryIvTrial.ValveVariable` | Trial B only |
| `WJSS_DRY_IV_TRIAL_SIMULATED_AI003_BAR` | — | finite bar in domain | `DryIvTrial.SimulatedPressureVariable` | Trial B only |

Never read by either trial path, and irrelevant to both: `WJSS_API_PORT`, `WJSS_SIMULATOR_SCENARIO`,
`WJSS_SYNTHETIC_SEED`, `WJSS_TIME_DELAY_DEMO`, `WJSS_LOW_PRESSURE_THRESHOLD_BAR`,
`WJSS_HIGH_PRESSURE_THRESHOLD_BAR`. The last two are **host options**, not the trial thresholds: the
trial thresholds are `WJSS_PRESSURE_LOW_BAR` and `WJSS_PRESSURE_HIGH_BAR`. Mixing the two is a
configuration error, not a refusal.

### 3.4 The two consent constants are different, and neither is interchangeable

| Trial | Source constant | Consequence of the wrong phrase |
|---|---|---|
| Trial A | `ProcessIoConsentPhrases.PhysicalDiAiRead` | `PUMP_READY_TRIAL_REFUSED_READ_ONLY_CONSENT` |
| Trial B | `ProcessIoConsentPhrases.PhysicalDoSingle` | `IV_TRIAL_REFUSED_CONSENT_MISSING` / mismatch code |

The phrase is deliberately **not reproduced in this document**, following the existing
two-stage-evidence convention: it is a physical-act authorization, it is published once in
`packages/contracts/ProcessIoContracts.cs`, and it is entered interactively at the console. The
comparison is ordinal and exact — a case change, an extra space, a punctuation change or a paraphrase
refuses the run. Neither phrase ever appears in captured output; Trial A prints only
`consent=consent-sha256-prefix:<12 hex>`.

### 3.5 Endpoint and catalogue are Owner-local, never committed here

* No address, port, Unit ID, catalogue path, setpoint or freshness value appears in this repository.
* The endpoint is read from the environment, held in memory and printed only as
  `endpoint=endpoint-sha256-prefix:<12 hex>` (Trial A) or a derived digest (Trial B, §5.3).
* The catalogue must be **fully qualified**, present, and no larger than one megabyte. It is then
  loaded by `ProcessIoCatalogueLoader`, which enforces the exact 22-column header, **exactly 111 data
  rows**, per-row source order and the per-row provenance digest
  `28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299`.
* The repository's own conforming inventory is `docs/evidence/process-io-inventory-2026-10-22.csv`
  (SHA-256 `4a6d998dcfad6d36d17aa7a4827f034e3b731d7edd0d9f1d530e8847d276a286`). Pointing
  `WJSS_PROCESS_IO_CATALOGUE` at a local copy of it, or at the validated worktree copy, is a decision
  for the Owner; either way the Operator records the SHA-256 of the file actually used.

### 3.6 Redaction rules — non-negotiable

1. The **endpoint address**, **port**, **Unit ID** and the **catalogue path** are never written to the
   evidence record. The endpoint is identified only by its printed fingerprint.
2. The **consent phrase** is never echoed, never written to disk, never pasted into chat, never stored
   in a file, and never committed. It is typed at the console prompt and consumed from memory.
3. The evidence record is written **outside** the repository. The scripts below refuse to write inside
   it.
4. After each run the scripts scan the captured output for the endpoint string and for the consent
   phrase. A hit is a **STOP**: destroy the capture, do not paste it anywhere, and report the leak.
5. Screenshots of the console are not evidence: they can carry the address in a banner, hostname or
   window title. Use the captured text files.

### 3.7 Evidence location and naming

```text
<owner-local evidence folder, outside the repository>\
    TRIAL-A-<yyyyMMdd-HHmmss>.stdout.txt
    TRIAL-A-<yyyyMMdd-HHmmss>.stderr.txt
    TRIAL-A-<yyyyMMdd-HHmmss>.record.txt
    TRIAL-B-<yyyyMMdd-HHmmss>.stdout.txt
    TRIAL-B-<yyyyMMdd-HHmmss>.stderr.txt
    TRIAL-B-<yyyyMMdd-HHmmss>.record.txt
```

One run, one file set. A second attempt gets a new timestamp; nothing is overwritten and nothing is
appended to a previous trial's record.

### 3.8 Lifecycle contract — shared by both trials

Both scripts follow the same shape, because the same class of defect applies to both: a check placed
after a normal return is not a check.

| Element | Rule |
|---|---|
| Invocation count | **exactly one** `Start-Process` per script, inside `try`. There is no loop, no retry, no second launch, and no automatic re-run of any kind. |
| Pre-initialized state | every variable the `finally` body reads is assigned **before** the `try`, in `NOT_ACCEPTED` / `NOT_CONFIRMED` form, so `Set-StrictMode -Version Latest` cannot make a `finally` body fail on an unassigned variable. |
| Exception capture | `try { … } catch { … } finally { … }` — the `catch` records `invocation_status=FAULTED`, the exception **type**, and a message passed through `Get-RedactedText`, then returns control so the `finally` still runs. |
| Exception message | the endpoint address and the consent phrase are replaced with `<endpoint-redacted>` and `<consent-redacted>`, and the text is capped at 300 characters. No exception text reaches the console or the record unfiltered. |
| Terminal obligations | all physical confirmations live in `finally`, so an invocation, redirection or wait fault cannot skip them. |
| Classification gate | `ACCEPTED…` requires `terminal_obligations_complete=YES`, `invocation_status=COMPLETED` and zero other failures. The assignment sits **after** the `try/finally`, so an interrupted `finally` leaves the initialized `NOT_ACCEPTED` in place. |
| Consent lifetime | the phrase is converted once inside an outer `try`, and an outer `finally` removes `Env:WJSS_PROCESS_IO_CONSENT`, nulls `$consent` and disposes `$secure` on every path. The phrase is never printed, never written and never placed in the record. |
| Path safety | before any prompt: placeholders must be replaced, `Repo` / `EvidenceDir` / `CataloguePath` must be fully qualified, and the evidence folder must resolve **outside** the repository after canonical normalization. |

**The limit of all of it.** The script cannot stop the Pump and cannot command a valve. If the
PowerShell process is hard-killed, the console is lost, or the machine loses power, `finally` is **not
guaranteed to run**. For Trial A that is tolerable, because the trial commands nothing and the Pump is
already under the Owner's hand. For Trial B it matters directly: a killed process cannot de-energise a
maintained coil. In either case the **physical abort procedure is immediately authoritative**, and no
software step in this package substitutes for it.

---

## 4. TRIAL A — physical Pump-ready read-only evidence

### 4.1 What it does, in the exact Product order

1. reads `WJSS_TC_ACQUISITION_MODE`, `WJSS_DRY_COMMAND_MODE` and `WJSS_DEVICE_PROFILE`;
2. `TryReadConfiguration`: PRODUCTION refused first; selector must be exactly `AI-003_ONLY`; integrated
   dry and IV selectors must both be absent; profile must be `TEST_HARDWARE`; endpoint, read-only
   consent, catalogue, `AI-003+` binding, thresholds and freshness validated — **all before any
   transport exists**;
3. constructs `ProcessIoPhysicalTransport` (no socket yet) and `ProcessIoSession`, then **opens** it;
4. reads **`AI-003+` once** through `session.ObserveAnalogInput`, bracketed by the injected clock
   (`ObservedAt` before the read, `ReceivedAt` after it);
5. captures `session.Generation`;
6. **decides** — `Classify` runs here, exactly once, with `inletDiagnostic: null` and the judgement
   instant taken immediately after `AI-003+` completed;
7. only then reads `AI-002+` as an optional diagnostic, behind its own guard and its own fault
   capture, and attaches it to the already-decided record;
8. reports, closes the session **last**, releases the transport, prints `session_state` and
   `transport_released`, and returns the exit code.

Step 6 before step 7 is why the diagnostic is inert: its latency cannot age the acquisition and its
value cannot move the verdict.

### 4.2 Zero-write is structural, not promised

`ProcessIoPumpReadyReadTrial.cs` contains **no** occurrence of `ProcessIoWriteAuthorization`,
`AdmitWrite`, `DispatchDigitalOutput`, `WriteCoil`, `WriteSingleRegister`, `WriteRegisters`,
`RequestValve`, `SafeCleanupCloseAll`, `ProcessIoEquipmentBoundary` or `ProcessIoDryCommandGate`, and no
motion, Galil or queue type. A read needs no authority; the total absence of write authority is what
makes the trial read-only.

**Trial A has no in-process abort handler.** `Ctrl+C` terminates the process directly. That is safe
precisely because nothing was ever energised, admitted or dispatched. The Pump abort is physical and
belongs to the Operator (§4.3 item 10).

### 4.3 Physical preconditions — confirm every line before the run

Print this list, tick each line, and attach it to the evidence record.

1. ☐ the validated executable head is exactly `c9c4a2d0ee1cca38bf6dee08ae8fa76f5c6ce6c7`;
2. ☐ the Release build output belongs to that exact head (the script proves HEAD, and the executable
   SHA-256 is recorded);
3. ☐ all eight Isolation Valve command outputs are in their commissioned **CLOSED** state;
4. ☐ no IV command selector is set (`WJSS_DRY_IV_TRIAL_MODE`, `WJSS_DRY_IV_TRIAL_VALVE` absent);
5. ☐ `WJSS_DRY_IV_TRIAL_MODE` is absent;
6. ☐ `WJSS_DRY_COMMAND_MODE` is absent;
7. ☐ no Galil or motion command is authorized, and no motion software is running;
8. ☐ no Cleaning Path is selected;
9. ☐ Pump start and stop remain under Owner operating authority; the Operator is named;
10. ☐ the Owner can perform the **physical Pump abort** at any moment, and the person doing it is named;
11. ☐ `AI-003+` is understood to be the **sole** Pump-ready source for this trial;
12. ☐ `AI-002+` is understood to be **diagnostic only** and cannot make the trial pass;
13. ☐ the configured Pump-ready setpoint is written down **before** the run, in bar;
14. ☐ the Process-I/O endpoint, port, Unit ID and catalogue are external Owner-local values;
15. ☐ every log and record will carry only the redacted endpoint fingerprint.

`OWNER_DECISION_REQUIRED` — the Pump running condition (speed, duration, water destination), the safe
observation point, and who is authorized to start and stop the Pump. None of these is defaulted by the
software and none is invented by this package.

### 4.4 Execution sequence

Fill the six placeholders in step 1, then run the block top to bottom in **one** PowerShell window.

**Guaranteed terminal state.** The Pump is started by the Owner at step 11, the executable is invoked
inside `try` at steps 12–13, and steps 14–17 — Pump stop, Pump-stopped confirmation, IV-closed
confirmation, no-motion confirmation and the safe-terminal confirmation — run in `finally`. All five
run on every path out of the invocation: normal return, non-zero exit, invocation fault, redirection
fault or wait fault. An invocation failure is retained as `invocation_status=FAULTED` with its
exception type and a redacted message, and it forces `NOT_ACCEPTED`; it can never bypass the Pump stop.

**Classification is gated on the finally body.** `$terminalObligationsComplete` starts at `NO` and is
set to `YES` only when all five answers are affirmative. `ACCEPTED_PHYSICAL_PUMP_READY` is reachable
only from a `YES` — and because the assignment happens *after* the `finally` block, a `finally` that
is itself interrupted leaves the run at its initialized `NOT_ACCEPTED`. Nothing is accepted by default.

The script never controls the Pump: it prompts the Owner, records the answer and records the
timestamp. A stop before step 12 contacts nothing and writes no record, because nothing was contacted.

```powershell
# =====================================================================================
# TRIAL A — PHYSICAL PUMP-READY READ-ONLY EVIDENCE
# One bounded read-only trial. No loop. No retry. Exactly ONE invocation.
#
# LIFECYCLE: the Owner starts the Pump, the executable is invoked inside try, and the Pump
# stop plus every physical terminal-state confirmation run inside finally. An invocation,
# redirection or wait failure therefore cannot bypass the Pump stop, the Pump-stopped
# confirmation, the IV-closed confirmation, the safe-terminal confirmation or the consent
# cleanup, because all of them live in finally rather than after a normal return.
#
# LIMIT: this script never controls the Pump and cannot stop it. If PowerShell itself or
# the host process cannot continue — hard kill, console loss, power loss — finally is NOT
# guaranteed to run, and the PHYSICAL ABORT is immediately authoritative.
# =====================================================================================
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---- step 1 — validated worktree and exact Release output ---------------------------
# ---- Owner-local values: never committed, never printed ------------------------------
$ExpectedHead  = 'c9c4a2d0ee1cca38bf6dee08ae8fa76f5c6ce6c7'
$Repo          = '<FULLY-QUALIFIED-PATH-TO-THE-VALIDATED-WORKTREE>'
$EvidenceDir   = '<FULLY-QUALIFIED-PATH-TO-AN-EVIDENCE-FOLDER-OUTSIDE-THE-REPOSITORY>'
$CataloguePath = '<FULLY-QUALIFIED-PATH-TO-THE-OWNER-LOCAL-CATALOGUE-CSV>'
$EndpointHost  = '<OWNER-LOCAL-BENCH-HOST>'
$EndpointPort  = ''
$EndpointUnit  = ''
$SetpointBar   = '<OWNER-LOCAL-PUMP-READY-SETPOINT-BAR>'
$FreshnessMs   = '<OWNER-LOCAL-FRESHNESS-BUDGET-MS>'

$Exe      = Join-Path $Repo 'apps\runtime\bin\Release\net10.0\Wjss.Runtime.exe'
$RunLabel = 'TRIAL-A-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$OutFile  = Join-Path $EvidenceDir ($RunLabel + '.stdout.txt')
$ErrFile  = Join-Path $EvidenceDir ($RunLabel + '.stderr.txt')
$RecFile  = Join-Path $EvidenceDir ($RunLabel + '.record.txt')

# ---- helpers ------------------------------------------------------------------------
function Test-FullyQualified {
    param([string] $Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return [System.IO.Path]::IsPathFullyQualified($Value)
}

function Get-CanonicalPath {
    param([string] $Value)
    $full = [System.IO.Path]::GetFullPath($Value)
    $trimmed = $full.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    if ($trimmed.Length -eq 0) { return [string][System.IO.Path]::DirectorySeparatorChar }
    return $trimmed
}

# The only text that may reach the record from a caught exception. The endpoint address and
# the consent phrase are replaced, and the result is length-capped.
function Get-RedactedText {
    param([string] $Text)
    if ([string]::IsNullOrEmpty($Text)) { return 'NONE' }
    $out = $Text
    if (-not [string]::IsNullOrEmpty($EndpointHost) -and $EndpointHost.Length -ge 3) {
        $out = $out.Replace($EndpointHost, '<endpoint-redacted>')
    }
    if (-not [string]::IsNullOrEmpty($consent) -and $consent.Length -ge 3) {
        $out = $out.Replace($consent, '<consent-redacted>')
    }
    if ($out.Length -gt 300) { $out = $out.Substring(0, 300) + '...' }
    return $out
}

# Always answers, so a terminal-state confirmation can never abort the finally body.
function Read-Confirmation {
    param([string] $Prompt)
    try {
        $answer = Read-Host $Prompt
        if ($null -eq $answer) { return 'UNAVAILABLE' }
        $text = ([string]$answer).Trim().ToUpperInvariant()
        if ($text.Length -eq 0) { return 'UNAVAILABLE' }
        return $text
    }
    catch { return 'UNAVAILABLE' }
}

function Get-ReportField {
    param(
        [Parameter(Mandatory)] [hashtable] $Report,
        [Parameter(Mandatory)] [string]    $Key
    )
    if ($Report.ContainsKey($Key)) { return $Report[$Key] }
    return 'UNAVAILABLE'
}

# ---- placeholder scan — BEFORE any prompt, so nothing is prompted or contacted ------
foreach ($pair in @(
    @{ Name = 'Repo';          Value = $Repo },
    @{ Name = 'EvidenceDir';   Value = $EvidenceDir },
    @{ Name = 'CataloguePath'; Value = $CataloguePath },
    @{ Name = 'EndpointHost';  Value = $EndpointHost },
    @{ Name = 'SetpointBar';   Value = $SetpointBar },
    @{ Name = 'FreshnessMs';   Value = $FreshnessMs })) {
    if ($pair.Value -match '[<>]') {
        throw ("STOP: {0} still contains a placeholder. Nothing was prompted and nothing was contacted." -f $pair.Name)
    }
}

# ---- canonical path validation — file-system strings only, no Hardware contact ------
foreach ($pair in @(
    @{ Name = 'Repo';          Value = $Repo },
    @{ Name = 'EvidenceDir';   Value = $EvidenceDir },
    @{ Name = 'CataloguePath'; Value = $CataloguePath })) {
    if (-not (Test-FullyQualified $pair.Value)) {
        throw ("STOP: {0} must be a fully qualified path. Nothing was prompted and nothing was contacted." -f $pair.Name)
    }
}

# Compared AFTER canonical normalization and against a trailing separator, so a sibling
# folder that merely shares the repository's name prefix is not mistaken for the repository
# and a real sub-folder cannot slip through. Windows path semantics: case-insensitive.
$repoCanonical       = Get-CanonicalPath $Repo
$evidenceCanonical   = Get-CanonicalPath $EvidenceDir
$repoPrefix          = $repoCanonical + [System.IO.Path]::DirectorySeparatorChar
$evidencePrefix      = $evidenceCanonical + [System.IO.Path]::DirectorySeparatorChar
if ($evidencePrefix.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'STOP: the evidence folder resolves inside the repository after canonicalization. Nothing was prompted and nothing was contacted.'
}

if (-not (Test-Path -LiteralPath $Exe -PathType Leaf))              { throw 'STOP: the Release executable was not found in the validated worktree.' }
if (-not (Test-Path -LiteralPath $CataloguePath -PathType Leaf))   { throw 'STOP: the Owner-local catalogue file is missing.' }
if (-not (Test-Path -LiteralPath $EvidenceDir -PathType Container)) { throw 'STOP: the evidence folder does not exist.' }

# ---- lifecycle state, initialized before any prompt --------------------------------
# StrictMode is on, so every variable used by the finally body exists even when the try
# body fails long before assigning it. NOT_ACCEPTED is the starting classification and no
# path can promote it except a completed finally body.
$invocationStatus            = 'NOT_ATTEMPTED'
$invocationErrorType         = 'NONE'
$invocationErrorMessage      = 'NONE'
$exitCode                    = -1
$pumpStartConfirmed          = 'NO'
$pumpStartUtc                = 'NOT_STARTED'
$pumpStopUtc                 = 'NOT_STOPPED'
$pumpStoppedConfirmed        = 'NOT_CONFIRMED'
$ivsClosed                   = 'NOT_CONFIRMED'
$noMotion                    = 'NOT_CONFIRMED'
$safeTerminal                = 'NOT_CONFIRMED'
$terminalObligationsComplete = 'NO'
$classification              = 'NOT_ACCEPTED'
$failures                    = @()
$map                         = @{}
$consent                     = $null
$secure                      = $null

try {
    # ---- step 2 — clear TargetPath and every WJSS_* variable ------------------------
    Get-ChildItem Env: | Where-Object { $_.Name -like 'WJSS_*' } |
        ForEach-Object { Remove-Item -LiteralPath ('Env:' + $_.Name) -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath 'Env:TargetPath' -ErrorAction SilentlyContinue

    # ---- step 3 — set only the Trial A variables ------------------------------------
    $env:WJSS_DEVICE_PROFILE        = 'TEST_HARDWARE'
    $env:WJSS_PUMP_READY_READ_TRIAL = 'AI-003_ONLY'
    $env:WJSS_PROCESS_IO_HOST       = $EndpointHost
    $env:WJSS_PROCESS_IO_CATALOGUE  = $CataloguePath
    $env:WJSS_PUMP_READY_SETPOINT_BAR = $SetpointBar
    $env:WJSS_PUMP_READY_FRESHNESS_MS = $FreshnessMs
    if ($EndpointPort -ne '') { $env:WJSS_PROCESS_IO_PORT    = $EndpointPort }
    if ($EndpointUnit -ne '') { $env:WJSS_PROCESS_IO_UNIT_ID = $EndpointUnit }

    # ---- the read-only consent. Typed, converted once, never echoed or persisted ----
    $secure  = Read-Host -AsSecureString -Prompt 'Enter the published READ-ONLY consent phrase'
    $consent = [System.Net.NetworkCredential]::new('', $secure).Password
    if ([string]::IsNullOrEmpty($consent)) { throw 'STOP: no consent phrase was supplied. Nothing was contacted.' }
    $env:WJSS_PROCESS_IO_CONSENT = $consent

    # ---- step 4 — prove Trial B, integrated-dry and TC selectors are absent ----------
    $mustBeAbsent = @(
        'WJSS_DRY_IV_TRIAL_MODE', 'WJSS_DRY_IV_TRIAL_VALVE', 'WJSS_DRY_IV_TRIAL_PUMP_STOPPED',
        'WJSS_DRY_IV_TRIAL_SIMULATED_AI003_BAR', 'WJSS_DRY_COMMAND_MODE', 'WJSS_TC_ACQUISITION_MODE'
    )
    foreach ($name in $mustBeAbsent) {
        $value = [System.Environment]::GetEnvironmentVariable($name, 'Process')
        if (-not [string]::IsNullOrWhiteSpace($value)) { throw "STOP: $name must be absent for Trial A but is present" }
    }

    # ---- step 5 — record the exact Git HEAD -----------------------------------------
    $head = (& git -C $Repo rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'STOP: git rev-parse failed in the validated worktree' }
    if ($head -ne $ExpectedHead) { throw "STOP: HEAD is $head, expected $ExpectedHead" }

    # ---- steps 6 and 7 — record the executable and catalogue SHA-256 ----------------
    $exeSha = (Get-FileHash -LiteralPath $Exe -Algorithm SHA256).Hash.ToLowerInvariant()
    $catSha = (Get-FileHash -LiteralPath $CataloguePath -Algorithm SHA256).Hash.ToLowerInvariant()

    # ---- steps 8, 9, 10 — record setpoint, freshness and the IV prohibition ---------
    Write-Host "setpoint_bar=$SetpointBar  freshness_ms=$FreshnessMs  iv_command=PROHIBITED  motion=PROHIBITED"
    $ivProhibited = 'PROHIBITED_ALL_EIGHT_CLOSED'

    # ---- step 11 — start the physical Pump under Owner authority --------------------
    $startAck = Read-Confirmation 'Start the Pump under Owner authority, confirm the flow destination, then type START'
    if ($startAck -eq 'START') {
        $pumpStartConfirmed = 'YES'
        $pumpStartUtc       = (Get-Date).ToUniversalTime().ToString('O')
    }
    else {
        # The Owner may already have started the Pump by hand before answering. The trial is
        # NOT invoked, but the terminal-state obligations still run below, so the Owner is
        # walked through the Pump stop instead of being left with a running Pump and no prompt.
        $pumpStartConfirmed = 'NO'
        Write-Host 'The Owner did not confirm the Pump start. The trial will NOT be invoked; the Pump stop and terminal-state checks still run.'
    }

    # ---- steps 12 and 13 — invoke EXACTLY ONCE, inside try --------------------------
    try {
        if ($pumpStartConfirmed -ne 'YES') {
            $invocationStatus = 'SKIPPED_PUMP_START_NOT_CONFIRMED'
        }
        else {
            $invocationStatus = 'ATTEMPTED'
            $trial = Start-Process -FilePath $Exe -WorkingDirectory $Repo -NoNewWindow -PassThru -Wait `
                -RedirectStandardOutput $OutFile -RedirectStandardError $ErrFile
            $exitCode         = $trial.ExitCode
            $invocationStatus = 'COMPLETED'
            Write-Host "exit_code=$exitCode"
        }
    }
    catch {
        # An invocation, redirection or wait failure is retained as NOT_ACCEPTED evidence
        # and never as a reading. Execution continues into the finally body below.
        $invocationStatus       = 'FAULTED'
        $invocationErrorType    = $_.Exception.GetType().Name
        $invocationErrorMessage = 'INVOCATION_EXCEPTION_REDACTED: ' + (Get-RedactedText $_.Exception.Message)
        $exitCode               = -1
        Write-Host ("invocation faulted: " + $invocationErrorType)
    }
    finally {
        # ---- steps 14, 15, 16, 17 — ALWAYS run once the Pump was started ------------
        # Nothing here reads the trial result, and nothing here can be skipped by an
        # invocation failure. The Owner stops the Pump; the software only asks and records.
        $pumpStopAck = Read-Confirmation 'STOP THE PUMP NOW under Owner authority, confirm it is stopped, then type STOPPED'
        $pumpStopUtc = (Get-Date).ToUniversalTime().ToString('O')

        $pumpStoppedConfirmed = Read-Confirmation 'Is the Pump physically stopped with no water flowing? type YES or NO'
        $ivsClosed            = Read-Confirmation 'Are all eight IV command outputs unchanged and physically CLOSED? type YES or NO'
        $noMotion             = Read-Confirmation 'Is there no motion authority and no axis movement? type YES or NO'
        $safeTerminal         = Read-Confirmation 'Is the safe terminal state confirmed: Pump stopped, no water flow, no valve energised? type YES or NO'

        if ($pumpStopAck -eq 'STOPPED' -and
            $pumpStoppedConfirmed -eq 'YES' -and
            $ivsClosed -eq 'YES' -and
            $noMotion -eq 'YES' -and
            $safeTerminal -eq 'YES') {
            $terminalObligationsComplete = 'YES'
        }
    }

    # ---- parse the captured report, only when a capture exists ----------------------
    if (Test-Path -LiteralPath $OutFile -PathType Leaf) {
        foreach ($line in Get-Content -LiteralPath $OutFile -Encoding UTF8) {
            $idx = $line.IndexOf('=')
            if ($idx -gt 0) {
                $key = $line.Substring(0, $idx).Trim()
                $val = $line.Substring($idx + 1).Trim()
                if ($key.Length -gt 0 -and -not $map.ContainsKey($key)) { $map[$key] = $val }
            }
        }
    }

    $expected = [ordered]@{
        'source_tag'                  = 'AI-003+'
        'evidence_kind'               = 'PHYSICAL'
        'physical_ai003_verified'     = 'TRUE'
        'pump_ready'                  = 'TRUE'
        'physical_pump_verified'      = 'TRUE'
        'fresh'                       = 'TRUE'
        'quality'                     = 'GOOD'
        'ai002_used_for_decision'     = 'FALSE'
        'iv_command_executed'         = 'FALSE'
        'motion_executed'             = 'FALSE'
        'waterjet_command_executed'   = 'FALSE'
        'write_authorization_created' = 'FALSE'
        'session_state'               = 'CLOSED'
        'transport_released'          = 'TRUE'
    }

    $failures = @()
    if ($invocationStatus -ne 'COMPLETED') { $failures += "invocation_status=$invocationStatus ($invocationErrorType)" }
    if ($exitCode -ne 0)                   { $failures += "exit_code=$exitCode (expected 0)" }
    foreach ($key in $expected.Keys) {
        if (-not $map.ContainsKey($key)) { $failures += "$key is missing from the report" }
        elseif ($map[$key] -ne $expected[$key]) { $failures += "$key=$($map[$key]) (expected $($expected[$key]))" }
    }
    if ($map.ContainsKey('strict_comparison') -and -not $map['strict_comparison'].EndsWith('=> TRUE')) {
        $failures += "strict_comparison did not resolve TRUE: $($map['strict_comparison'])"
    }
    if ($map.ContainsKey('observed_at') -and $map.ContainsKey('received_at')) {
        $observed = [DateTimeOffset]::Parse($map['observed_at'], [Globalization.CultureInfo]::InvariantCulture)
        $received = [DateTimeOffset]::Parse($map['received_at'], [Globalization.CultureInfo]::InvariantCulture)
        if ($observed -gt $received) { $failures += 'observed_at is later than received_at' }
        if (([DateTimeOffset]::UtcNow - $received).TotalSeconds -lt -5) { $failures += 'received_at is in the future' }
    }
    if ($terminalObligationsComplete -ne 'YES') { $failures += 'physical terminal-state obligations were not fully confirmed' }

    # ---- step 18 — redaction scan, then one redacted record outside the repository --
    $leaks = @()
    foreach ($file in @($OutFile, $ErrFile)) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
        if (Select-String -LiteralPath $file -SimpleMatch -Pattern $EndpointHost -Quiet -ErrorAction SilentlyContinue) { $leaks += 'endpoint address' }
        if ($consent -and (Select-String -LiteralPath $file -SimpleMatch -Pattern $consent -Quiet -ErrorAction SilentlyContinue)) { $leaks += 'consent phrase' }
    }
    if ($leaks.Count -gt 0) {
        Remove-Item -LiteralPath $OutFile, $ErrFile -Force -ErrorAction SilentlyContinue
        throw "STOP: redaction failure ($($leaks -join ', ')) — captures destroyed, do not paste them anywhere"
    }

    $identityLine = if (Test-Path -LiteralPath $OutFile -PathType Leaf) {
        Get-Content -LiteralPath $OutFile -Encoding UTF8 | Where-Object { $_ -match '^\s*TRIAL:\s' } | Select-Object -First 1
    } else { $null }
    $trialIdentity = if ($identityLine) { $identityLine.Split(':', 2)[1].Trim() } else { 'UNAVAILABLE' }
    $reportText = if (Test-Path -LiteralPath $OutFile -PathType Leaf) {
        (Get-Content -LiteralPath $OutFile -Encoding UTF8) -join "`n"
    } else { 'NO_CAPTURE_FILE' }

    # ACCEPTED only here, only after the finally body above completed with every YES.
    $classification = if ($terminalObligationsComplete -eq 'YES' -and
                          $invocationStatus -eq 'COMPLETED' -and
                          $failures.Count -eq 0) { 'ACCEPTED_PHYSICAL_PUMP_READY' } else { 'NOT_ACCEPTED' }

    $record = @"
TRIAL A — PHYSICAL PUMP-READY READ-ONLY EVIDENCE
scope_id=TRIAL-A-B-HARDWARE-EXECUTION-PACKAGE
run_label=$RunLabel
recorded_at_utc=$((Get-Date).ToUniversalTime().ToString('O'))
git_head=$head
executable_sha256=$exeSha
catalogue_sha256=$catSha
endpoint=$(Get-ReportField $map 'endpoint')
consent_code=$(Get-ReportField $map 'consent')
selector=$(Get-ReportField $map 'selector')
trial=$(Get-ReportField $map 'trial')
trial_identity=$trialIdentity
configured_setpoint_bar=$SetpointBar
configured_freshness_ms=$FreshnessMs
strict_comparison=$(Get-ReportField $map 'strict_comparison')
invocation_status=$invocationStatus
invocation_error_type=$invocationErrorType
invocation_error_redacted=$invocationErrorMessage
exit_code=$exitCode
pump_start_confirmed=$pumpStartConfirmed
pump_start_utc=$pumpStartUtc
pump_stop_utc=$pumpStopUtc
pump_stopped_confirmed=$pumpStoppedConfirmed
iv_unchanged_and_closed=$ivsClosed
motion_authority_created=$noMotion
safe_terminal_state=$safeTerminal
terminal_obligations_complete=$terminalObligationsComplete
physical_abort_available=YES_OWNER_OPERATOR_NAMED
iv_command=$ivProhibited
classification=$classification
failures=$($failures -join ' | ')
-- report --
$reportText
"@
    Set-Content -LiteralPath $RecFile -Value $record -Encoding UTF8

    Write-Host "classification=$classification"
    Write-Host "record=$RecFile"
    if ($failures.Count -gt 0) { Write-Host ("failures: " + ($failures -join ' | ')) }
}
finally {
    # Consent cleanup is guaranteed once the phrase has been converted for the child, on
    # every path: accepted, refused, faulted or thrown. The phrase is never printed.
    Remove-Item -LiteralPath 'Env:WJSS_PROCESS_IO_CONSENT' -ErrorAction SilentlyContinue
    $consent = $null
    if ($null -ne $secure) {
        try { $secure.Dispose() } catch { }
        $secure = $null
    }
}
```

`TRIAL:`. The line `  TRIAL: PHYSICAL_PUMP_READY_READ_ONLY` is the only colon-separated field; the record
captures it under the key `TRIAL:`. If the operator prefers, copy that one line by hand.

### 4.5 Required evidence fields

Every field below is printed by Source. Column three is the acceptance reading.

| Field | Meaning | Accepted value |
|---|---|---|
| `git_head` | the commit actually executed | `c9c4a2d0…6ce6c7` |
| `executable_sha256` | identity of the binary | recorded, stable across the run |
| `catalogue_sha256` | identity of the catalogue used | recorded, equals the Owner-approved file |
| `endpoint` | `endpoint-sha256-prefix:<12 hex>` | recorded, no address |
| `trial` | surface token | `--pump-ready-read-trial` |
| `TRIAL:` | trial identity | `PHYSICAL_PUMP_READY_READ_ONLY` |
| `selector` | selector literal actually used | `WJSS_PUMP_READY_READ_TRIAL=AI-003_ONLY` |
| `source_tag` | the Pump-ready identity | **`AI-003+`** |
| `evidence_kind` | where the value came from | **`PHYSICAL`** |
| `physical_ai003_verified` | acquisition chain valid, current, in generation | **`TRUE`** |
| `pump_ready` | strictly above the configured setpoint | **`TRUE`** |
| `physical_pump_verified` | the conjunction of the two | **`TRUE`** |
| `raw_value` | raw word | recorded |
| `engineering_bar` / `engineering_unit` | converted pressure and unit | recorded / `bar` |
| `quality` | truthful observation quality | **`GOOD`** |
| `observed_at` / `received_at` | the two bracketing instants | recorded, `observed_at ≤ received_at` |
| `fresh` | freshness at the moment of judgement | **`TRUE`** |
| `provenance` | read provenance | `PHYSICAL_READ` |
| `observation_diagnostic` | per-observation code | `OK` |
| `session_generation` | live generation | recorded, current |
| `configured_setpoint_bar` | the setpoint that was applied | equals the recorded pre-run value |
| `strict_comparison` | the comparison as Product made it | ends `=> TRUE` |
| `ai002_role` | diagnostic role | `DIAGNOSTIC_ONLY_NOT_USED_FOR_DECISION` |
| `ai002_read` | whether the diagnostic row was read | either; never decisive |
| `ai002_raw_value` | present only when `ai002_read=TRUE` | recorded if present |
| `ai002_used_for_decision` | the separation as a fact | **`FALSE`** |
| `iv_command_executed` | valve activity | **`FALSE`** |
| `motion_executed` | motion activity | **`FALSE`** |
| `waterjet_command_executed` | water-jet command | **`FALSE`** |
| `write_authorization_created` | any write authority at all | **`FALSE`** |
| `session_state` | terminal session state | **`CLOSED`** |
| `transport_released` | transport released after the session | **`TRUE`** |
| `exit_code` | process exit code | **`0`** |
| `pump_start_confirmed` | the Owner confirmed the Pump start at step 11 | `YES` |
| `pump_start_utc` / `pump_stop_utc` | Owner Pump authority boundaries | recorded, stop never `NOT_STOPPED` |
| `pump_stopped_confirmed` | explicit Pump-stopped answer | **`YES`** |
| `physical_abort_available` | abort posture | `YES` |
| `iv_unchanged_and_closed` | all eight IVs unchanged and CLOSED | **`YES`** |
| `motion_authority_created` | no motion authority, no axis movement | **`NO`** |
| `safe_terminal_state` | terminal attestation | **`YES`** |
| `terminal_obligations_complete` | every `finally` answer affirmative | **`YES`** |
| `invocation_status` | `COMPLETED` only; `FAULTED`, `SKIPPED_*` and `NOT_ATTEMPTED` all force `NOT_ACCEPTED` | **`COMPLETED`** |
| `invocation_error_type` / `..._redacted` | exception type and redacted message | `NONE` |

### 4.6 Acceptance

Trial A is accepted as **`PHYSICAL_PUMP_READY`** evidence only when **every** line holds:

* exit code **0**;
* `source_tag` exactly `AI-003+`;
* `evidence_kind` exactly `PHYSICAL`;
* `physical_ai003_verified=TRUE`, `pump_ready=TRUE`, `physical_pump_verified=TRUE`;
* `quality=GOOD` and accepted by Product, `fresh=TRUE`, `provenance=PHYSICAL_READ`;
* `session_generation` is the current generation;
* `engineering_bar` strictly greater than `configured_setpoint_bar`;
* `ai002_used_for_decision=FALSE`;
* no IV command (`iv_command_executed=FALSE`), no motion (`motion_executed=FALSE`), no water-jet command;
* Pump stopped at the terminal state; all eight IVs remain CLOSED;
* `session_state=CLOSED`, `transport_released=TRUE`;
* `invocation_status=COMPLETED` — a faulted invocation is never accepted;
* `terminal_obligations_complete=YES` — the `finally` body ran to completion with every answer
  affirmative, including the Pump stop.

Because `$terminalObligationsComplete` can only become `YES` inside the `finally` block, the
acceptance rule and the safety lifecycle are the same rule: a run that could not confirm the Pump
stop cannot be accepted, no matter what the report contains.

A valid physical `AI-003+` reading **at or below** the setpoint is still evidence — it establishes the
transmitter, wiring, rack input, transport, scaling, quality, freshness and generation — but it is
recorded as:

```text
PHYSICAL_AI003_ACQUISITION_EVIDENCE
physical_ai003_verified=TRUE   pump_ready=FALSE   physical_pump_verified=FALSE
```

and it is **not** a passing `PHYSICAL_PUMP_READY` result. Never restate it as one.

### 4.7 Exit codes

| Code | Meaning |
|---|---|
| `0` | valid, current physical `AI-003+` strictly above the setpoint |
| `2` | observation completed, Pump-ready not accepted (quality, stale, future-dated, wrong identity, superseded generation, at/below setpoint) |
| `3` | bounded read timeout or transport fault — no value produced |
| `4` | refusal before acquisition: selector, profile, endpoint, consent, catalogue, binding, threshold, freshness, session |

Any **other** exit code is an unexpected fault outside both captures (for example an unhandled
exception during open/close). Classify NOT ACCEPTED and investigate; do not re-run to "see if it
clears".

### 4.8 STOP conditions — classify NOT ACCEPTED and stop

* the exact HEAD differs from `c9c4a2d0ee1cca38bf6dee08ae8fa76f5c6ce6c7`;
* the executable SHA-256 differs from the recorded build identity;
* the catalogue SHA-256 differs from the approved external file;
* the endpoint or the catalogue is missing;
* the selector or the consent is missing or mismatched;
* `WJSS_DEVICE_PROFILE` is not `TEST_HARDWARE`;
* a Trial B or integrated-dry selector is present (or Trial B variables are present);
* `source_tag` is not `AI-003+`;
* `provenance` is not `PHYSICAL_READ`;
* `physical_ai003_verified=FALSE`;
* `quality` is not usable;
* the evidence is stale or future-dated;
* the generation is not live;
* the pressure is at or below the setpoint;
* any IV output changes, or an IV is not confirmed CLOSED;
* a motion session is created or an axis moves;
* water reaches a Water Jet unexpectedly;
* the Pump stop cannot be confirmed;
* the session or the transport does not close cleanly;
* the process exits unexpectedly or without a report;
* the invocation faults (`invocation_status=FAULTED`) — redirection, wait or launch failure;
* any `finally` confirmation is `UNAVAILABLE` or not `YES`;
* the Owner did not confirm the Pump stop.

A STOP is a completed, reportable outcome. It is never repaired by editing an expectation, by raising
the freshness budget, by lowering the setpoint after the fact, or by re-running until it passes.

---

## 5. TRIAL B — physical IV open/close evidence with software-simulated `AI-003+`

### 5.1 What it does, in the exact Product order

1. selector, profile, endpoint, consent, Pump-stopped attestation, valve identity, thresholds,
   supervision window and simulated pressure validated — **before anything is constructed**;
2. catalogue loaded and **all eight** IV command coils and their open/closed feedback rows validated;
3. `ProcessIoDryCommandGate.TryAuthorizeIvTrial` mints the trial write authorization;
4. transport constructed, session opened, generation taken;
5. the simulated Pump-ready sample is minted **for that generation** and must satisfy the existing
   Pump-ready rule, otherwise the trial refuses before touching a valve;
6. initial `DI-021` / `DI-029` read **before any command exists**; only `upper=false, lower=true` admits
   OPEN;
7. OPEN dispatched through the existing session on `DO-023` / coil 22 / `true`;
8. `DI-021` supervised until field-confirmed, contradicted, or the window expires;
9. CLOSE dispatched on the same coil / `false`;
10. `DI-029` supervised the same way;
11. cleanup runs for every outcome and closes **only** valves this trial dispatched OPEN;
12. session closed **last**, transport released, classification printed, exit code returned.

### 5.2 Fixed representative route — do not restate it, do not re-derive it

| Element | Identity |
|---|---|
| WSB | `WSB1` |
| Controller identity | `MC-WSB1` |
| Canonical Water Jet | `WJ1` |
| Legacy identity | Legacy `WJ2` / Cannon 1 |
| Isolation Valve | `IV1` |
| Command tag | `DO-023` |
| FC05 coil | `22` |
| OPEN value | `true` / energised |
| CLOSE value | `false` / de-energised |
| Upper / open feedback | `DI-021`, FC02 address `20` |
| Lower / closed feedback | `DI-029`, FC02 address `28` |
| IV outlet pressure | `AI-004+`, FC04 input register `3` |
| Pump-ready source identity | `AI-003+` |
| Pump-ready evidence used by Trial B | `SOFTWARE_SIMULATED` |

No Sensor, no Galil axis and no Cleaning Path coordinate is needed for Trial B. No Home. No Move. No
P1 through P6.

### 5.3 Initial-state truth table (exact Product rule)

| Upper `DI-021` | Lower `DI-029` | Meaning | Result |
|---|---|---|---|
| `false` | `true` | closed and physically confirmed | **OPEN admitted** |
| `true` | `true` | contradictory feedback | refused `IV_TRIAL_REFUSED_INITIAL_STATE_CONTRADICTORY` |
| `true` | `false` | already open | refused `IV_TRIAL_REFUSED_INITIAL_STATE_UNSAFE` |
| `false` | `false` | closed **not** confirmed | refused `IV_TRIAL_REFUSED_INITIAL_CLOSED_NOT_CONFIRMED` |

"Neither limit asserted" is not evidence that the valve is closed. An absent or unusable Lower
observation reads `false` through the same path, so a missing feedback channel refuses rather than
being treated as safe. None of the three refused rows writes anything.

### 5.4 The simulated Pump-ready value

`WJSS_DRY_IV_TRIAL_SIMULATED_AI003_BAR` must be:

* **explicitly configured** — never defaulted, never reused from a previous run;
* **finite** — `NaN` and the infinities refuse;
* **inside the accepted engineering domain** — `0` … `PumpOutletPressureBar(RawFullScale)`, i.e. the
  Product's own conversion at raw `32767`, which is `40` bar;
* **strictly greater** than the configured `WJSS_PUMP_READY_SETPOINT_BAR`;
* **marked** `SOFTWARE_SIMULATED`, with provenance `SOFTWARE_SIMULATED_AI003_FOR_IV_TRIAL` and purpose
  `IV_OPEN_CLOSE_HARDWARE_TRIAL_ONLY`;
* **never reported as physical** — the report prints `evidence_kind=SOFTWARE_SIMULATED`,
  `physical_pump_verified=FALSE` and `physical_ai003_verified=FALSE` unconditionally;
* **valid only for the current trial generation** — a reopened session is a different generation and
  the sample is refused, never re-stamped.

### 5.5 Physical preconditions — confirm every line before the run

1. ☐ the physical Pump is **STOPPED**;
2. ☐ Pump start is **not authorized** during Trial B;
3. ☐ the Pump-stopped state is **Owner-attested**, not software-verified;
4. ☐ no Pump-driven water flow is expected;
5. ☐ `IV1` is physically **CLOSED**;
6. ☐ `DI-021` Upper/open is **inactive**;
7. ☐ `DI-029` Lower/closed is **active**;
8. ☐ the initial state is **not contradictory** (both limits inactive is also a refusal);
9. ☐ the physical abort is available and the person performing it is named;
10. ☐ the Owner/Operator and an observer can see the `IV1` field state;
11. ☐ `IV2` … `IV8` remain untouched and are not to be operated;
12. ☐ no Galil or motion authority exists;
13. ☐ `WJSS_DRY_COMMAND_MODE` is absent (integrated dry not selected);
14. ☐ **`WJSS_PUMP_READY_READ_TRIAL` is absent** — Trial B does not refuse this itself (§2.1);
15. ☐ endpoint and catalogue are approved Owner-local values.

### 5.6 Execution sequence

Fill the nine placeholders in step 1, then run the block top to bottom in **one** PowerShell
window.

**Guaranteed final state.** The executable is invoked inside `try`, and every closing confirmation —
Pump still stopped, `IV1` physically CLOSED, `DI-029` active, `DI-021` inactive, `IV2`…`IV8`
untouched, no motion, abort still available, release observed — is requested in `finally`. A check
placed after a normal return can be skipped by an invocation, redirection or wait fault; a `finally`
body is reached on every path once the invocation has begun.

**Classification is gated on the finally body.** `$terminalObligationsComplete` starts at `NO` and is
set to `YES` only when all eight answers are `YES`. Any answer that is `UNAVAILABLE` or not `YES`
forces `NOT_ACCEPTED`.

**Product cleanup is never inferred from an operator answer.** `cleanup.complete` and `cleanup.unknown`
are read from the captured trial report, in their own block of the record, labelled as the Product's
own cleanup record. The operator's observed state is written to a *separate* block. The two are never
merged, and an operator "IV1 CLOSED" never stands in for `cleanup.complete=True`.

```powershell
# =====================================================================================
# TRIAL B — PHYSICAL IV OPEN/CLOSE EVIDENCE (SOFTWARE-SIMULATED AI-003)
# One bounded trial. No loop. No retry. Exactly ONE invocation.
#
# LIFECYCLE: the executable is invoked inside try, and every final physical confirmation —
# Pump still stopped, IV1 CLOSED, DI-029 active, DI-021 inactive, IV2..IV8 untouched, no
# motion, abort still available, release observed — runs inside finally. A normal-return
# check could be skipped by an invocation, redirection or wait failure; a finally body
# cannot. The IV is left CLOSED by the Product's own ordered cleanup inside the executable.
#
# LIMIT: if PowerShell itself or the host process cannot continue — hard kill, console
# loss, power loss — finally is NOT guaranteed to run, and the PHYSICAL ABORT is
# immediately authoritative. The maintained coil is the reason that matters: a killed
# process cannot de-energise DO-023 by itself.
# =====================================================================================
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---- step 1 — validated worktree and exact Release output ---------------------------
# ---- Owner-local values: never committed, never printed ------------------------------
$ExpectedHead  = 'c9c4a2d0ee1cca38bf6dee08ae8fa76f5c6ce6c7'
$Repo          = '<FULLY-QUALIFIED-PATH-TO-THE-VALIDATED-WORKTREE>'
$EvidenceDir   = '<FULLY-QUALIFIED-PATH-TO-AN-EVIDENCE-FOLDER-OUTSIDE-THE-REPOSITORY>'
$CataloguePath = '<FULLY-QUALIFIED-PATH-TO-THE-OWNER-LOCAL-CATALOGUE-CSV>'
$EndpointHost  = '<OWNER-LOCAL-BENCH-HOST>'
$EndpointPort  = ''
$EndpointUnit  = ''
$SetpointBar   = '<OWNER-LOCAL-PUMP-READY-SETPOINT-BAR>'
$LowBar        = '<OWNER-LOCAL-PRESSURE-LOW-BAR>'
$HighBar       = '<OWNER-LOCAL-PRESSURE-HIGH-BAR>'
$SupervisionMs = '<OWNER-LOCAL-VALVE-SUPERVISION-MS>'
$SimulatedBar  = '<OWNER-LOCAL-SIMULATED-AI003-BAR-STRICTLY-ABOVE-THE-SETPOINT>'

$Exe      = Join-Path $Repo 'apps\runtime\bin\Release\net10.0\Wjss.Runtime.exe'
$RunLabel = 'TRIAL-B-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$OutFile  = Join-Path $EvidenceDir ($RunLabel + '.stdout.txt')
$ErrFile  = Join-Path $EvidenceDir ($RunLabel + '.stderr.txt')
$RecFile  = Join-Path $EvidenceDir ($RunLabel + '.record.txt')

# ---- helpers ------------------------------------------------------------------------
function Test-FullyQualified {
    param([string] $Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return [System.IO.Path]::IsPathFullyQualified($Value)
}

function Get-CanonicalPath {
    param([string] $Value)
    $full = [System.IO.Path]::GetFullPath($Value)
    $trimmed = $full.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    if ($trimmed.Length -eq 0) { return [string][System.IO.Path]::DirectorySeparatorChar }
    return $trimmed
}

function Get-RedactedText {
    param([string] $Text)
    if ([string]::IsNullOrEmpty($Text)) { return 'NONE' }
    $out = $Text
    if (-not [string]::IsNullOrEmpty($EndpointHost) -and $EndpointHost.Length -ge 3) {
        $out = $out.Replace($EndpointHost, '<endpoint-redacted>')
    }
    if (-not [string]::IsNullOrEmpty($consent) -and $consent.Length -ge 3) {
        $out = $out.Replace($consent, '<consent-redacted>')
    }
    if ($out.Length -gt 300) { $out = $out.Substring(0, 300) + '...' }
    return $out
}

function Read-Confirmation {
    param([string] $Prompt)
    try {
        $answer = Read-Host $Prompt
        if ($null -eq $answer) { return 'UNAVAILABLE' }
        $text = ([string]$answer).Trim().ToUpperInvariant()
        if ($text.Length -eq 0) { return 'UNAVAILABLE' }
        return $text
    }
    catch { return 'UNAVAILABLE' }
}

function Get-ReportField {
    param(
        [Parameter(Mandatory)] [hashtable] $Report,
        [Parameter(Mandatory)] [string]    $Key
    )
    if ($Report.ContainsKey($Key)) { return $Report[$Key] }
    return 'UNAVAILABLE'
}

# ---- placeholder scan — BEFORE any prompt, so nothing is prompted or contacted ------
foreach ($pair in @(
    @{ Name = 'Repo';          Value = $Repo },
    @{ Name = 'EvidenceDir';   Value = $EvidenceDir },
    @{ Name = 'CataloguePath'; Value = $CataloguePath },
    @{ Name = 'EndpointHost';  Value = $EndpointHost },
    @{ Name = 'SetpointBar';   Value = $SetpointBar },
    @{ Name = 'LowBar';        Value = $LowBar },
    @{ Name = 'HighBar';       Value = $HighBar },
    @{ Name = 'SupervisionMs'; Value = $SupervisionMs },
    @{ Name = 'SimulatedBar';  Value = $SimulatedBar })) {
    if ($pair.Value -match '[<>]') {
        throw ("STOP: {0} still contains a placeholder. Nothing was prompted and nothing was contacted." -f $pair.Name)
    }
}

# ---- canonical path validation — file-system strings only, no Hardware contact ------
foreach ($pair in @(
    @{ Name = 'Repo';          Value = $Repo },
    @{ Name = 'EvidenceDir';   Value = $EvidenceDir },
    @{ Name = 'CataloguePath'; Value = $CataloguePath })) {
    if (-not (Test-FullyQualified $pair.Value)) {
        throw ("STOP: {0} must be a fully qualified path. Nothing was prompted and nothing was contacted." -f $pair.Name)
    }
}

# Compared AFTER canonical normalization and against a trailing separator, so a sibling
# folder that merely shares the repository's name prefix is not mistaken for the repository
# and a real sub-folder cannot slip through. Windows path semantics: case-insensitive.
$repoCanonical     = Get-CanonicalPath $Repo
$evidenceCanonical = Get-CanonicalPath $EvidenceDir
$repoPrefix        = $repoCanonical + [System.IO.Path]::DirectorySeparatorChar
$evidencePrefix    = $evidenceCanonical + [System.IO.Path]::DirectorySeparatorChar
if ($evidencePrefix.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'STOP: the evidence folder resolves inside the repository after canonicalization. Nothing was prompted and nothing was contacted.'
}

if (-not (Test-Path -LiteralPath $Exe -PathType Leaf))              { throw 'STOP: the Release executable was not found in the validated worktree.' }
if (-not (Test-Path -LiteralPath $CataloguePath -PathType Leaf))   { throw 'STOP: the Owner-local catalogue file is missing.' }
if (-not (Test-Path -LiteralPath $EvidenceDir -PathType Container)) { throw 'STOP: the evidence folder does not exist.' }

# ---- lifecycle state, initialized before any prompt --------------------------------
$invocationStatus            = 'NOT_ATTEMPTED'
$invocationErrorType         = 'NONE'
$invocationErrorMessage      = 'NONE'
$exitCode                    = -1
$initialUpper                = 'UNKNOWN'
$initialLower                = 'UNKNOWN'
$pumpStillStopped            = 'NOT_CONFIRMED'
$iv1Closed                   = 'NOT_CONFIRMED'
$di029Active                 = 'NOT_CONFIRMED'
$di021Inactive               = 'NOT_CONFIRMED'
$unrelated                   = 'NOT_CONFIRMED'
$noMotion                    = 'NOT_CONFIRMED'
$abortAvailable              = 'NOT_CONFIRMED'
$released                    = 'NOT_CONFIRMED'
$terminalObligationsComplete = 'NO'
$classification              = 'NOT_ACCEPTED'
$failures                    = @()
$map                         = @{}
$consent                     = $null
$secure                      = $null

try {
    # ---- step 2 — clear TargetPath and every WJSS_* variable ------------------------
    Get-ChildItem Env: | Where-Object { $_.Name -like 'WJSS_*' } |
        ForEach-Object { Remove-Item -LiteralPath ('Env:' + $_.Name) -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath 'Env:TargetPath' -ErrorAction SilentlyContinue

    # ---- step 3 — set only the Trial B variables ------------------------------------
    $env:WJSS_DEVICE_PROFILE                   = 'TEST_HARDWARE_DRY'
    $env:WJSS_DRY_IV_TRIAL_MODE                = 'SOFTWARE_SIMULATED_AI003'
    $env:WJSS_DRY_IV_TRIAL_VALVE               = 'IV1'
    $env:WJSS_DRY_IV_TRIAL_PUMP_STOPPED        = 'PUMP_STOPPED_OWNER_ATTESTED'
    $env:WJSS_DRY_IV_TRIAL_SIMULATED_AI003_BAR = $SimulatedBar
    $env:WJSS_PROCESS_IO_HOST                  = $EndpointHost
    $env:WJSS_PROCESS_IO_CATALOGUE             = $CataloguePath
    $env:WJSS_PUMP_READY_SETPOINT_BAR          = $SetpointBar
    $env:WJSS_PRESSURE_LOW_BAR                 = $LowBar
    $env:WJSS_PRESSURE_HIGH_BAR                = $HighBar
    $env:WJSS_VALVE_SUPERVISION_MS             = $SupervisionMs
    if ($EndpointPort -ne '') { $env:WJSS_PROCESS_IO_PORT    = $EndpointPort }
    if ($EndpointUnit -ne '') { $env:WJSS_PROCESS_IO_UNIT_ID = $EndpointUnit }

    # ---- the single-DO consent. Typed, converted once, never echoed or persisted ----
    $secure  = Read-Host -AsSecureString -Prompt 'Enter the published SINGLE-DO consent phrase'
    $consent = [System.Net.NetworkCredential]::new('', $secure).Password
    if ([string]::IsNullOrEmpty($consent)) { throw 'STOP: no consent phrase was supplied. Nothing was contacted.' }
    $env:WJSS_PROCESS_IO_CONSENT = $consent

    # ---- step 4 — prove Trial A and the integrated-dry selector are absent ----------
    $mustBeAbsent = @('WJSS_PUMP_READY_READ_TRIAL', 'WJSS_DRY_COMMAND_MODE', 'WJSS_TC_ACQUISITION_MODE')
    foreach ($name in $mustBeAbsent) {
        $value = [System.Environment]::GetEnvironmentVariable($name, 'Process')
        if (-not [string]::IsNullOrWhiteSpace($value)) { throw "STOP: $name must be absent for Trial B but is present" }
    }

    # ---- steps 5 and 6 — record HEAD, executable SHA-256 and catalogue SHA-256 ------
    $head = (& git -C $Repo rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'STOP: git rev-parse failed in the validated worktree' }
    if ($head -ne $ExpectedHead) { throw "STOP: HEAD is $head, expected $ExpectedHead" }
    $exeSha = (Get-FileHash -LiteralPath $Exe -Algorithm SHA256).Hash.ToLowerInvariant()
    $catSha = (Get-FileHash -LiteralPath $CataloguePath -Algorithm SHA256).Hash.ToLowerInvariant()

    # ---- steps 7, 8 and 9 — record simulated value, thresholds and supervision ------
    Write-Host "simulated_ai003_bar=$SimulatedBar  setpoint=$SetpointBar  low=$LowBar  high=$HighBar  supervision_ms=$SupervisionMs"
    Write-Host 'PUMP_STOPPED_OWNER_ATTESTED — the software does not verify this and never claims to'

    # ---- step 10 — record IV1 initial feedback, and gate on the only admissible state
    $initialUpper = (Read-Confirmation 'IV1 DI-021 Upper/open observed state before the run — type TRUE or FALSE')
    $initialLower = (Read-Confirmation 'IV1 DI-029 Lower/closed observed state before the run — type TRUE or FALSE')
    if ($initialUpper -ne 'FALSE' -or $initialLower -ne 'TRUE') {
        throw 'STOP: IV1 is not physically CLOSED-and-confirmed (FALSE/TRUE). Nothing was commanded.'
    }

    # ---- steps 11 and 12 — invoke EXACTLY ONCE, inside try -------------------------
    try {
        $preAck = Read-Confirmation 'Confirm the Pump is STOPPED, IV1 is CLOSED and the field observer is in position, then type GO'
        if ($preAck -ne 'GO') {
            # Nothing is invoked and nothing is commanded: the gate above already proved the
            # closed state. The finally body still asks for the final physical state.
            $invocationStatus = 'SKIPPED_PRE_RUN_NOT_CONFIRMED'
        }
        else {
            $invocationStatus = 'ATTEMPTED'
            $trial = Start-Process -FilePath $Exe -WorkingDirectory $Repo -NoNewWindow -PassThru -Wait `
                -RedirectStandardOutput $OutFile -RedirectStandardError $ErrFile
            $exitCode         = $trial.ExitCode
            $invocationStatus = 'COMPLETED'
            Write-Host "exit_code=$exitCode"
        }
    }
    catch {
        $invocationStatus       = 'FAULTED'
        $invocationErrorType    = $_.Exception.GetType().Name
        $invocationErrorMessage = 'INVOCATION_EXCEPTION_REDACTED: ' + (Get-RedactedText $_.Exception.Message)
        $exitCode               = -1
        Write-Host ("invocation faulted: " + $invocationErrorType)
    }
    finally {
        # ---- steps 13 to 20 — ALWAYS run once invocation has begun ------------------
        # These are observations of the field, not of the software. The Product's own
        # cleanup record is read separately from the captured report and is never
        # replaced by these answers.
        # Read through the same always-answers helper as the rest of the finally body, so this
        # pause cannot abort the obligations that follow it.
        $null = Read-Confirmation 'Observe IV1 physically now: confirm it is CLOSED, then type OK'

        $pumpStillStopped = Read-Confirmation 'Is the physical Pump still stopped, with no Pump command issued? type YES or NO'
        $iv1Closed        = Read-Confirmation 'Is IV1 physically CLOSED? type YES or NO'
        $di029Active      = Read-Confirmation 'Is DI-029 Lower/closed active on IV1? type YES or NO'
        $di021Inactive    = Read-Confirmation 'Is DI-021 Upper/open inactive on IV1? type YES or NO'
        $unrelated        = Read-Confirmation 'Are the IV2 through IV8 outputs untouched? type YES or NO'
        $noMotion         = Read-Confirmation 'Is there no motion authority and no axis movement? type YES or NO'
        $abortAvailable   = Read-Confirmation 'Did the physical abort remain available throughout? type YES or NO'
        $released         = Read-Confirmation 'Was process exit and session/transport release observed? type YES or NO'

        if ($pumpStillStopped -eq 'YES' -and
            $iv1Closed -eq 'YES' -and
            $di029Active -eq 'YES' -and
            $di021Inactive -eq 'YES' -and
            $unrelated -eq 'YES' -and
            $noMotion -eq 'YES' -and
            $abortAvailable -eq 'YES' -and
            $released -eq 'YES') {
            $terminalObligationsComplete = 'YES'
        }
    }

    # ---- parse the captured report, only when a capture exists ----------------------
    if (Test-Path -LiteralPath $OutFile -PathType Leaf) {
        foreach ($line in Get-Content -LiteralPath $OutFile -Encoding UTF8) {
            $idx = $line.IndexOf('=')
            if ($idx -gt 0) {
                $key = $line.Substring(0, $idx).Trim()
                $val = $line.Substring($idx + 1).Trim()
                if ($key.Length -gt 0 -and -not $map.ContainsKey($key)) { $map[$key] = $val }
            }
        }
    }

    $expected = [ordered]@{
        'accepted'                 = 'True'
        'trial_code'               = 'IV_TRIAL_ACCEPTED'
        'evidence_kind'            = 'SOFTWARE_SIMULATED'
        'physical_pump_verified'   = 'FALSE'
        'physical_ai003_verified'  = 'FALSE'
        'physical_iv_command'      = 'TRUE'
        'physical_upper_feedback'  = 'TRUE'
        'physical_lower_feedback'  = 'TRUE'
        'cleanup.complete'         = 'True'
        'cleanup.unknown'          = '0'
    }

    $failures = @()
    if ($invocationStatus -ne 'COMPLETED') { $failures += "invocation_status=$invocationStatus ($invocationErrorType)" }
    if ($exitCode -ne 0)                   { $failures += "exit_code=$exitCode (expected 0)" }
    foreach ($key in $expected.Keys) {
        if (-not $map.ContainsKey($key)) { $failures += "$key is missing from the report" }
        elseif ($map[$key] -ne $expected[$key]) { $failures += "$key=$($map[$key]) (expected $($expected[$key]))" }
    }
    if ($map.ContainsKey('open_command') -and $map['open_command'] -notlike 'DO-023 coil=22 value=True*') {
        $failures += "open_command is not DO-023/coil 22/True: $($map['open_command'])"
    }
    if ($map.ContainsKey('close_command') -and $map['close_command'] -notlike 'DO-023 coil=22 value=False*') {
        $failures += "close_command is not DO-023/coil 22/False: $($map['close_command'])"
    }
    if ($terminalObligationsComplete -ne 'YES') { $failures += 'final physical-state obligations were not fully confirmed' }

    # ---- redaction scan, then one redacted record outside the repository -----------
    $leaks = @()
    foreach ($file in @($OutFile, $ErrFile)) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
        if (Select-String -LiteralPath $file -SimpleMatch -Pattern $EndpointHost -Quiet -ErrorAction SilentlyContinue) { $leaks += 'endpoint address' }
        if ($consent -and (Select-String -LiteralPath $file -SimpleMatch -Pattern $consent -Quiet -ErrorAction SilentlyContinue)) { $leaks += 'consent phrase' }
    }
    if ($leaks.Count -gt 0) {
        Remove-Item -LiteralPath $OutFile, $ErrFile -Force -ErrorAction SilentlyContinue
        throw "STOP: redaction failure ($($leaks -join ', ')) — captures destroyed, do not paste them anywhere"
    }

    $reportText = if (Test-Path -LiteralPath $OutFile -PathType Leaf) {
        (Get-Content -LiteralPath $OutFile -Encoding UTF8) -join "`n"
    } else { 'NO_CAPTURE_FILE' }

    $classification = if ($terminalObligationsComplete -eq 'YES' -and
                          $invocationStatus -eq 'COMPLETED' -and
                          $failures.Count -eq 0) { 'ACCEPTED_PHYSICAL_IV_COMMAND_AND_FEEDBACK' } else { 'NOT_ACCEPTED' }

    $record = @"
TRIAL B — PHYSICAL IV OPEN/CLOSE EVIDENCE
scope_id=TRIAL-A-B-HARDWARE-EXECUTION-PACKAGE
run_label=$RunLabel
recorded_at_utc=$((Get-Date).ToUniversalTime().ToString('O'))
git_head=$head
executable_sha256=$exeSha
catalogue_sha256=$catSha
endpoint=$(Get-ReportField $map 'endpoint')
selector=WJSS_DRY_IV_TRIAL_MODE=SOFTWARE_SIMULATED_AI003
trial=$(Get-ReportField $map 'trial')
valve=$(Get-ReportField $map 'valve')
pump_ready_source=$(Get-ReportField $map 'pump_ready_source')
pump_ready_evidence=$(Get-ReportField $map 'PUMP_READY_EVIDENCE')
pump_stopped_attestation=PUMP_STOPPED_OWNER_ATTESTED_NOT_SOFTWARE_VERIFIED
simulated_ai003_bar=$SimulatedBar
setpoint_bar=$SetpointBar
low_bar=$LowBar
high_bar=$HighBar
supervision_ms=$SupervisionMs
iv1_initial_upper=$initialUpper
iv1_initial_lower=$initialLower
invocation_status=$invocationStatus
invocation_error_type=$invocationErrorType
invocation_error_redacted=$invocationErrorMessage
exit_code=$exitCode
-- product cleanup record (from the trial report, never from an operator answer) --
cleanup_complete=$(Get-ReportField $map 'cleanup.complete')
cleanup_unknown=$(Get-ReportField $map 'cleanup.unknown')
-- observed final physical state (operator attestation, separate by design) --------
pump_still_stopped=$pumpStillStopped
iv1_final_closed=$iv1Closed
di029_lower_closed_active=$di029Active
di021_upper_open_inactive=$di021Inactive
unrelated_iv_untouched=$unrelated
motion_absent=$noMotion
physical_abort_available=$abortAvailable
session_closed_transport_released=$released
terminal_obligations_complete=$terminalObligationsComplete
classification=$classification
failures=$($failures -join ' | ')
-- report --
$reportText
"@
    Set-Content -LiteralPath $RecFile -Value $record -Encoding UTF8

    Write-Host "classification=$classification"
    Write-Host "record=$RecFile"
    if ($failures.Count -gt 0) { Write-Host ("failures: " + ($failures -join ' | ')) }
}
finally {
    # Consent cleanup is guaranteed once the phrase has been converted for the child, on
    # every path: accepted, refused, faulted or thrown. The phrase is never printed.
    Remove-Item -LiteralPath 'Env:WJSS_PROCESS_IO_CONSENT' -ErrorAction SilentlyContinue
    $consent = $null
    if ($null -ne $secure) {
        try { $secure.Dispose() } catch { }
        $secure = $null
    }
}
```

### 5.7 Required evidence fields

| Field | Meaning | Accepted value |
|---|---|---|
| `git_head`, `executable_sha256`, `catalogue_sha256`, `endpoint` | as Trial A | recorded |
| `trial` | surface token | `--dry-iv-trial` |
| `valve` | commanded valve | `IV1` |
| `pump_ready_source` | identity the rule consumes | `AI-003+` |
| `PUMP_READY_EVIDENCE` | evidence class of the Pump-ready input | **`SOFTWARE_SIMULATED`** |
| `PHYSICAL_PUMP_EVIDENCE` | Pump evidence claim | `NOT_ESTABLISHED_BY_THIS_TRIAL` |
| `PUMP_STOPPED` | attestation line | `PUMP_STOPPED_OWNER_ATTESTED / NOT SOFTWARE_VERIFIED` |
| `evidence_kind` | reported class | **`SOFTWARE_SIMULATED`** |
| `trial_purpose` | single permitted use | `IV_OPEN_CLOSE_HARDWARE_TRIAL_ONLY` |
| `physical_pump_verified` | Pump verification | **`FALSE`** |
| `physical_ai003_verified` | `AI-003` verification | **`FALSE`** |
| `physical_iv_command` | OPEN actually dispatched | **`TRUE`** |
| `open_command` | dispatched OPEN | `DO-023 coil=22 value=True admission=… dispatch=…` |
| `physical_upper_feedback` | `DI-021` confirmed | **`TRUE`** |
| `open_observation` | open stage record | `… upper=True lower=… field_confirmed=True fc01=… pressure_bar=…` |
| `close_command` | dispatched CLOSE | `DO-023 coil=22 value=False admission=… dispatch=…` |
| `physical_lower_feedback` | `DI-029` confirmed | **`TRUE`** |
| `closed_observation` | close stage record | `… upper=False lower=True field_confirmed=True …` |
| `fc01` (inside both observations) | FC01 process-image read-back | recorded **separately**; never field confirmation |
| `pressure_bar` (inside both observations) | physical `AI-004+` | recorded separately |
| `cleanup.complete` | cleanup completed | **`True`** |
| `cleanup.unknown` | remaining unknown valve state | **`0`** |
| `cleanup.attempted` / `.closed` / `.refused` / `.transport_failures` | cleanup categories | recorded |
| `accepted` | trial verdict | **`True`** |
| `trial_code` | classification | `IV_TRIAL_ACCEPTED` |
| `exit_code` | process exit code | **`0`** |
| `pump_still_stopped` | Pump remained stopped, no Pump command | **`YES`** |
| `iv1_final_closed` | `IV1` physically CLOSED | **`YES`** |
| `di029_lower_closed_active` | `DI-029` active | **`YES`** |
| `di021_upper_open_inactive` | `DI-021` inactive | **`YES`** |
| `unrelated_iv_untouched` | `IV2`…`IV8` outputs untouched | **`YES`** |
| `motion_absent` | no motion authority, no movement | **`YES`** |
| `physical_abort_available` | abort remained available throughout | **`YES`** |
| `session_closed_transport_released` | operator attestation — the runner prints no session field (unlike Trial A), so this is observed, not quoted | **`YES`** |
| `terminal_obligations_complete` | every `finally` answer affirmative | **`YES`** |
| `invocation_status` | `COMPLETED` only; `FAULTED`, `SKIPPED_*` and `NOT_ATTEMPTED` all force `NOT_ACCEPTED` | **`COMPLETED`** |
| `invocation_error_type` / `..._redacted` | exception type and redacted message | `NONE` |
| `cleanup_complete` / `cleanup_unknown` | the Product's own cleanup record, quoted from the report | `True` / `0` — never inferred from an operator answer |

Two printing conventions in this report are **exactly as Source emits them** and must not be "corrected"
in a transcript: `accepted=True` and `cleanup.complete=True` are Pascal case, while the `physical_*`
flags are upper case `TRUE`/`FALSE`.

### 5.8 Acceptance

Trial B is accepted as **`PHYSICAL_IV_COMMAND_AND_FEEDBACK`** only when every line holds:

* exit code **0**;
* `evidence_kind=SOFTWARE_SIMULATED` and `physical_pump_verified=FALSE`;
* `physical_ai003_verified=FALSE` — the simulated input never becomes physical evidence;
* the physical IV OPEN was dispatched through the existing session;
* `DO-023` / coil `22` is the only IV command output used;
* Upper/open confirmation came from the physical `DI-021`;
* the CLOSE command was dispatched `false` / de-energised on the same coil;
* Lower/closed confirmation came from the physical `DI-029`;
* FC01 was recorded separately and was never treated as field confirmation;
* cleanup completed with **zero** remaining unknown state;
* unrelated IV outputs untouched, Pump still stopped, no motion;
* `IV1` ends physically **CLOSED**, `DI-029` active and `DI-021` inactive;
* the process has exited with no open connection remaining — Trial B prints no `session_state` or
  `transport_released` field, so the release is confirmed by observation and recorded as such;
* `invocation_status=COMPLETED`;
* `terminal_obligations_complete=YES` — the `finally` body ran with all eight answers affirmative;
* cleanup fields quoted from the report, not reconstructed from operator confirmations.

**A passing Trial B proves nothing about the Pump, `AI-003+` or water-on behaviour.** It is not Pump
evidence and must never be cited as Pump evidence.

### 5.9 Exit codes

| Code | Meaning |
|---|---|
| `0` | trial completed and accepted |
| `2` | composition or sequence refusal, unexpected fault, or operator abort after the selector was accepted |
| `3` | supervision timeout before a terminal result |
| `4` | invocation, selector or configuration refusal before anything was constructed |

### 5.10 STOP conditions — classify NOT ACCEPTED and stop

* the Pump is running, or the Pump-stopped attestation is uncertain;
* the initial closed state is not physically confirmed;
* both limits active, or both limits inactive;
* the integrated-dry selector or the **Trial A selector** is present;
* the simulated pressure is missing, non-finite, outside the domain, or at/below the setpoint;
* the consent is missing or incorrect;
* endpoint or catalogue validation fails;
* session admission fails;
* the OPEN command is not dispatched;
* Upper/open feedback is stale, unavailable or contradictory;
* the wrong IV or output is commanded;
* the CLOSE command is not dispatched;
* Lower/closed feedback is stale, unavailable or contradictory;
* cleanup is incomplete, or remaining valve state is unknown;
* any unrelated IV output changes;
* a Pump command occurs;
* a motion session is created;
* water flow occurs unexpectedly;
* the operator physical abort becomes unavailable;
* the invocation faults (`invocation_status=FAULTED`);
* any `finally` confirmation is `UNAVAILABLE` or not `YES`.

### 5.11 Operator abort

* **Ctrl+C requests Trial B cancellation.** The console handler sets a trial-local flag and suppresses
  default termination; it holds no device, session or transport reference, so it cannot write a coil,
  close a session or dispose anything.
* The **main execution path** performs the ordered cleanup: the bounded sequence stops at its next
  observation point, `SafeCleanupCloseAll` closes **only** valves this trial dispatched OPEN, the
  session closes **after** cleanup, and the transport is released after the session.
* A **hard process kill is not the primary abort.** If the process is killed while the coil is
  energised, the maintained coil can stay energised — that case needs the physical abort.
* **The physical abort remains authoritative** whenever orderly software cleanup cannot complete.
* After any abort the Operator must confirm **Pump stopped** and **`IV1` physically CLOSED** before any
  second attempt. A second attempt is a new run with a new record, and it needs its own authorization.

Trial A has no such handler by design (§4.2): nothing is energised, so Ctrl+C simply ends the process.

---

## 6. Evidence separation

### 6.1 The two records side by side

| | Trial A | Trial B |
|---|---|---|
| Pump | **REAL** (running, Owner authority) | **STOPPED** (Owner-attested) |
| `AI-003+` | **REAL physical read** | **SOFTWARE_SIMULATED** |
| IV command | **NONE** | **REAL** (`DO-023` / coil 22) |
| Upper / lower feedback | NONE | **REAL** (`DI-021` / `DI-029`) |
| Motion | NONE | NONE |
| Evidence produced when accepted | `PHYSICAL_PUMP_READY` | `PHYSICAL_IV_COMMAND_AND_FEEDBACK` |
| Pump evidence produced | YES | **NONE** |

### 6.2 Four statements that must never be written

1. "Pump and IV were executed together." — They were never executed together.
2. "Trial B proves the Pump-ready path." — Trial B's Pump-ready input is simulated.
3. "Trial A proves the valve command path." — Trial A commands nothing.
4. "Trial B's simulated `AI-003+` was validated by the physical transmitter." — It is a configured
   number, not a measurement.

### 6.3 The endpoint digest is not comparable between the two trials

This is exact Source behaviour and a common misreading:

* **Trial A** prints `endpoint=` the handle's own fingerprint —
  `endpoint-sha256-prefix:<12 hex>` of `host:port:unitId`.
* **Trial B** prints `endpoint=` a digest of the **already-redacted** string
  `PROCESS_IO_ENDPOINT_CONFIGURED/<fingerprint>` (`ProcessIoDryCommandGate.EndpointFingerprint`).

The same endpoint therefore produces **two different** printed digests. Neither is wrong, and neither
may be "reconciled": they are different derivations by design. Record both as they appear.

---

## 7. Cross-WSB evidence position

This package deliberately contains **no per-WSB trial matrix** and requires no per-WSB repetition.

* The commissioned **Legacy Actual Run** remains authoritative for physical wiring, mechanical routing
  and every WSB **while the Hardware is unchanged**.
* **Trial A is a shared Pump Outlet observation** of `AI-003+` and is **not repeated per WSB**.
* **Trial B representative command-equivalence evidence uses `WSB1` / `IV1`.**
* When the WJSS command semantics, parameters and canonical routing match the commissioned Legacy
  behaviour, **the representative result extends to `IV2` … `IV8` and to `WSB2` … `WSB4`**.
* **No per-WSB Hardware repetition is required.**
* A **new Scope/Evidence Gate is required only after** a Hardware, wiring, mechanical, command-sequence,
  parameter or canonical-topology change.

Trial B asserts **extendability only**. It does not claim that any other valve or WSB was executed.

---

## 8. What this package does not change

No Product Source, test, project file, solution file, dependency, lock file, generated file, fixture,
canonical topology, Process-I/O binding, Trial A behaviour, Trial B behaviour or Motion behaviour is
modified by this package. It adds one document and one cross-reference.

---

## 9. Boundaries

| Boundary | State |
|---|---|
| PHYSICAL HARDWARE | **NOT EXECUTED / NOT VERIFIED** by this package |
| PRODUCTION | **NOT AUTHORIZED** |
| MERGE | **HOLD** |
| FINAL READINESS REVIEW | **NOT STARTED** |

Trial A and Trial B each require a **separate** Owner authorization, one execution, one classification
and one close. Nothing in this document authorizes either run, and nothing in it may be cited as
evidence that either run occurred.
