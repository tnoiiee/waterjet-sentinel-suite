# B1 Motion Identity — Owner-Local Validation and First-Contact Runbook

**Gate:** B1 one-controller identity software foundation (software only) and — under a SEPARATE,
explicit Owner authorization — B1-H WSB1 First Contact. **Audience:** the Owner (or a delegate
with the Owner's instruction) on the Windows development workstation. **Not runnable in Arena** —
the Arena sandbox has no .NET SDK, no native library and no access to the Owner machine; every
step below is the validation of record.

**Authority line of this Gate (unchanged).** Software foundation only: one direct connection
attempt, the three typed identity reads (connection type, revision information, serial number),
redacted identity classification, deterministic close and unload. NO raw controller command, NO
axis read, NO motion, NO controller configuration, NO discovery, NO gcaps. The Motion
authorization gate stays CLOSED and candidate values stay `NOT_CONFIGURED` / `NOT_COMMISSIONED`.
Real hardware contact requires the separate B1-H authorization (section B below) — the software
validation runs (section A) are refusal-path only.

**Owner-local values — never committed.** The five Owner-local values (the four probe values of
the Gate A runbook plus the endpoint in `WJSS_MOTION_CONTROLLER_01_ENDPOINT`) are referenced here
by name only. They must stay out of the repository, out of shell-history transcripts pasted
anywhere public, and out of the final report. Record in the evidence template only whether each
proof PASSED — not the values. The serial number observed at B1-H is likewise recorded only as
"observed, Owner-confirmed" — never as a value.

---

## A. B1 software validation (no hardware contact)

### 0. Prerequisites

1. `dotnet --version` satisfies `global.json` (SDK **10.0.401**, `rollForward: latestPatch`).
   If not, STOP and fix the SDK first.
2. Worktree checked out at the PR head of this Gate; `git status --porcelain` empty.
3. Complete the **global-property class sweep** exactly as in
   `STAGE_0.3A_OWNER_LOCAL_VALIDATION.md` §0A (the pre-existing motion toolset has proven it can
   hijack a Release build through environment global properties). Remove any presence
   **process-scope only**; record presence, not values.
4. Prove the workstation motion-tool services stay down and nothing vendor is running (same
   checks as the Gate A runbook §0.4). If a vendor service is not Stopped/Disabled, STOP: this
   Gate does not start, stop or configure any service.

### 1. Restore (genuine — generates the new lock)

The Gate adds one project (`apps/motion-identity`, no new package and no new version pin).
Restore therefore generates only that project's genuine `packages.lock.json`; nothing else may
change:

```powershell
dotnet restore WaterJetSentinelSuite.sln
git status --porcelain
```

Expected: at most one new untracked file — `apps/motion-identity/packages.lock.json` — and NO
other change. The lock is genuine restore output: commit it only after the FULL validation
passes (never hand-edit a lock file; never invent a `contentHash`).

### 2. Locked restore (after the genuine lock exists)

```powershell
dotnet restore WaterJetSentinelSuite.sln --locked-mode
```

Expected: PASS against the committed lock set plus the new genuine lock.

### 3. Full Release build

```powershell
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore
```

Expected: **0 warnings, 0 errors** (TreatWarningsAsErrors).

### 4. Focused tests (the B1 suite)

```powershell
dotnet test tests/motion-library.tests/Wjss.MotionLibrary.Tests.csproj -c Release --no-build
```

Expected: all focused tests pass — probe suite unchanged and green, plus the new endpoint,
session and source-boundary suites (endpoint fail-closed shapes; proof-refusal prevents open;
direct-mode-before-open; single open; fixed read order; response-shape refusals; zero-serial
refusal; `MODEL_CONFIRMATION_REQUIRED`; close-on-every-path; unload truth; busy/stale/budget
fencing; redaction; allow-list pins).

### 5. Full .NET test suite

```powershell
dotnet test WaterJetSentinelSuite.sln -c Release --no-build
```

Expected: all existing tests pass alongside; the Motion Foundation suite is untouched and must
pass unchanged (gate CLOSED, mappings exact, catalogue unchanged).

### 6. Fixture and boundary review

```powershell
git status --porcelain packages/ config/
node tools/boundary-scan/boundary-scan.mjs .
```

Expected: no fixture drift (the Gate adds no contract and no Snapshot/Delta member); boundary
scan prints `0 findings (S1-S9 clean)` and exits 0.

### 7. Refusal-path harness runs only (software validation)

Set the five variables **process-scope only**, then exercise ONLY refusal paths (for example: run
once with the endpoint variable removed → `MOTION_ENDPOINT_NOT_CONFIGURED`, exit 2; run once with
a malformed endpoint shape → `MOTION_ENDPOINT_REJECTED`, exit 2; run with arguments → exit 4).
Do NOT run a success path against a real controller in this section.

```powershell
dotnet run --project apps/motion-identity/Wjss.MotionIdentity.csproj -c Release --no-build
```

### 8. Redaction review and cleanup

Review any pasted output: no endpoint, no serial, no path, no hash. Then remove the five
variables (process-scope), confirm no vendor processes, confirm `git status --porcelain` shows
only the genuine new lock (commit it with the evidence after PASS).

### 9. B1 software evidence template

```text
B1 MOTION IDENTITY — OWNER-LOCAL SOFTWARE VALIDATION
date (local): ______  machine: ______
dotnet --version: ______
git rev-parse HEAD: ______
0 global-property sweep (§0A class): CLEAN / overrides removed (names only): ______
0 vendor services stopped+disabled: PASS/FAIL   vendor processes absent: PASS/FAIL
1 genuine restore: PASS/FAIL   new lock (exactly 1): YES/NO   any other diff: ______
2 locked restore: PASS/FAIL
3 Release build: PASS/FAIL (warnings: ___)
4 focused suite: ___/___
5 full .NET suite: ___ total / ___ passed / 0 failed / 0 skipped
6 fixture drift: NONE/listed   boundary scan: 0 findings / findings
7 refusal-path runs: codes observed ______ (expected refusal codes only)
8 redaction review: PASS/FAIL   cleanup: variables gone YES/NO   worktree clean YES/NO
verdict: PASS / FAIL — steps 0-8 all PASS
```

A PASS verdict authorizes the Owner to merge the B1 software PR and to consider the SEPARATE B1-H
First Contact authorization. The agent never merges.

---

## B. B1-H WSB1 First Contact (SEPARATE explicit Owner authorization required)

Execute only under the B1-H authorization, for **WSB1 / MC-WSB1** only, one controller session at
a time:

1. Standalone NIC state sighting (no changes): confirm the dedicated NIC carries the approved
   address with empty gateway and DNS. This runbook never applies or changes NIC settings.
2. Exclusivity proof: vendor service Stopped/Disabled; no vendor/legacy client processes; PATH
   posture recorded.
3. Set the five variables process-scope only (endpoint value stays Owner-local).
4. Run the probe harness first (`apps/motion-probe`) — Gate A PASS required.
5. Run the identity harness (`apps/motion-identity`) ONCE — one-shot connect / reads / close.
6. Acceptance: exit 0; `IDENTITY_SESSION_COMPLETE` or `MODEL_CONFIRMATION_REQUIRED` (the latter
   is a normal, honest outcome: record the observed revision text for the Owner's model
   confirmation decision); `controllerHandleClosed: true`; `nativeHandlesReleased: true`;
   serial observed and redacted.
7. Owner manual confirmation: the Owner compares the displayed revision text with the expected
   `DMC-B140-M` model and confirms the controller is the intended MC-WSB1 unit by its own
   installation knowledge (the software never claims physical identity).
8. Redaction review of any transcript (no endpoint, no serial) and mandatory cleanup (variables
   removed, no vendor processes, worktree clean).

STOP and request an Owner decision if: any vendor service must be touched; the NIC sighting shows
the standalone state incorrect; the harness refuses with any code not listed above; the cleanup
state is unproven (`CLEANUP_UNPROVEN` / `NATIVE_UNLOAD_FAILED`); a second controller or client
appears; or any step seems to require a raw command, an axis read or motion — none is authorized
at B1-H.

---

## C. Rollback

The Gate installs nothing, writes no file outside `bin/`/`obj/`, and changes no PATH/registry/
service/NIC. Rollback = section A.8 cleanup + closing the PR unmerged. Vendor-side library
removal remains a workstation decision outside this repository.

## D. STOP conditions for this runbook

Stop and request an Owner decision if: any vendor service/tool/process must be touched first; the
endpoint or probe values would need to enter the repository for debugging; the probe or identity
harness refuses with an unexplained code; the software validation would require real contact; or
any step seems to require a raw controller command, an axis read, motion or controller
configuration — none is authorized.
