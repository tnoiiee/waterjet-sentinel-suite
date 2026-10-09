# Stage 0.4A CP-3c-1 — atomic publication foundation

**Latest status (2026-10-09): OWNER-LOCALLY VALIDATED at `29f133ce6598bc2eb489273a2f2d40f8827deb5f`; Final Source Review PASSED, no blocking defect; PR #10 OPEN, NOT MERGED. The original development/validation-pending notes below are historical and superseded by the closeout section at the end.**

Scope: an in-memory, single-writer publication aggregate of RuntimeState and its bounded contiguous Delta chain. This is a library foundation only. Existing Host state and Delta stores remain in use until a separately authorized CP-3c-2 migration; no Host, API, Inspector, scenario or device changes are authorized here.

Development evidence: source authored and statically reviewed; not compiled or executed in Arena. Owner-local validation required before acceptance. No claim of Host atomicity is made while the Host still uses its compatibility stores.

## Implementation inventory

`RuntimePublication` contains one frozen current state, newest-first read-only bounded Delta list, capacity, and generation. `RuntimePublicationStore` owns one volatile publication reference and issues exactly one `RuntimePublicationWriter`; `RuntimePublicationResult` returns an accepted/refused outcome and the resulting generation. The writer checks the expected revision, state invariants, Delta linkage, projected whole sections including previous Sensor records, strict replay, and retained-chain continuity before publishing one reference. An exception before that write changes no generation. The Host still owns legacy `RuntimeStateStore` and `RuntimeDeltaHistory`: they are compatibility types, not additional writers of the new aggregate. CP-3c-2 must replace both Host reads and its writer together; no parallel authoritative source is permitted.

The candidate projector now accepts a full trend window shifted left exactly once with one appended point; the Delta carries only that point. Below capacity the existing append-only rule remains. A changed window with neither an append nor an exact shift is refused. Identical-valued repeated points have no distinguishable identity beyond their contract values; a byte-identical window remains an unchanged window.

## Development and validation

SOURCE AUTHORED. STATICALLY REVIEWED. NOT COMPILED IN ARENA. NOT EXECUTED IN ARENA. OWNER-LOCAL VALIDATION REQUIRED. The tests are source-authored, not evidence of a passing run. No Host, API, Inspector, contract, fixture, TypeScript or device source was modified. Full-window trend behavior, including a one-slot window and repeated identical point values, needs explicit Owner-local review. The Host still has its legacy post-commit Delta gap path until CP-3c-2; do not describe Host publication as atomic in CP-3c-1.

Suggested Owner-local PowerShell from repository root:

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore -p:TreatWarningsAsErrors=true
dotnet test tests/runtime.tests/Wjss.Runtime.Core.Tests.csproj -c Release --no-restore
dotnet test -c Release --no-restore
dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release --no-restore
Push-Location packages/contracts/wjss-contracts-ts
npm ci
npm run typecheck
npm test
Pop-Location
# Run documented S1-S9 boundary scan after checking its current command.
git diff --check
git status --short
```

## Owner-local validation and Final Source Review closeout (2026-10-09)

Owner-reported validation at code head `29f133ce6598bc2eb489273a2f2d40f8827deb5f` (.NET SDK 10.0.401; not run in Arena): locked restore PASS, lock drift NONE; Release build PASS, 0 warnings, 0 errors; RuntimePublicationTests **20/20**, Runtime.Core **450/450**, full .NET **572/572**, fixture parity **7/7**, TypeScript typecheck PASS and tests **31/31** (0 failed); boundary scan **0 findings, S1–S9 clean**; `git diff --check` PASS; final Owner-local working tree CLEAN. Failed 0, skipped 0 in each reported .NET suite; no Owner-local artifact commit required.

Final Source Review (static, exact validated code head): **PASS, no blocking defect**. CP-3c-1 delivers the library-only atomic State/Delta publication aggregate with one writer, bounded contiguous history and detached reader/result snapshots. The full-window trend failure was fixed by checking equal-count *unchanged content* before testing the exact oldest-eviction-plus-append shift. The internal-Delta JSON fingerprint failure was a test defect: fingerprints now serialize `ProjectWire(delta)` and previous Sensors, never raw `RuntimeDelta`. The reader-facing mutable `TrendPoint.Series` alias was corrected with a defensive-copy reader boundary and nested typed freeze, without changing the contract. Analyzer corrections CA1861 and xUnit1031 are closed at the validated code head. No Host integration, Runtime API integration, Inspector change, contract, fixture or TypeScript change is included. CP-3c-2 and CP-4 are NOT AUTHORIZED; TEST_HARDWARE and PRODUCTION device access are NOT AUTHORIZED. PR #10 remains OPEN, NOT MERGED, for Owner merge review.

Non-blocking defense-in-depth: null candidate/Delta and fabricated internal history-gap branches are publicly unreachable through the typed single-writer path; no reflection seam or additional tests requested. Source review and documentation were performed in Arena; restore, build, tests and boundary scan were **not** executed there. The existing Host continues to use compatibility stores and does not inherit the new aggregate’s atomic guarantee until separately authorized integration.
