# Stage 0.3A-1 — Owner-Local .NET Validation Runbook

- **Audience:** the Owner (or a delegate with the Owner's instruction) on the Windows
  development workstation. **Not runnable in Arena — that is the entire reason this file
  exists.**
- **What it proves:** the committed .NET sources compile, the xUnit suites pass, the golden
  fixtures regenerate byte-identical (contract authority is the .NET generator, not Node), and
  the TypeScript mirror + boundary scan agree with the committed artifacts.
- **Gate semantics:** a PASS of every step below is a **mandatory pre-merge gate** for PR
  "Stage 0.3A-1: product foundation source skeleton". Any FAIL = the PR stays open, no merge,
  no advance to 0.3A-2 (Option-C amended gate).

---

## 0. Mandatory first step

```powershell
dotnet --version
```

Required: .NET SDK **10.0.401** exactly as pinned by the repository-root `global.json`
(`rollForward: latestPatch` — the Owner-validated stable SDK; the pin was added at the
2026-10-07 closeout, superseding the former intentional absence, which existed only while no
build had ever run; no Preview/RC is referenced or accepted). `dotnet --version` must satisfy
the pin; record the exact version in the evidence template. If `dotnet` is missing or does not
satisfy the pin, **STOP**: install/update the SDK, then start at step 1. Record the outcome even
in this case.

## 0A. Pre-build environment check (added after round 2)

```powershell
$env:TargetPath
```

MSBuild imports valid environment-variable names as global properties, and a
`TargetPath` defined in the current PowerShell process **overrides normal
project-output resolution** for every project in the build — producing
cascading reference/namespace failures in perfectly good source. This is a
local validation-environment prerequisite, **not a WJSS Product dependency**;
the Product tree must never add paths, references, or workarounds to
accommodate an external environment variable.

If `TargetPath` is defined:

```powershell
$originalTargetPath = $env:TargetPath   # record it in the evidence template
Remove-Item Env:TargetPath -ErrorAction SilentlyContinue
```

- This removes the override **from the current PowerShell process only**.
- **Do NOT delete or modify the User-level or Machine-level environment
  variable** — that belongs to whatever external toolset installed it.
- Record the presence of the override (and its value if the Owner chooses to
  quote it in the evidence comment) in the evidence template below; the
  validation run itself is performed with the process-scope override removed.

## 1. Checkout the PR head

```powershell
git fetch origin pull/PR_NUMBER/head:stage-03a1-validation
git checkout stage-03a1-validation
git rev-parse HEAD          # record in evidence template
```

Worktree must be clean before starting: `git status --porcelain` → empty.

## 2. Restore (first network contact the proposed pins ever get)

```powershell
dotnet restore WaterJetSentinelSuite.sln
```

The NuGet pins (`Microsoft.NET.Test.Sdk 17.12.0`, `xunit 2.9.2`, `xunit.runner.visualstudio
2.8.2`) are **PROPOSED / UNVERIFIED** — this step is their first-ever resolution attempt.
If restore fails for version reasons, note the exact error; substituting a nearby version is
allowed **only** if recorded in the evidence template as a pin amendment (and mirrored into
`Directory.Packages.props` in the fix commit). Lock files: the first real restore legitimately
GENERATES a `packages.lock.json` per project. Leave the generated set **untouched during the
run** — never hand-author or edit a lock file. They are assessed and committed only after the
FULL validation passes, by explicit Owner decision (round-3 policy of 2026-10-07). **Status at
closeout:** this gate has been satisfied — the twelve genuine Owner-local lock files were
committed via the handoff (`488b98fb…`) after the passing run; future restores must keep them
in sync through real restores, not manual edits.

## 3. Build everything (including the Windows-only kiosk)

```powershell
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore
```

`Wjss.Kiosk` targets `net10.0-windows` — expected to build only on Windows; on this box that
condition is satisfied by definition. Zero warnings tolerated? — **no:** record any warnings
verbatim in the evidence template; triage is a Stage 0.3A-6 item unless a warning contradicts
ADR-0014.

## 4. Run every test project

```powershell
dotnet test WaterJetSentinelSuite.sln -c Release --no-build
```

All five test projects must pass, notably:

- `Wjss.Runtime.Api.Tests` — health stub 200/503 semantics (the **only** executed proof that
  the stub returns what the plan says).
- `Wjss.Config.Examples.Tests` — committed `config/examples/*.example.json` parse against the
  contract types and are simulator-only.
- `Wjss.FixtureEmission.Tests` — the parity gate; see next step.

## 5. Fixture regeneration check (contract authority)

> **Regeneration ordering gate (Owner, round 5 of 2026-10-07).** Do NOT run the
> `WJSS_UPDATE_FIXTURES=1` regeneration in this step unless BOTH prerequisites are already
> GREEN in the same session: (1) a successful **full-solution Release build** (steps 2-3),
> and (2) **all non-parity test projects passing** (step 4). Fixture drift observed before
> those gates is expected provisional-fixture evidence — record it in the evidence template,
> do not treat it as a failed gate and do not regenerate to hide it. Current Arena-recorded
> state (2026-10-07): drift on `snapshot.seed0.json`, `delta.basic.json`,
> `sensor-map.example.json` is EXPECTED provisional drift pending the gates above.

```powershell
$env:WJSS_UPDATE_FIXTURES = "1"
dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release
Remove-Item Env:WJSS_UPDATE_FIXTURES
git status --porcelain packages/contracts/fixtures/ config/examples/
```

Expected: **empty `git status`** — the .NET-emitted fixtures are byte-identical to the
committed (Node-authored, PROVISIONAL) files except, by design, where the committed files
carry the `fixtureStatus` PROVISIONAL marker. Handle precisely:

- Only difference is removal/normalization of the `fixtureStatus` line in regenerated files →
  commit the regenerated fixtures, replacing the provisional ones; update
  `packages/contracts/fixtures/README.md` and `tools/fixtures/README.md` to drop the
  PROVISIONAL label and this file's §9 wording to "regenerated and committed by .NET
  generator". **Then re-run steps 3–5 to confirm idempotency (empty diff).**
- Any structural difference (field renamed, value differs, ordering differs) → **STOP. No
  auto-fixing.** This is a C#/TS drift finding. Record it verbatim; the fix direction (C#
  generator vs. mirror) is an Owner call per ADR-0014 §1.

> **Sensor-map regeneration after the tcChannels shape correction (round 6).** The
> committed `config/examples/sensor-map.example.json` was regenerated in Arena into the
> array form for TypeScript validation only and remains PROVISIONAL until confirmed by
> the .NET generator. Sequence after the Owner reviews/pulls the fix commit:
>
> 1. Preserve the rejected fixture set (the drifted `snapshot.seed0.json`,
>    `delta.basic.json`, and comma-delimited `sensor-map.example.json`) as validation
>    evidence — copy them to a scratch location outside the repository first.
> 2. Reset the three drifted files to their committed state:
>    `git checkout -- packages/contracts/fixtures/snapshot.seed0.json packages/contracts/fixtures/delta.basic.json config/examples/sensor-map.example.json`
> 3. Pull the correction commit and rebuild the full solution in Release (0 warnings /
>    0 errors required).
> 4. Rerun the API, domain, config, runtime, and integration test projects against fresh
>    assemblies (parity may still report drift — that is exactly what this sequence closes).
> 5. Run the regeneration gate above (`WJSS_UPDATE_FIXTURES=1`).
> 6. Review the generated `sensor-map.example.json` diff: `tcChannels` must be arrays of
>    exactly two channel strings on every SENSOR slot and absent on CANNON slots; a scalar
>    string anywhere fails the gate (ADR-0014 decision 8).
> 7. Run `npm run check` in `packages/contracts/wjss-contracts-ts`.
> 8. Rerun fixture/delta parity tests — they must pass with no residual diff beyond the
>    `fixtureStatus` marker handling above.
> 9. Rerun the full test suite and record the results in the PR. Do not merge while any
>    drift or any scalar `tcChannels` emission remains.

## 6. TypeScript mirror + structural validator

```powershell
Set-Location packages/contracts/wjss-contracts-ts
npm ci
npm run check          # typecheck + node --test + fixture validation
Set-Location ../../..
node tools/boundary-scan/boundary-scan.mjs .   # must print: 0 findings
```

(`npm ci` must hit the Owner's normal registry; if the workstation mirrors npm like it mirrors
NuGet, use the mirror.) Expected: 24/24 tests pass, including the new sensor-map block
that covers the tcChannels array contract (ADR-0014 decision 8); both fixture sets
validate; boundary scan exits 0.

## 7. Optional smoke (recommended, quick)

```powershell
dotnet run --project apps/runtime -c Release
# new shell:
curl.exe -s http://127.0.0.1:5181/api/v1/health/live     # expect 200 JSON
curl.exe -s http://127.0.0.1:5181/api/v1/health/ready    # expect 503 RUNTIME_NOT_IMPLEMENTED
# Ctrl-C the server
```

## 8. Evidence template — paste into the PR comment

```text
STAGE 0.3A-1 OWNER-LOCAL VALIDATION
date (local): ______  machine: ______
dotnet --version: ______
git rev-parse HEAD: ______
0A env: TargetPath defined at session start? YES/NO (if YES: removed process-scope only;
   value recorded here only if the Owner elects to quote it): ______
2 restore: PASS/FAIL   notes: ______
  resolved pins: Microsoft.NET.Test.Sdk ___ xunit ___ xunit.runner.visualstudio ___
  packages.lock.json committed (genuine, from the passing restore): count 12 at closeout
3 build (Release, sln): PASS/FAIL   warning count: ___ (list below if >0)
4 dotnet test (sln): PASS/FAIL
  Wjss.Domain.Tests ___ / Wjss.Runtime.Core.Tests ___ / Wjss.Runtime.Api.Tests ___
  Wjss.Config.Examples.Tests ___ / Wjss.FixtureEmission.Tests ___
5 fixture regeneration: IDENTICAL / PROVISIONAL-MARKER-ONLY / DRIFT
  (if DRIFT — verbatim diff):
6 npm run check: PASS/FAIL (tests ___/15)   boundary-scan (S1-S8): findings ___
7 health smoke: live ___ / ready ___
verdict: PASS / FAIL — stages 2-6 all PASS and 5 ≠ DRIFT
pin amendments made (if any): ______
```

A PASS verdict authorizes the Owner to merge the 0.3A-1 PR and to authorize **0.3A-2**
explicitly. A FAIL keeps the PR open; no substage advances on partial passes.

## 9. What this runbook never claims

Until the completed template exists in the PR: no claim that anything compiles, restores,
passes, or runs. Arena-side validation covers only: JSON parseability, the TypeScript mirror
(typecheck + 24 structural tests + fixture validation), the boundary scan, and the
documentation/consistency checks recorded in the PR description. (Closeout of 2026-10-07:
the template was completed and posted; the Owner-local verdict is recorded in §10; the
committed fixtures are the genuine .NET-generated files transferred from the validated
Working Tree — the in-file `fixtureStatus` PROVISIONAL label persists only because the
generator constant still writes it.)

## 10. FINAL VERDICT — Stage 0.3A-1 (Owner-local, 2026-10-07)

**STAGE 0.3A-1 OWNER-LOCAL VALIDATION PASSED.** Recorded from the Owner's authoritative run
at predecessor `685e056` / final test state `a9ef185` (full Release build completed
successfully immediately before the fresh full-suite run, which is what makes the result
authoritative):

| Gate | Result |
| --- | --- |
| Release build (full solution) | **PASS** — 0 warnings, 0 errors, 12/12 projects, 3.7 s |
| Fresh full .NET suite | **61 / 61 passed**, 0 failed, 0 skipped (5.5 s) |
| .NET SDK / xUnit runtime | **10.0.401** / .NET 10.0.12 |
| TypeScript mirror | **24 / 24 PASS** |
| Fixture parity (FixtureEmission) | **7 / 7 PASS** |
| Boundary scan | **0 findings**, S1–S9 clean |
| Health endpoints | live **200** (`ALIVE`, `SIMULATOR`, `runtimeImplemented=false`, `STAGE_03A1_SKELETON`); ready **503** (`RUNTIME_NOT_IMPLEMENTED`) |
| Profile fail-closed gates | SIMULATOR starts; TEST_HARDWARE refused exit **2**; PRODUCTION refused exit **2**; invalid profile refused exit **4**; no silent fallback with `--no-launch-profile` |
| Port collision (5181) | explicit refusal, exit **3** |
| tcChannels structural contract | 108 slots / 106 sensors / 2 cannons / exact-two arrays / 212 = 212 unique / 0 duplicates / I7+I16 channel-free / walls 24-29-24-29 (CLR `System.Object[]`) |
| Environment prerequisite (§0A) | `TargetPath` (external toolset, process-scoped) — removal resolves; **not** a WJSS defect; runbook keeps process-scope cleanup only |
| Artifacts at closeout | 12 genuine `packages.lock.json` + 3 .NET-generated fixtures transferred from the validated Working Tree (handoff `488b98fb…`); Arena generated none |

**Outcome: PR #4 READY FOR OWNER MERGE (not merged — the Agent never merges).** Stage 0.3A-2
NOT AUTHORIZED. Production device access NOT AUTHORIZED.

## 11. Test policy for subsequent checkpoints (agreed at closeout)

Add automated tests **only** for: product behavior; safety invariants; public or wire
contracts; deterministic state transitions; cross-language parity; and meaningful regressions
with product impact. Do **not** add tests solely for: syntax errors, missing imports,
compiler-detectable mistakes, analyzer-detectable patterns, documentation wording, JSON
whitespace or formatting, private implementation details, or test code testing other test
code. Rely instead on: the compiler, analyzers, XML/JSON parsers, the static boundary scanner,
and focused semantic code review. Existing meaningful contract, mapping, startup-safety and
parity tests stay.

## 12. Stage 0.3A-2A Checkpoint A — Owner-local validation (appended 2026-10-07)

Checkpoint A (Runtime State Foundation, SIMULATOR-only) changes `packages/application`,
`packages/time`, `adapters/simulator` and `tests/runtime.tests` only. `apps/runtime` is
**unchanged**, so the §7 health smoke still returns the 0.3A-1 answers
(`live` 200 ALIVE/SIMULATOR/`runtimeImplemented=false`/`STAGE_03A1_SKELETON`; `ready` 503
`RUNTIME_NOT_IMPLEMENTED`) and is expected to stay that way until Checkpoint C.

Prerequisites are §0 (SDK **10.0.401** exactly) and §0A (no process-scope `TargetPath`).

```powershell
git fetch origin arena/873f0015-waterjet-sentinel-suite
git switch arena/873f0015-waterjet-sentinel-suite

# FIRST restore after the new project reference (see the lock note below).
# This is a real restore: it refreshes tests/runtime.tests/packages.lock.json.
dotnet restore WaterJetSentinelSuite.sln

dotnet build WaterJetSentinelSuite.sln -c Release --no-restore
dotnet test WaterJetSentinelSuite.sln -c Release --no-build
```

Expected: Release build 0 warnings / 0 errors; the 61 existing tests still pass; the new
Runtime-core tests in `Wjss.Runtime.Core.Tests` pass (Arena executes nothing, so the count is
**not claimed** here — record the observed numbers in the PR comment). Optional focused run:

```powershell
dotnet test tests/runtime.tests -c Release --no-build --logger "console;verbosity=detailed"
```

**Lock-file note (round-3 policy still applies).** Checkpoint A adds
`Wjss.Adapters.Simulator` as a project reference of `tests/runtime.tests` (the composition the
runtime will use: SIMULATOR source → contracts → Runtime core). The affected
`tests/runtime.tests/packages.lock.json` must therefore be refreshed **by the real restore
above**, never hand-edited, and committed with the checkpoint review. The other eleven lock
files are expected to be unchanged; report any other diff instead of accepting it silently.

**Expected evidence to record.** Build result and warning count; per-project test counts; the
boundary scan result (`node tools/boundary-scan/boundary-scan.mjs .` — must exit 0); the
`tests/runtime.tests/packages.lock.json` diff; a statement of anything that differs from the
expectations above. If any Runtime-core test fails, that is a Checkpoint A defect: stop and
report it rather than adjusting the test to match the code.

### 12.1 Checkpoint A Owner-local result (2026-10-07) — PASSED

Recorded from the Owner's run; Arena did not execute any .NET command.

| Item | Observed |
| --- | --- |
| Validated feature head | `a512aa76c4b2d7633d2a83b10c523c318b3a420e` |
| Lock-refresh commit (genuine restore output) | `55d3b8b4b7d7ba51b28a0b66adb7445e7ffb579c` — one file: `tests/runtime.tests/packages.lock.json`, +7 lines, adds `wjss.adapters.simulator` (Project) with `Wjss.Contracts` + `Wjss.Time`; no absolute path |
| .NET SDK / xUnit runtime | `10.0.401` / .NET 10.0.12 |
| Release build | **PASS — 0 warnings, 0 errors** |
| Full .NET tests | **101 total / 101 passed / 0 failed / 0 skipped** |
| Locked restore | **PASS** |
| Boundary scan (S1–S9) | **0 findings** |
| Working Tree | **CLEAN** |

Correction chain validated by this run: `b389805` (CA1859), `93e8f24` (CS0051), `b75bccb`
(synthetic-example test compile), `a512aa7` (SensorChannels tamper isolation). Checkpoint A is
complete for a development checkpoint and **not merged**. **Checkpoint B is authorized**; its
Owner-local commands are recorded in §13 of this runbook. Checkpoint C remains NOT AUTHORIZED;
TEST_HARDWARE and PRODUCTION remain NOT AUTHORIZED.

**Recorded discrepancy (not silently repaired).** Section 7 above writes the smoke paths as
`/api/v1/health/live` and `/api/v1/health/ready`, which do not match the implemented route
constants (`ApiRoutes.HealthLive` = `/health/live`, `ApiRoutes.HealthReady` = `/health/ready`,
the paths the Owner's Stage 0.3A-2 instruction also names). Section 7 is a dated Stage 0.3A-1
record and is left unmodified; the Owner-local smoke for this checkpoint should use
`http://127.0.0.1:5181/health/live` and `http://127.0.0.1:5181/health/ready`.

## 13. Stage 0.3A-2A Checkpoint B — Owner-local validation (appended 2026-10-07)

Checkpoint B is **source authored in Arena and NOT COMPILED / NOT EXECUTED there**. The
Owner-local run below is the validation of record, and the mandatory gate before the
checkpoint can be treated as validated. No hardware, no device, no Production profile.

Run from the repository root on the Owner workstation (SDK exactly `10.0.401`), on the
Checkpoint B commit:

| # | Step | Command | Expected |
| --- | --- | --- | --- |
| 1 | Genuine restore (only if a Project or Package reference changed; Checkpoint B changed neither) | `dotnet restore WaterJetSentinelSuite.sln` | Success; if a real restore rewrites a `packages.lock.json`, commit that genuine output — never hand-edit it. No lock churn is expected from Checkpoint B |
| 2 | Locked-restore verification | `dotnet restore WaterJetSentinelSuite.sln --locked-mode` | Success against the committed lock files |
| 3 | Release build | `dotnet build WaterJetSentinelSuite.sln -c Release --no-restore` | **0 warnings, 0 errors** |
| 4 | Full test suite | `dotnet test WaterJetSentinelSuite.sln -c Release --no-build` | All tests pass, including the new evolution / Delta / apply / history tests. Checkpoint A recorded 101/101; the total rises by the number of new tests |
| 5 | Boundary scan | `node tools/boundary-scan/boundary-scan.mjs .` | Exit 0 — **0 findings (S1-S9 clean)** |
| 6 | Working Tree | `git status` | Clean after the run and any genuine lock refresh |

**What the Owner-local run must demonstrate for Checkpoint B.** Each item is covered by an
automated test in `tests/runtime.tests/`; no item is claimed as executed in Arena.

1. Repeatable evolution from the same seed, tick sequence and clock: the same state and the
   same serialized Snapshot/Delta sequence.
2. Revision progression: +1 exactly once per accepted tick, one committed state per tick,
   and refusals committing nothing.
3. A gapless Delta chain whose `previousRevision` / `revision` / `generatedAt` agree with the
   committed revisions, with no duplicate and no skipped revision.
4. Bounded trend: capacity respected, oldest point dropped deterministically, and published
   windows never mutated.
5. Bounded Delta history: oldest-entry eviction, newest revision discoverable, and refused
   transitions never recorded.
6. Gap detection: a revision mismatch yields a machine-readable fresh-Snapshot-required result
   and applies nothing; catch-up with a missing link never fabricates a Delta.
7. Snapshot reconstruction parity: applying the Deltas in order reproduces direct evolution
   through the Snapshot projection.
8. The three Active Job states on the wire: absent key, object, explicit null.
9. `TEST_HARDWARE` and `PRODUCTION` remain refused at the startup gate and the evolution gate.

### 13.1 Checkpoint B Owner-local result (2026-10-07) — PASSED

Recorded from the Owner's run; Arena did not execute any .NET command.

| Item | Observed |
| --- | --- |
| Validated head | `cfa6d4a376bbf10a87db2349045cb6b9b57bb544` (consolidated correction after the Checkpoint B feature commit `619f999`) |
| Release build | **PASS — 0 warnings, 0 errors** |
| Full .NET tests | **129 total / 129 passed / 0 failed / 0 skipped** |
| Locked restore | **PASS** |
| Lock drift | **NONE** |
| Boundary scan (S1–S9) | **0 findings** |
| Working Tree | **CLEAN** |

Correction rounds validated by this run: `dd45bf6` (CS0102), `8692b77` (test-source alignment),
`cfa6d4a` (consolidated). **Product behaviour changed in exactly one place** —
`RuntimeDeltaHistory.CatchUpFrom` now selects the Delta that continues the consumer revision
(`previousRevision` match) rather than the Delta whose own revision equals it; the other five
findings were test-side (structural `TrendPoint` comparison via
`RuntimeTestFixture.AssertTrendPointsEquivalent`, and three re-derived revision expectations).
Checkpoint B is complete for a development checkpoint and **not merged**. **Checkpoint C is
authorized**; `TEST_HARDWARE` and `PRODUCTION` remain NOT AUTHORIZED.
