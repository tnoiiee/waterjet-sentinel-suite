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
permits it, and one bounded absolute Move target at a time. It displays raw
TP/RP/status/output observations separately from profile-based
interpretation and a codes-only operation log.

There is no raw command box, command file, simultaneous multi-axis operation,
automatic sequence, cleaning path, standby return, engineering-unit
conversion, drive enable/disable write, relative move, output-bank write,
retry, reconnect, replay or discovery. A dispatched Stop is never shown as a
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
