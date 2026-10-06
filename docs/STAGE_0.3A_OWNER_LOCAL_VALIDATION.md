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
`Directory.Packages.props` in the fix commit). A lock file (`packages.lock.json`) is
deliberately absent — do not commit one from this run.

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

## 6. TypeScript mirror + structural validator

```powershell
Set-Location packages/contracts/wjss-contracts-ts
npm ci
npm run check          # typecheck + node --test + fixture validation
Set-Location ../../..
node tools/boundary-scan/boundary-scan.mjs .   # must print: 0 findings
```

(`npm ci` must hit the Owner's normal registry; if the workstation mirrors npm like it mirrors
NuGet, use the mirror.) Expected: 14/14 tests pass; both fixture sets validate; boundary scan
exits 0.

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
2 restore: PASS/FAIL   notes: ______
  resolved pins: Microsoft.NET.Test.Sdk ___ xunit ___ xunit.runner.visualstudio ___
3 build (Release, sln): PASS/FAIL   warning count: ___ (list below if >0)
4 dotnet test (sln): PASS/FAIL
  Wjss.Domain.Tests ___ / Wjss.Runtime.Core.Tests ___ / Wjss.Runtime.Api.Tests ___
  Wjss.Config.Examples.Tests ___ / Wjss.FixtureEmission.Tests ___
5 fixture regeneration: IDENTICAL / PROVISIONAL-MARKER-ONLY / DRIFT
  (if DRIFT — verbatim diff):
6 npm run check: PASS/FAIL (tests ___/14)   boundary-scan: findings ___
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
