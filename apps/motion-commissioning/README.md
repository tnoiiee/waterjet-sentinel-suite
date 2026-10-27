# WJSS Manual Motion Commissioning application

This is an isolated Owner-local Windows application for the WSB1 manual
commissioning software slice: Axis A (Horizontal), Axis B (Vertical),
Axis C (Horizontal) and Axis D (Vertical).
It is not part of Runtime, API, Kiosk, Operations Preview, Production, an
automatic sequence or the application Motion gate, and no other project
references it.

## Invocation

Exactly one mode argument is accepted:

```text
Wjss.MotionCommissioning.exe simulated
Wjss.MotionCommissioning.exe wsb1-hardware
```

`simulated` is deterministic and in-process. It loads no native library,
opens no socket or Controller session, drives no physical output and is
labelled as simulated in the window. Unless `WJSS_MANUAL_MOTION_PROFILE`
names an Owner-local profile, it uses the built-in synthetic profile. The
simulation profile is refused for a hardware-capable port.

`wsb1-hardware` is only a hardware-capable software composition; building,
testing or merging it does not authorize use against a Controller. Do not
invoke it with real local values until a separate post-merge Owner Scope
Gate authorizes the exact WSB1 commissioning activity. It requires the
one-use exact consent in `WJSS_MANUAL_MOTION_CONSENT`, the established
Owner-local endpoint and Gate A configuration, and a fully qualified
Owner-local profile path in `WJSS_MANUAL_MOTION_PROFILE`. Values are never
committed or displayed; only canonical refusal/outcome codes are shown.

| Exit | Meaning |
| --- | --- |
| `0` | The window ran and closed through the application lifecycle. It is not proof of stopped, safe or commissioned motion. |
| `2` | A hardware-mode precondition was refused before native load/open. |
| `4` | The invocation was not exactly `simulated` or `wsb1-hardware`. |

A refused Owner-local profile is shown in the window and the session never
opens either port. In simulated mode, an explicitly configured refused
profile still prevents opening; it does not silently fall back to synthetic
operation values for control.

## Operator surface and limits

The window is driven only by redacted Application snapshots. It provides
individual brake release/apply commands, hold-to-run Jog +/-, per-axis Stop,
Home, define position zero, define dual zero where the Owner-local profile
permits it, and one bounded absolute Move target per axis. Operations are
independent per axis: at most one operation per axis, and several axes may
hold active operations during the same interval (each panel is enabled from
its own axis state only). It displays raw TP/RP/status/output observations
separately from profile-based interpretation and a codes-only operation log.

There is no raw command box, command file, coordinated simultaneous
multi-axis motion (no synchronized Begin, interpolation or combined-axis
command), automatic sequence, cleaning path, standby return, drive
enable/disable write, relative move, output-bank write, retry, reconnect,
replay or discovery.

## Engineering Motion Configuration

One window, opened from the main window, holds the five per-axis conversion
settings of all four axes side by side: encoder pulses per revolution, drive
pulses per revolution, mechanical gear ratio, pitch in millimetres per output
revolution, and the Move position tolerance in millimetres. Each is
independent per axis and each is optional; an empty field means
`NOT_CONFIGURED` and is captured as absent rather than coerced to a number.

The four derived values (TP normalization ratio, PA command units per
millimetre, TP feedback units per millimetre, RP feedback units per
millimetre) are displayed read-only. There is deliberately no editable
divide-by-two or multiplier field: the theoretical-to-reference relationship
is the axis's own drive-over-encoder ratio, so a control cannot express a
factor the model does not derive.

Apply posts one typed candidate to the Application session, which validates
it and captures it as an immutable snapshot; every later operation is planned
against that snapshot. While anything is in flight the change is refused
(`ENGINEERING_SETTINGS_LOCKED_WHILE_ACTIVE`) and the window says so instead of
queueing or retrying. The derivation, its units and its expected P1/P2 values
are in
[`docs/evidence/H1_MOTION_CONVERSION_DIMENSIONAL_LEDGER.md`](../../docs/evidence/H1_MOTION_CONVERSION_DIMENSIONAL_LEDGER.md). A dispatched Stop is never shown as a
confirmed stop; `StopConfirmed` remains false.

Closing the window requests the Application session's supervised Stop/brake
and close path. If the session does not become terminal within the form's
bounded wait, the final last-resort window close is an application-lifecycle
fallback only; it must never be reported as proof that native cleanup or a
physical stop completed.

## Validation and runbook

The sandbox has no .NET SDK, so this project was not restored, compiled or
tested there and its genuine `packages.lock.json` was not generated. The
Owner-local exact-head restore/build/test sequence, profile shape, simulated
smoke boundary and separate hardware gate are in
[`docs/MANUAL_MOTION_COMMISSIONING_RUNBOOK.md`](../../docs/MANUAL_MOTION_COMMISSIONING_RUNBOOK.md).
Never hand-author the lock file.
