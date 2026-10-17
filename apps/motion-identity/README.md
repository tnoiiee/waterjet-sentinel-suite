# WJSS Motion Identity Harness — B1 One-Controller First-Contact Foundation

**Gate:** B1 one-controller identity software foundation (software only). **Validation:**
SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA — the Owner-local build and run are the
validation of record (see `docs/MOTION_IDENTITY_FIRST_CONTACT_RUNBOOK.md`).

## What this harness is

An Owner-invoked, console-only front-end for the B1 identity surface of
`Wjss.Adapters.MotionLibrary`. It takes no arguments, binds no port, opens no listener, writes no
file, starts no Runtime and shows no UI. It resolves the Owner-local probe values plus the
Owner-local endpoint variable `WJSS_MOTION_CONTROLLER_01_ENDPOINT`, then runs the one-shot
identity session:

1. Gate A native identity/version proof (the complete probe, unchanged);
2. injected precondition posture;
3. direct/no-gcaps posture enforced before any open;
4. exactly one controller connection attempt whose `gclib_open()` address is the validated
   canonical endpoint only (gclib 2.6.0 manual, `gclib_open` entry, printed page 48); direct/no-
   gcaps is not an address option — it is the process-wide `gclib_force_gcaps(false)` call that
   precedes the open;
5. exactly three typed identity reads — connection type, revision information, serial number;
6. redacted identity classification (model token match or `MODEL_CONFIRMATION_REQUIRED`);
7. deterministic controller close and native-handle release on every path.

## Output contract

- Exit **0** — accepted (`IDENTITY_SESSION_COMPLETE`): every step passed. Physical identity is
  still Owner-confirmed at the separately authorized B1-H First Contact Gate.
- Exit **2** — refused: the canonical refusal code is printed (endpoint missing/rejected, proof
  refused, direct mode not enforced, concurrent authority detected, a classified open refusal,
  `CONNECTION_HANDLE_INVALID`, connection-type mismatch, revision/serial response invalid,
  model confirmation required, stale generation, operation busy, lifecycle budget exceeded,
  close failed/cleanup unproven, native unload failed). A classified open refusal is exactly one
  of `OPEN_INVALID_ARGUMENT`, `OPEN_INTERNAL_ERROR`, `CONNECTION_TIMEOUT`, `OPEN_COMMAND_ERROR`,
  `OPEN_BUFFER_TOO_SMALL`, `OPEN_NOT_CONNECTED`, `OPEN_NOT_SUBSCRIBED` or
  `OPEN_RESULT_UNRECOGNISED`; the raw library value and any vendor error text are never printed.
  Nothing is defaulted, corrected or retried.
- Exit **4** — invalid invocation (any argument).

The JSON outcome is redacted: it never contains the endpoint, the serial number, a native path, a
hash or raw vendor error text. The serial carries only `SERIAL_OBSERVED_REDACTED` +
`OWNER_MANUAL_CONFIRMATION_REQUIRED`.

## What this harness is NOT

- No raw controller command of any kind; no axis read; no motion; no controller configuration.
- No discovery, no IP assignment, no gcaps, no vendor tool.
- No polling, no retry, no reconnect, no background service.
- No Runtime, Snapshot/Delta, Kiosk or Preview coupling.
- No real hardware contact during software validation: Agent-side validation uses fake kernels
  only, and Owner-local software runs are refusal-path only until the separate B1-H gate
  authorizes first contact.
