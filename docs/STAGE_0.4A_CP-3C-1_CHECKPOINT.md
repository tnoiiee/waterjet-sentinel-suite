# Stage 0.4A CP-3c-1 — atomic publication foundation

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
