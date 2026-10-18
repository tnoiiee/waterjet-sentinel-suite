# Owner-invoked C1 Stop-only trial foundation

**Status:** software-only source foundation for a possible later no-load WSB1 Controller Stop trial.
**This runbook is not hardware authorization. No Controller contact or real Stop dispatch was
performed while authoring this change.** A separate post-merge Owner Scope Gate is required before
any real Controller invocation.

The C1 Stop-only foundation is distinct from the B2-to-Application C1 read contract, which remains
not implemented. The vendor-derived API boundary and the current C1 scope distinction are recorded
in [`docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md`](evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md).
The earlier internal C1 Stop plan/owner was merged in PR #32; this change adds the isolated,
Owner-invoked console composition.

## Closed invocation

The harness is `apps/motion-stop-trial/Wjss.MotionStopTrial.csproj`. Its only accepted invocation
shape is exactly one fixed mode plus exactly one uppercase typed axis token:

```text
dotnet run --project apps/motion-stop-trial/Wjss.MotionStopTrial.csproj -- no-load-stop-trial A
dotnet run --project apps/motion-stop-trial/Wjss.MotionStopTrial.csproj -- no-load-stop-trial B
```

- `A` maps only to the existing typed Horizontal Stop plan.
- `B` maps only to the existing typed Vertical Stop plan.
- Extra or missing arguments, different case, another axis, combined/multi-axis syntax, command
  text, options, file paths, stdin and interactive input are refused. There is no dry-run-native,
  retry, reconnect, replay, queue, fallback or automatic rerun mode.
- The two command templates remain centralized in `MotionStopCommandPlan.cs`; the harness passes
  only the typed enum to the existing C1 Stop owner.

## Required preconditions

The harness requires all four gates before any native load/open/dispatch:

1. the exact fixed mode and one A/B selection;
2. an exact-match Owner-local value in `WJSS_MOTION_STOP_TRIAL_CONSENT`;
3. the existing canonical Owner-local endpoint in `WJSS_MOTION_CONTROLLER_01_ENDPOINT`;
4. all existing Gate A values, validated by `MotionLibraryConfigurationResolver`:
   `WJSS_MOTION_LIB_DIR`, `WJSS_MOTION_LIB_GCLIB_SHA256`,
   `WJSS_MOTION_LIB_GCLIBO_SHA256`, and `WJSS_MOTION_LIB_EXPECTED_VERSION`.

The consent value is an explicit local confirmation, not a credential or reusable authorization
service. It is not printed, logged, committed or stored by the harness. The harness removes it from
its own process immediately after reading it; the Owner must also unset it in the invoking shell
after the process exits. Do not put endpoint/native values or consent in Git, files, logs or chat.
Missing or malformed configuration refuses before Gate A native load or Controller open; there is
no fallback or default.

## Fixed lifecycle and report

After all preconditions pass, the isolated session:

1. runs Gate A first (approved module identity, x64 image, loaded-path and version proof; only the
   Gate A version export);
2. loads only the two approved modules through the restricted loader and proves each loaded path;
3. resolves only direct-mode, open, close and the existing command export; it does not resolve B1
   identity-read or B2 observation exports;
4. selects direct/no-gcaps mode before its one open with the validated address only;
5. creates one Stop owner/session and dispatches at most one selected typed A/B Stop;
6. invalidates the Stop boundary before closing the handle, then unloads the native modules.

The JSON result contains only canonical codes, the selected A/B label, truthful lifecycle flags and
exit status. It never includes endpoint, native path/hash/version, consent, command/reply text,
vendor error detail or exception detail. `stopDispatched` means only that the single C1 dispatch
completed without a classified refusal. `stopConfirmed` is permanently false; dispatch is not a
confirmed stop, stopped/safe state or commissioned result. No confirmation readback occurs.

| Exit | Meaning |
| --- | --- |
| `0` | One selected Stop dispatch completed and cleanup is complete; no stop is confirmed. |
| `2` | A precondition/lifecycle refusal occurred, or cleanup could not be proven complete. |
| `4` | The invocation is not exactly the fixed mode plus one uppercase A/B token. |

## Owner-local software validation (fake/source tests only)

Run this validation on the exact PR head **without setting the trial consent or real endpoint/native
values**. It restores/builds/tests software; it does not invoke the trial executable.

```powershell
git rev-parse HEAD
git status --porcelain

dotnet restore WaterJetSentinelSuite.sln

git status --porcelain

dotnet build WaterJetSentinelSuite.sln -c Release --no-restore

dotnet test tests/motion-library.tests/Wjss.MotionLibrary.Tests.csproj -c Release --no-build
dotnet test WaterJetSentinelSuite.sln -c Release --no-build

node tools/boundary-scan/boundary-scan.mjs .
git diff --check
```

`Directory.Build.props` enables lock-file generation. The genuine
`apps/motion-stop-trial/packages.lock.json` must be produced only by Owner-local `dotnet restore`;
never hand-author it. Confirm that no existing lock changed unexpectedly. Stop if restore creates
unexplained lock drift, any fake/source test calls a real native boundary, or a required result
cannot be established. The Arena authoring sandbox has no .NET SDK; no restore/build/test result is
claimed here.

The focused `Wjss.MotionLibrary.Tests` coverage uses fake Gate A results, fake controller kernels,
fake Stop-dispatch kernels and source-boundary reads only. It does not load native modules, open a
Controller, probe a network, or issue a real command.

## Separate hardware authorization remains outstanding

The software foundation does **not** authorize the no-load trial. After this PR is merged, the
Owner's next action is to validate the exact PR head locally and make a review/merge decision. Only
after merge may a **separate Scope Gate** authorize the actual no-load WSB1 trial. Until that
separate decision exists, do not set the local consent or invoke this application against a real
Controller.
