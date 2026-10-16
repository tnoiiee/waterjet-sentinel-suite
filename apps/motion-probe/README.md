# WJSS Motion Library Probe (Owner-invoked harness)

Console-only front-end for the native load / version probe boundary
(`adapters/motion-library/`), part of the Native Load / Version Probe Software Gate. It takes
no arguments, binds no port, opens no socket and writes no file: it resolves the four Owner-local
environment variables, runs the fail-closed probe and prints a redacted JSON outcome, then exits.

Library inspection/loading only. No controller endpoint, discovery, connection, read or motion
command exists here or can be constructed from here. The Motion authorization gate stays CLOSED
and is not touched by this harness.

## Run (Owner workstation only — see `docs/NATIVE_LOAD_VERSION_PROBE_RUNBOOK.md`)

```powershell
$env:WJSS_MOTION_LIB_DIR = "<approved directory from Owner-local evidence>"
$env:WJSS_MOTION_LIB_GCLIB_SHA256 = "<expected 64-hex identity from Owner-local evidence>"
$env:WJSS_MOTION_LIB_GCLIBO_SHA256 = "<expected 64-hex identity from Owner-local evidence>"
$env:WJSS_MOTION_LIB_EXPECTED_VERSION = "<expected version from Owner-local evidence>"

dotnet run --project apps/motion-probe/Wjss.MotionProbe.csproj -c Release
```

Exit codes: `0` probe accepted (all proofs passed, version equals expected); `2` refused
(configuration missing/rejected or any probe proof failed — the canonical refusal code is
printed); `4` invalid invocation (arguments given).

Cleanup afterwards (the values must not linger in a reused shell):

```powershell
Remove-Item Env:WJSS_MOTION_LIB_DIR, Env:WJSS_MOTION_LIB_GCLIB_SHA256, `
            Env:WJSS_MOTION_LIB_GCLIBO_SHA256, Env:WJSS_MOTION_LIB_EXPECTED_VERSION `
            -ErrorAction SilentlyContinue
```

SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA — the Owner-local run is the validation of record.
