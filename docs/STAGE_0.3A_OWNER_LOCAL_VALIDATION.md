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

Required: .NET **10.0 SDK** (any 10.0.x servicing band; record the exact version in the
evidence template). There is intentionally **no `global.json`** in the repository — pinning a
SDK would be an unverified claim. If `dotnet` is missing or < 10.0, **STOP**: install/update the
SDK, then start at step 1. Record the outcome even in this case.

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
GENERATES a `packages.lock.json` per project. Leave the generated set **untracked and
untouched** throughout validation — do not delete, clean, or edit them, and do not commit them
during the run. They are assessed and committed only after the FULL validation passes, by
explicit Owner decision (round-3 policy of 2026-10-07; supersedes the earlier "do not commit
one from this run" shorthand).

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
  packages.lock.json generated (left untracked): count ___
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
(typecheck + 14 structural tests + fixture validation), the boundary scan, and the
documentation/consistency checks recorded in the PR description. The fixtures remain
`PROVISIONAL STRUCTURAL FIXTURE` until step 5 replaces them through the .NET generator.
