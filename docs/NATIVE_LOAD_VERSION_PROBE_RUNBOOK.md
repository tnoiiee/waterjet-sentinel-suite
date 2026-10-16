# Native Load / Version Probe — Owner-Local Validation Runbook

**Gate:** Native Load / Version Probe Software Gate. **Audience:** the Owner (or a delegate with
the Owner's instruction) on the Windows development workstation. **Not runnable in Arena** — the
Arena sandbox has no .NET SDK, no native library and no access to the Owner machine; every step
below is the validation of record.

**Authority line of this Gate (unchanged):** library inspection/loading ONLY. This runbook does
NOT authorize a controller endpoint, a NIC change, controller discovery, a controller connection,
a controller read, or any motion command. It does not open GDK, GalilTools, Galil Suite, gcaps or
`GalilService`; it does not invoke any open/close/discovery/command/callback/status API. The only
native export invoked is the library-local version query of the exact installed header
declaration, through the approved modules in the approved directory.

**Owner-local values — never committed.** The four probe values (approved native directory, both
expected SHA-256 identities, expected library version) are the values recorded in this Gate's
Owner evidence (Section 2 of the Gate instruction). They are referenced here by name only. They
must stay out of the repository, out of shell history transcripts that get pasted anywhere
public, and out of the final report. Record in the evidence template only whether each proof
PASSED — not the values.

---

## 0. Prerequisites

1. `dotnet --version` must satisfy the repository pin (`global.json`: SDK **10.0.401**,
   `rollForward: latestPatch`). If not, STOP and fix the SDK first.
2. Worktree checked out at the PR head of this Gate; `git status --porcelain` empty.
3. Complete the **global-property class sweep** exactly as in
   [`STAGE_0.3A_OWNER_LOCAL_VALIDATION.md`](STAGE_0.3A_OWNER_LOCAL_VALIDATION.md) §0A — the
   pre-existing motion toolset on this workstation has already proven it can hijack a Release
   build through environment global properties (`TargetPath` class; see that document and the
   CHANGELOG entry of 2026-10-15):

   ```powershell
   $names = 'TargetPath','TargetDir','TargetFileName','OutDir','OutputPath',
            'ReferencePath','AssemblySearchPaths','AdditionalLibPaths',
            'AssemblyFoldersEx','AssemblyFoldersSuffix'
   foreach ($n in $names) { if (Test-Path "Env:$n") { "OVERRIDE: $n = " + (Get-Item "Env:$n").Value } }
   ```

   Remove any presence **process-scope only** (`Remove-Item Env:<name>` in this shell). Never
   modify the User/Machine-level variables of the external toolset. Record the presence in the
   evidence template; do NOT record the values unless the Owner elects to.
4. Prove the workstation motion-tool services stay down and nothing vendor is running:

   ```powershell
   Get-Service GalilService | Format-List Status, StartType   # expected: Stopped, Disabled
   Get-Process | Where-Object { $_.Name -match 'gclib|gcaps|gdk|galil' }
   # expected: no rows. If anything matches by name, STOP and identify it with the Owner.
   ```

   If `GalilService` is not Stopped/Disabled, STOP: this Gate does not start, stop or configure
   any service; that needs an explicit Owner decision outside this runbook.
5. PATH isolation sanity (evidence, not a change): the probe never consults PATH, but record the
   posture that protects the result from ambiguity:

   ```powershell
   ($env:Path -split ';') | Where-Object { $_ -match 'gclib|gcaps|gdk|galil' }
   # expected: no rows (per the Owner evidence: PATH carries no such directory)
   ```

## 1. Restore (genuine — generates the new locks)

The Gate adds three projects with **no new package and no new version pin** (the adapter and the
harness reference nothing but the framework; the test project uses only the already-pinned test
stack). Restore therefore generates ONLY the three genuine lock files of the new projects;

nothing else may change:

```powershell
dotnet restore WaterJetSentinelSuite.sln
git status --porcelain
```

Expected: exactly three new untracked files —
`adapters/motion-library/packages.lock.json`, `apps/motion-probe/packages.lock.json`,
`tests/motion-library.tests/packages.lock.json` — and NO other change. If anything else appears,
STOP and report it. The locks are genuine restore output: commit them only after the FULL
validation passes, exactly per the repository's round-3 lock policy (never hand-edit a lock
file; never invent a `contentHash`).

## 2. Locked restore (after the genuine locks exist)

```powershell
dotnet restore WaterJetSentinelSuite.sln --locked-mode
```

Expected: PASS against the committed lock set plus the three new genuine locks.

## 3. Full Release build

```powershell
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore
```

Expected: **0 warnings, 0 errors** (TreatWarningsAsErrors). If the `MSB3245` / `CS0246`
reference-resolution signature appears, this is the §0A environment class again — return to
step 0/3; never edit the Product tree to compensate.

## 4. Focused tests (the new Gate suite)

```powershell
dotnet test tests/motion-library.tests/Wjss.MotionLibrary.Tests.csproj -c Release --no-build
```

Expected: all focused tests pass. The suite exercises the entire defect class WITHOUT the native
library: resolver (missing/rejected/normalization/containment), hash/PE proofs, and the full
orchestration (missing file, identity mismatch, non-x64, load failure, substituted module path,
missing export, dead buffer, version mismatch, unload failure, deterministic cleanup,
call order, single-export discipline) plus the source-boundary pins.

## 5. Full .NET test suite

```powershell
dotnet test WaterJetSentinelSuite.sln -c Release --no-build
```

Expected: all existing tests still pass alongside the new suite. The Motion Foundation suite
(`Wjss.Runtime.Core.Tests`) is untouched and must pass unchanged: gate CLOSED, mappings exact,
catalogue unchanged.

## 6. Fixture and lock drift review

```powershell
git status --porcelain packages/ config/
node tools/boundary-scan/boundary-scan.mjs .
```

Expected: no fixture drift (the Gate adds no contract and no Snapshot/Delta member); boundary
scan prints `0 findings (S1-S9 clean)` and exits 0. The S7 solution-membership and S8 XML checks
now include the three new projects by construction.

## 7. Probe configuration (Owner-local values; shell-scoped only)

Set the four variables with the values from this Gate's Owner evidence — **process scope only**,
never User/Machine scope (the toolset pollution lesson), never written to a file:

```powershell
$env:WJSS_MOTION_LIB_DIR = "<approved native directory — Owner evidence only>"
$env:WJSS_MOTION_LIB_GCLIB_SHA256 = "<expected 64-hex identity of gclib.dll — Owner evidence only>"
$env:WJSS_MOTION_LIB_GCLIBO_SHA256 = "<expected 64-hex identity of gclibo.dll — Owner evidence only>"
$env:WJSS_MOTION_LIB_EXPECTED_VERSION = "<expected library version — Owner evidence only>"
```

Self-checks before running (optional but recommended):

```powershell
Test-Path $env:WJSS_MOTION_LIB_DIR                                    # True
Get-FileHash (Join-Path $env:WJSS_MOTION_LIB_DIR 'gclib.dll') -Algorithm SHA256
Get-FileHash (Join-Path $env:WJSS_MOTION_LIB_DIR 'gclibo.dll') -Algorithm SHA256
# Human-compare the two outputs with the Owner-evidence values. The probe repeats this proof
# itself; these commands are the Owner sighting, not a substitute for it.
```

## 8. Run the probe

```powershell
dotnet run --project apps/motion-probe/Wjss.MotionProbe.csproj -c Release --no-build
```

Expected (the only acceptance):

- exit code **0**;
- JSON on stdout with `"accepted": true`, `"code": "NATIVE_LIBRARY_PROBE_COMPLETE"`,
  `"nativeHandlesReleased": true`;
- `"libraryVersion"` printed **exactly equal** to the Owner-configured expected version
  (item 11 of the validation list: record PASS only if exact, character for character);
- the probe internally proved the loaded module paths equal the approved files before this
  result (the acceptance code exists only after that proof; if it were unequal the outcome
  would be `NATIVE_MODULE_PATH_MISMATCH` with exit code 2).

Refusal table (exit code 2 — the code names the failed proof; every refusal is fail-closed and
nothing was loaded after the failing step):

| Code | Meaning |
| --- | --- |
| `NATIVE_LIB_DIR_NOT_CONFIGURED` | a required variable is absent/blank (detail names WHICH variable) |
| `NATIVE_LIB_DIR_REJECTED` | a present value rejected by shape rules (no UNC/wildcard/relative/malformed hash) |
| `NATIVE_FILE_NOT_FOUND` | an approved module file is absent from the approved directory |
| `NATIVE_FILE_READ_FAILED` | a file could not be read, so identity/architecture could not be verified |
| `NATIVE_FILE_IDENTITY_MISMATCH` | SHA-256 of a module does not equal its expected identity |
| `NATIVE_ARCHITECTURE_MISMATCH` | a module is not a provable x64 PE image |
| `NATIVE_LOAD_FAILED` | the approved module could not be loaded with the restricted search set |
| `NATIVE_MODULE_PATH_MISMATCH` | the loaded module path is not provably the approved file |
| `NATIVE_EXPORT_NOT_FOUND` | `gclib_version` absent from the loaded approved module |
| `NATIVE_VERSION_CALL_FAILED` | no terminated, printable version string within the bounded buffer |
| `NATIVE_VERSION_MISMATCH` | returned version does not equal the expected version |
| `NATIVE_UNLOAD_FAILED` | a native handle could not be released deterministically |
| `NATIVE_PROBE_UNSUPPORTED_PLATFORM` | not a 64-bit Windows process; nothing was loaded |

Any refusal: STOP, record the code, and report. Never "fix" by editing values blindly; re-read
the Owner evidence first.

## 9. Boundary proofs to attach to the evidence

1. **No endpoint / no network contact:** this Gate introduces NO endpoint variable and the probe
   performs no network operation. Optional transcript: `Get-NetTCPConnection` snapshots before
   and after the probe run show no new external connection from the probe process; the code
   contains no socket API at all (reviewable in the diff).
2. **No committed native material or secrets:**

   ```powershell
   git status --porcelain
   Select-String -Path adapters/motion-library/*, apps/motion-probe/* -Pattern ':[\\/]' -SimpleMatch:$false
   # plus the source-boundary tests from step 4 which machine-check the same prohibition.
   ```

3. **Redaction:** probe JSON, console output and any pasted transcript contain no directory, no
   hash and no expected-version value. Review the pasted evidence before attaching it.
4. **Motion gate unchanged:** the static catalogue route still serves the closed-gate catalogue;
   candidate values remain `NOT_CONFIGURED` / `NOT_COMMISSIONED`; nothing motion-executable
   exists.

## 10. Cleanup (mandatory)

```powershell
Remove-Item Env:WJSS_MOTION_LIB_DIR, Env:WJSS_MOTION_LIB_GCLIB_SHA256, `
            Env:WJSS_MOTION_LIB_GCLIBO_SHA256, Env:WJSS_MOTION_LIB_EXPECTED_VERSION `
            -ErrorAction SilentlyContinue
Get-ChildItem Env:WJSS_MOTION_LIB_* -ErrorAction SilentlyContinue   # expected: no rows
Get-Process | Where-Object { $_.Name -match 'gclib|gcaps|gdk|galil' } # expected: no rows
git status --porcelain
```

Expected: variables gone, no vendor process, worktree shows only the three genuine new lock
files (commit them with the evidence after the PASS), nothing else.

## 11. Rollback

The Gate leaves no machine residue of its own: it installed nothing, wrote no file outside
`bin/`/`obj/`, changed no PATH/registry/service and started nothing. Rollback = step 10 cleanup
plus closing the PR unmerged; the vendor installer and its directory remain the Owner-installed
state from before this Gate. To also remove the library from the machine, use the vendor
installer/uninstaller outside this repository — that is a workstation decision, not a Product
change.

## 12. Evidence template — paste into the PR comment

```text
NATIVE LOAD / VERSION PROBE — OWNER-LOCAL VALIDATION
date (local): ______  machine: ______
dotnet --version: ______
git rev-parse HEAD: ______
0 global-property sweep (§0A class): CLEAN / overrides removed process-scope (names only): ______
0 GalilService Stopped+Disabled: PASS/FAIL   vendor processes absent: PASS/FAIL (or reported)
0 PATH rows matching tool folders: none / listed: ______
1 genuine restore: PASS/FAIL   new locks (exactly 3): YES/NO   any other diff: ______
2 locked restore: PASS/FAIL
3 Release build: PASS/FAIL (warnings: ___)
4 focused gate suite: ___/___
5 full .NET suite: ___ total / ___ passed / 0 failed / 0 skipped
6 fixture drift: NONE/listed   boundary scan: 0 findings / findings
7 four variables set process-scope: YES/NO   file sighting vs evidence: MATCH/MISMATCH
8 probe exit code: ___   code: ______   nativeHandlesReleased: true/false
  returned version EXACTLY equals expected version: YES/NO (values not recorded here)
  loaded module paths equal approved files (internal proof, acceptance requires it): YES n/a
9 no new TCP connection from probe: YES/NO   redaction review of pasted evidence: PASS/FAIL
10 cleanup: variables gone YES/NO   vendor processes none YES/NO   worktree clean YES/NO
verdict: PASS / FAIL — steps 0-10 all PASS and 8 reports the exact expected version
```

A PASS verdict authorizes the Owner to merge this Gate's PR and to consider the NEXT separate
Gate (the single-controller read-only connection Gate, still NOT AUTHORIZED). A FAIL keeps the
PR open. The agent never merges.

## 13. STOP conditions for this runbook

Stop and request an Owner decision if: any vendor service/tool/process must be touched first;
PATH shows a tool folder that could shadow the approved modules; the hashes or versions sighted
on the machine differ from the Owner evidence; the probe refuses with any code; the evidence
values would need to enter the repository for debugging; or any step seems to require controller
contact, an endpoint, a NIC change or a motion command — none is authorized.
