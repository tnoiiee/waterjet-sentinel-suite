# `Wjss.MotionObservation` — B2-H one-shot read-only observation harness

Owner-invoked console harness for the B2-H one-shot read-only observation trial. It is the
smallest executable composition over the already committed B2 read boundary: **one** controller
handle, **one** observation session, **one** deterministic pass over the fixed sixteen typed read
intents, then boundary invalidation, close, unload and exit.

This project is not a service, a UI, a route or a Runtime component. It takes no arguments, binds
no port, writes no file, keeps no state and starts nothing.

## Invocation

```powershell
dotnet run --project apps/motion-observation/Wjss.MotionObservation.csproj -c Release
```

Configuration comes only from the Owner-local environment variables named in
[`docs/MOTION_OBSERVATION_TRIAL_RUNBOOK.md`](../../docs/MOTION_OBSERVATION_TRIAL_RUNBOOK.md).
Never pass an endpoint, an address, a command or a subject on the command line — any argument is
refused with exit code `4` before anything is read.

| Exit | Meaning |
| --- | --- |
| `0` | Trial accepted: proof passed, one open succeeded, sixteen reads accepted in plan order, boundary invalidated, handle closed, modules released |
| `2` | Refused: configuration, proof, direct mode, open, read boundary, a typed read, or cleanup (canonical code on standard error) |
| `4` | Invalid invocation |

Output is one redacted JSON object. It never contains an endpoint, a serial number, a native path,
a file hash, request text or vendor error text.

## Authority

There is no write, motion, jog, brake, home, position-define, auto-clean, program-array or
output-mutation operation in this project, and none can be formed from it. A completed trial is
not an authorization: the Motion gate stays CLOSED, the endpoint stays `NOT_CONFIGURED` and the
axes stay `NOT_COMMISSIONED`.

## Vendor contract

The library contract behind this harness — the three-layer separation, the verified entries, the
open-address misuse rule, the result order and the command registry — is recorded in
[`docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md`](../../docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md).
That document is evidence only: it authorizes nothing, and a completed trial opens no gate.

## Build note

`Directory.Build.props` sets `RestorePackagesWithLockFile=true`, so the first Owner-local restore
generated `apps/motion-observation/packages.lock.json`. That genuine generated file was committed by
the Owner (commit `2d2ebe41`) and is never hand-authored; the Agent never regenerated, rewrote or
reformatted it, and no other lock file changed. Subsequent restores use `--locked-mode`.

SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA. The Owner-local build and run are the validation
of record.
