# WJSS Owner-invoked Controller Stop trial harness

This is an isolated console application for the later **no-load WSB1 Controller Stop trial**. It is not part of Runtime, API, Kiosk, Operations Preview, Production composition, or the application Motion gate.

The only accepted command line is one fixed trial mode plus one uppercase axis token:

```text
dotnet run --project apps/motion-stop-trial/Wjss.MotionStopTrial.csproj -- no-load-stop-trial A
dotnet run --project apps/motion-stop-trial/Wjss.MotionStopTrial.csproj -- no-load-stop-trial B
```

`A` maps to the existing typed Horizontal Stop plan; `B` maps to the existing typed Vertical Stop plan. Any extra, missing, differently-cased, raw-command, multi-axis, file, or option input is refused. There is no interactive mode, stdin path, retry switch, dry-run-native mode, or arbitrary command input.

## Authorization and safety boundary

**This software foundation does not authorize or perform a hardware trial by being built or merged. Do not invoke it against a real Controller until a separate post-merge Owner Scope Gate authorizes the exact no-load WSB1 trial.** The application requires all of the following in one invocation before native loading: the exact fixed trial mode and one typed axis, an exact-match Owner-local consent value in `WJSS_MOTION_STOP_TRIAL_CONSENT`, the existing valid Owner-local endpoint, and valid Gate A native-library identity configuration. The consent value is never printed or included in output. The harness removes the consent variable from its own process immediately after reading it; also unset the variable in the invoking shell after use. Never place it in Git, a file, logs, or chat.

Endpoint and native-library values use the already established Owner-local environment boundaries documented by the motion probe and identity runbooks. They are validated before Gate A load/open. Missing or malformed configuration refuses without native loading or Controller contact. Values, paths, hashes, version strings, command text, reply text, and exception details are never reported.

The session enforces direct/no-gcaps mode before one open, resolves only the existing restricted lifecycle exports and the already-approved command export, creates one Stop owner/session, and dispatches at most one selected typed A/B Stop operation. It does not retry, reconnect, replay, queue, read back, confirm, or dispatch another axis. Cleanup invalidates before close and closes before unload. `stopDispatched` is not a stop confirmation, stopped/safe state, or commissioning claim; `stopConfirmed` is always false.

| Exit | Meaning |
| --- | --- |
| `0` | One selected Stop dispatch completed and cleanup is complete; no stop is confirmed. |
| `2` | A precondition/lifecycle refusal occurred, or cleanup could not be proven complete. |
| `4` | The invocation is not exactly the fixed mode plus one uppercase A/B token. |

## Software validation

The vendor-derived contract and command-scope record is
[`docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md`](../../docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md).
The focused `Wjss.MotionLibrary.Tests` suite uses fake kernels and source-boundary checks only. It must not load a vendor DLL or contact a Controller. The Arena sandbox used to author this source had no .NET SDK; no build, restore, or .NET test result is claimed here. Generate the genuine project lock file only with Owner-local `dotnet restore`; do not hand-author it. See [`../../docs/MOTION_STOP_TRIAL_RUNBOOK.md`](../../docs/MOTION_STOP_TRIAL_RUNBOOK.md) for validation and the separate authorization boundary.
