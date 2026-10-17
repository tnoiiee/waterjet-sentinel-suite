# Legacy Motion Commissioning Baseline (Redacted)

**Document class:** EVIDENCE ONLY. This is the canonical, redacted baseline of the Legacy motion
commissioning behaviour, committed so that a later read or motion contract can be reviewed against
what the Legacy application actually did. It takes no decision and authorizes no work.

**Authority.** `docs/MASTER_PLAN.md` remains the roadmap of record and is not competed with here;
`docs/MOTION_COMMISSIONING_FOUNDATION.md` remains the commissioning authority;
`docs/SAFETY_BOUNDARY.md` remains the safety boundary. This document introduces no stage, no gate
and no roadmap item, and it does not promote any Legacy value into Product configuration.

**Checkpoint.** PR #31 — B2 read-only observed-state foundation.

**Gate position.** The Motion authorization gate is CLOSED
(`MOTION_COMMISSIONING_GATE_NOT_OPEN`). Controller endpoint configuration is `NOT_CONFIGURED`;
the axes are `NOT_COMMISSIONED`. Planned is not authorized. This baseline does not change either.

**Hardware position.** No controller was contacted, read, configured or commanded while producing
this document. No endpoint, address, coordinate, engineering constant or response value from the
Legacy application is reproduced here. The four Legacy controller endpoints are redacted, every
coordinate and standby value is redacted, and all numeric commissioning constants are recorded by
name only.

**Handling.** The pack was treated as read-only external evidence: it was retrieved once through
the connected Google Drive connector, held outside the repository for analysis, and is not
committed. No Legacy source line is copied into this repository; every statement below is a
paraphrase of behaviour.

---

## A. Provenance and pack integrity

### A.1 Pack identity

| Item | Value |
| --- | --- |
| Exact filename | `repomix-output.md` |
| Retrieval | Google Drive connector, read-only, one exact-name match |
| Size | 662,922 bytes |
| Lines | 10,834 |
| File headers | 7 (`## File:` entries) |
| Code-fence lines | 16 |
| Truncation markers | none |
| Final line | closing fence after the last file's closing brace |
| Other Drive files accessed | none |
| Committed to this repository | no |
| Temporary local copy | created outside the repository, read-only, deleted after extraction |

The pack is a merged representation of the seven Legacy source files below. It is not a Git tree,
not a build output and not a runtime capture: every conclusion in this document is a source-level
reading, not a measurement of the machine.

### A.2 Packed path inventory

| # | Path | Lines | Role in the baseline |
| --- | --- | --- | --- |
| 1 | `FormCannonController.cs` | 1,988 | Operator surface, axis action dispatch, auto-move timer, cleaning-path persistence, motion configuration |
| 2 | `FormCannonController.Designer.cs` | 3,064 | Control construction and event wiring only |
| 3 | `FormCannonController.resx` | 1,324 | Localised strings and images only |
| 4 | `FormDataCenter.cs` | 3,512 | Provider implementation: connection, command dispatch, feedback loop, status decode, brake and motion helpers |
| 5 | `FormDataCenter.Designer.cs` | 282 | Control construction and event wiring only |
| 6 | `FormDataCenter.resx` | 430 | Localised strings only |
| 7 | `IDataProvider.cs` | 160 | The provider contract the controller codes against |

### A.3 Completeness checks performed

- every `## File:` header resolves to exactly one fenced block;
- every `.cs` file ends with a closing brace (no truncated method body);
- every method relevant to connection, command dispatch, feedback polling, brake handling, Stop,
  Jog, Home, position definition, explicit Move and auto-motion was read end to end;
- the Designer and resource files were scanned for event wiring, control names and default values
  only, because they contain no motion logic;
- extraction produced seven files with the line counts above.

### A.4 What this pack can and cannot prove

It **can** prove: which command shapes the Legacy application constructs; from which method, in
which order, under which trigger; how failures and cleanup were handled; what the concurrency and
state model were; and which defaults were persisted.

It **cannot** prove: whether any command was accepted by the controller; the exact response text
of the installed firmware; the electrical polarity of any input or output; the meaning of any
status bit on the installed hardware; any measured position, speed, acceleration or timing; or any
physical outcome. Those are `NOT VERIFIED` and remain so (section G).

---

## B. Command inventory

### B.0 Legend and disposition vocabulary

Every command below is one *distinct Legacy command shape*. "Site" is the file plus method in the
pack. "Origin" is what triggers it: an Operator control, the feedback timer, the connect path or
the auto-move timer. "Unit" is the native unit of the operand, never a converted engineering
value. Ordering, failure, cleanup and concurrency describe the call site, not a recommendation.

Dispositions:

- **BLOCKED** — a read that has no authorized surface at this checkpoint. The vendor command is
  real and documented, but issuing it needs a raw command-and-response path that the B1 gate
  explicitly does not authorize ("no raw controller command, no axis read"). Not implemented.
- **EXCLUDED** — deliberately outside every current and planned WJSS surface: either an
  unconditional multi-output write, or dead code in the Legacy source.
- **LATER** — a motion, brake or position-definition write that belongs to a future
  Owner-authorized motion stage. Not implemented, not prepared, not scheduled.

No command in this inventory is implemented by PR #31. The four B2 read candidates are recorded
separately in section B.4.

### B.1 Shared dispatch path

Most command shapes below reach the controller through one method,
`FormDataCenter.SendGalilCommandAsync(command, terminator, responsePrefix, returnResponse)`:

1. it first calls `IsGalilActuallyConnected()`, which issues a separate liveness read and returns
   `false` on any exception — a failed read is indistinguishable from a disconnected controller;
2. if that returns `false`, it throws `InvalidOperationException` and the command is never sent;
3. otherwise it wraps the vendor COM call in a background task and awaits it, with no
   cancellation token and no timeout of its own;
4. any exception is re-wrapped as a new exception whose message embeds the **full command text**
   and the vendor error text, and that message is surfaced to the Operator.

The two position reads in the feedback loop are the exception: they call the vendor object
directly inside a background task, bypassing this method and therefore bypassing the liveness
probe and the shared error wrapping.

There is no request queue, no correlation identifier, no single-outstanding rule and no
generation fence on this path. Two callers can dispatch concurrently, and replies are matched only
by "whatever the awaited call returns".

### B.2 Read commands

#### B2-OBS-1 — `MG _TS{axis}`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `ProcessAxisStatusAndPositionAsync` |
| Caller | the feedback loop (`GetGalilDataAsync`), per axis |
| Origin | feedback timer, not an Operator control |
| Native unit | axis status word, native integer; the fractional part is discarded |
| Ordering | first of three per-axis reads; then commanded position, then actual position |
| Failure | any exception reports one generic error and stops the whole feedback loop |
| Cleanup | none |
| Concurrency | per axis sequential, axes sequential; the next timer tick can overlap the current pass |
| Vendor verification | the vendor command reference documents the message command and the status-word operand; the **bit order and polarity for the installed controller family are NOT confirmed** |
| Disposition | **BLOCKED** |

#### B2-OBS-2 — `TP {axis}`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `ProcessAxisStatusAndPositionAsync` |
| Caller | the feedback loop, per axis, through a direct vendor call that bypasses the shared dispatch path |
| Origin | feedback timer |
| Native unit | commanded (theoretical) position, controller counts |
| Ordering | second per-axis read, after the status word and before the actual position |
| Failure | same as B2-OBS-1: generic error, loop stops |
| Cleanup | none |
| Concurrency | same as B2-OBS-1 |
| Vendor verification | documented as the commanded-position read; the response text format on this firmware is NOT verified |
| Disposition | **BLOCKED** |

#### B2-OBS-3 — `RP {axis}`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `ProcessAxisStatusAndPositionAsync` |
| Caller | the feedback loop, per axis, through a direct vendor call that bypasses the shared dispatch path |
| Origin | feedback timer |
| Native unit | actual (encoder) position, controller counts |
| Ordering | third per-axis read |
| Failure | same as B2-OBS-1 |
| Cleanup | none |
| Concurrency | same as B2-OBS-1 |
| Vendor verification | documented as the actual-position read; the response text format on this firmware is NOT verified |
| Disposition | **BLOCKED** |

#### B2-OBS-4 — `MG @OUT[{output}]`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `ProcessDigitalOutputAsync` |
| Caller | the feedback loop, once per output in the closed output namespace |
| Origin | feedback timer |
| Native unit | output state; the Legacy parse is a text substring check for the digit `1` followed by a decimal point |
| Ordering | all output reads precede the per-axis reads in one poll pass |
| Failure | generic error, then the whole feedback loop stops |
| Cleanup | none |
| Concurrency | sequential inside one poll pass; passes can overlap across ticks |
| Vendor verification | the message command and the output-array operand are documented; the **response text shape is NOT verified**, and the substring parse would also accept unrelated text containing that sequence |
| Disposition | **BLOCKED** |

### B.3 Motion, brake and position-definition commands

#### `HV{axis}={homeSpeed}`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `HomeAxisAsync` |
| Caller | `FormCannonController.HandleAxisActionButton`, Home action |
| Origin | Operator Home button |
| Native unit | controller counts per second; the value is the Legacy home-speed candidate |
| Ordering | brake released, fixed delay, then home velocity, home switch search, begin motion |
| Failure | exception becomes one generic message; the brake stays released and nothing is aborted |
| Cleanup | none on failure |
| Concurrency | fire-and-forget async from the UI; no gate and no token |
| Vendor verification | documented per-axis velocity assignment used with the home command; numeric-format acceptance on this firmware NOT verified |
| Disposition | **LATER** |

#### `HM{axis}`

Same site, ordering and failure as the home velocity row. It starts the home-switch search for
that axis. Documented vendor command; per-axis behaviour on the installed machine is
`NOT VERIFIED`. **LATER**.

#### `BG{axis}`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `HomeAxisAsync`, `MoveAbsoluteAxisAsync`, `JogAxisAsync` |
| Caller | the three motion helpers |
| Origin | Home, Move and Jog actions, including the auto-move timer |
| Native unit | none (motion start) |
| Ordering | always the last command of its sequence |
| Failure | generic message; the previously configured motion parameters remain loaded in the controller |
| Cleanup | none |
| Concurrency | two axes can be started in parallel by the auto-move and standby dispatches |
| Vendor verification | documented begin-motion command |
| Disposition | **LATER** |

#### `ST {axis}`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `StopMoveAxisAsync` and `MotionStop` |
| Caller | Stop control, and the home-complete branch inside the feedback loop |
| Origin | Operator Stop, or a home switch sighting |
| Native unit | none |
| Ordering | standalone for Stop; in the home branch followed by a fixed delay and a position definition |
| Failure | the Operator path has no handler and propagates; the home branch is inside the poll loop, so a failure stops feedback |
| Cleanup | none |
| Concurrency | can be issued while a move dispatch is still in flight |
| Vendor verification | documented decelerate-to-stop command. It is a controlled stop, not an emergency stop, and nothing in this baseline treats it as one |
| Disposition | **LATER** |

#### `SB {output}` and `CB {output}`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `ToggleBrakeAxisAsync` |
| Caller | Home, Move and Jog helpers; the brake switch control; **and the feedback event handler** |
| Origin | Operator action, auto-move, and a feedback event (re-entrant command inside a read path) |
| Native unit | output index, derived as the axis position in the axis-name array plus one |
| Ordering | brake release is the first command of the home, move and jog sequences; brake set is standalone |
| Failure | generic message; the physical brake state is never read back, so a failed release is not detected before motion starts |
| Cleanup | none: no path sets the brake again after a failed move |
| Concurrency | issued from the UI thread and from the polling event with no serialization |
| Vendor verification | set-bit and clear-bit output commands are documented; the axis-to-output allocation is Owner-confirmed, but the **electrical polarity (which state is "released") is NOT verified** |
| Disposition | **LATER** |

#### `AC{axis}={accel}`, `SP{axis}={speed}`, `DC{axis}={decel}`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `MoveAbsoluteAxisAsync` |
| Caller | Move action, including the auto-move and standby dispatches |
| Origin | Operator Move, auto-move timer |
| Native unit | controller counts per second squared (acceleration and deceleration) and counts per second (speed) — but see the defect recorded in D-09 |
| Ordering | after brake release and a fixed delay: acceleration, speed, deceleration, target, begin motion |
| Failure | generic message; the values already accepted by the controller are not rolled back |
| Cleanup | none |
| Concurrency | two axes are configured in parallel with no serialization |
| Vendor verification | all three are documented per-axis parameter commands; the arithmetic that produces the operands is a Legacy construction, not a vendor rule |
| Disposition | **LATER** |

#### `PA{axis}={target}`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `MoveAbsoluteAxisAsync` |
| Caller | Move action and auto-move |
| Origin | Operator Move, auto-move timer |
| Native unit | controller counts; the Legacy value is produced by a floating-point expression from an integer target |
| Ordering | after the three motion parameters and before begin motion |
| Failure | generic message; no position verification afterwards |
| Cleanup | none |
| Concurrency | as above |
| Vendor verification | documented absolute-position command; the conversion expression is Legacy-specific and NOT verified against the machine |
| Disposition | **LATER** |

#### `JG{axis}={speed}`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `JogAxisAsync` |
| Caller | Jog controls |
| Origin | Operator Jog press |
| Native unit | controller counts per second; the sign carries the direction |
| Ordering | brake release, fixed delay, jog speed, begin motion |
| Failure | generic message; motion may already be active |
| Cleanup | none; the jog is stopped only by an explicit Stop |
| Concurrency | fire-and-forget; two jog axes can overlap |
| Vendor verification | documented jog command; this site does not set acceleration or deceleration, so the controller's current values apply |
| Disposition | **LATER** |

#### `DP{axis}=0` and `DE{axis}=0`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `ResetEncoderAxisAsync` and `MotionPositionReset` |
| Caller | Reset-encoder controls, and the home-complete branch of the feedback loop |
| Origin | Operator reset, or a home switch sighting |
| Native unit | position counts |
| Ordering | reset does position definition then encoder definition; the home branch does stop, fixed delay, then position definition |
| Failure | reset path reports one generic message; the home branch failure stops feedback |
| Cleanup | none |
| Concurrency | the home-branch definition runs inside a read path and is not synchronized with an Operator reset |
| Vendor verification | documented position-define and encoder-define commands |
| Disposition | **LATER** — and note D-07: the home reference is defined **only** by this polling-triggered command |

### B.4 B2 candidate disposition — verified subset

| Candidate | Kind | Vendor command family | Disposition |
| --- | --- | --- | --- |
| B2-OBS-1 | `AXIS_STATUS` | message command, status-word operand | **BLOCKED** |
| B2-OBS-2 | `COMMAND_POSITION` | message command, commanded-position read | **BLOCKED** |
| B2-OBS-3 | `ACTUAL_POSITION` | message command, actual-position read | **BLOCKED** |
| B2-OBS-4 | `DIGITAL_OUTPUT` | message command, output-array operand | **BLOCKED** |

The verified subset is **empty**. Each candidate is a real, documented vendor read, and each one
requires the raw command-and-response path. The B1 gate authorizes exactly six typed identity
exports and states that it authorizes "no raw controller command" and "no axis read"; the
source-boundary tests also pin the raw command API as a prohibited token. A seventh export would
be an authorization change, so it was not taken. See section G.2.

### B.5 Other commands present in the pack

#### `MG TIME`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `IsGalilActuallyConnected` |
| Caller | the shared dispatch path, before every command |
| Origin | every Operator action and every timer dispatch |
| Native unit | controller time, native integer; the value is discarded |
| Ordering | immediately before each individual command, so every command costs two round trips |
| Failure | any exception is converted to "not connected"; timeout, disconnect and controller error are indistinguishable |
| Cleanup | none |
| Concurrency | per caller, with no serialization |
| Vendor verification | documented message command with the time operand; response format on this firmware NOT verified |
| Disposition | **BLOCKED** — a read with no authorized surface (it is a liveness read, not one of the four observed-state candidates) |

#### `OP 0`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `ClearAllOutputs` (both overloads) and the dead `GalilConnect` block |
| Caller | `InitializeGalilConnection`, after a fixed post-connect delay, before any Operator action |
| Origin | connect path |
| Native unit | none; the operand clears the whole output bank |
| Ordering | first write after a successful connection |
| Failure | the live overload returns success (`1`) even when its liveness probe fails and nothing was sent, and the connect path ignores the returned value; only an exception yields `-1` |
| Cleanup | none: it is itself an output write |
| Concurrency | inside the connect task, with no exclusion against a still-running feedback loop |
| Vendor verification | documented output-bank command; the physical effect on brake outputs is a machine fact and NOT verified |
| Disposition | **EXCLUDED** |

#### `TI0`

| Field | Evidence |
| --- | --- |
| File / method | `FormDataCenter.cs` / `ProcessDigitalInputsAsync` |
| Caller | none — the method returns before the command, and its call site in the poll pass is commented out |
| Origin | unreachable |
| Native unit | input-bank integer (had it run) |
| Ordering | would precede the per-axis reads |
| Failure | not reachable |
| Cleanup | not applicable |
| Concurrency | not applicable |
| Vendor verification | the input-array read is documented; the source is dead code |
| Disposition | **EXCLUDED** |

Because `TI0` never runs, the drive-alarm projection that depends on the input bank never updates
either. Any Operator-facing alarm indication derived from it is dead in the Legacy application.

---

## C. Control flows

### C.1 Connection and reconnection

`FormCannonController.SetWCIndex` stops the feedback loop, switches the water-cannon index,
reinitializes the connection, restarts feedback, reloads the path list and activates the form.
`InitializeGalilConnection` resolves an endpoint for the index, marks the previous instance
offline, constructs a **new** controller object, assigns the endpoint, connects, waits a fixed
delay, clears all outputs and raises the connected event. The previous object is not closed
explicitly; the code relies on the new instance replacing it.

A dead `GalilConnect` block still exists earlier in the file (commented out) and contains a second
variant of the same sequence, including its own output clear.

### C.2 Command dispatch

Most commands go through the shared dispatch path in B.1. The path performs a liveness read,
throws when it fails, runs the vendor call on a background task and re-wraps every exception with
the command text. The two position reads in the feedback loop bypass it and call the vendor object
directly. There is no queue, no outstanding-command tracking, no timeout and no token.

### C.3 Feedback polling

One timer starts a poll pass. A pass:

1. reads the four digital outputs in order;
2. per axis, reads the status word, the commanded position and the actual position;
3. decodes the status word into a fixed-width binary string;
4. raises a status event with the two positions and a millimetre-shaped display value;
5. decodes the home, reverse-limit and forward-limit bits and raises a sensor event;
6. on the home bit, issues a stop, waits, defines position zero and clears the homing flag;
7. updates a per-axis motion counter used by the move-completion logic.

Any exception in the pass reports one generic message and stops the whole feedback loop; nothing
restarts it except a reconnection.

### C.4 Home

The Operator Home action disables the button, releases the brake, waits, sends home velocity,
home-switch search and begin motion, and sets an in-memory homing flag. Completion is detected
**only** by the poll loop seeing the home bit; that branch sends a stop, waits, defines position
zero and clears the flag. There is no timeout, no abort, no verification that the axis actually
left the switch, and no failure path that re-sets the brake.

### C.5 Absolute move

The Operator Move action releases the brake, waits, configures acceleration, speed and
deceleration, sends the absolute target and begins motion, and raises a MotionAbsolute event
**before** the move completes. The target comes from a UI numeric control. The auto-move path uses
the same action for X and Z in parallel.

### C.6 Jog

The Jog action releases the brake, waits, sends a signed jog speed and begins motion. Release of
the control sends a stop for the mapped axis. No acceleration or deceleration is set at this site.

### C.7 Stop

The Stop action sends a stop for the mapped axis and nothing else: no brake set, no verification,
no state update. No single Stop-all control exists in the reviewed Operator surface, and the
auto-move has its own separate stop that only halts the timer.

### C.8 Brake

The brake switch control releases or sets the brake for the mapped axis. The **same** operation is
also invoked from the feedback event handler when a move completes, so a command is issued from
inside a read path. The brake state is never read back, so the UI reflects the last commanded
state, not the observed one.

### C.9 Auto-move over a cleaning path

The auto-move timer starts from the Operator control. On each tick it reads the mapped X and Z
motion flags and all four axis in-motion flags; if anything is moving it returns, otherwise it
dispatches the next coordinate as two parallel absolute moves, waits a fixed delay and increments
the step. After the last coordinate it dispatches two parallel moves to the standby position,
waits, stops the timer and re-enables Start. The step count is a hard-coded six.

The tick handler is asynchronous and the timer is not stopped while the handler awaits, so a tick
can re-enter the handler and dispatch the next step while the previous dispatch sequence is still
running. The only guard is the moving flag check, which is not atomic with the dispatch.

### C.10 Water-cannon switch

Switching the index performs the full reconnection sequence in C.1. In-flight poll tasks from the
previous connection are not cancelled or fenced; the stop-then-start of the timer is the only
ordering mechanism.

### C.11 Motion configuration persistence

Motion settings (jog speed and ratio, move acceleration, speed and deceleration, home speed,
standby X and Z, and the per-index axis-name map) are written to a JSON file beside the
executable and loaded at form load. The file carries no schema version, no integrity check and no
provenance. A corrupt file shows a message box, resets the axis map to the Legacy default and
**overwrites** the file. When the file is missing, a different pair of speed defaults is assigned
and immediately persisted, so two different "default" sets exist in the same application.

### C.12 Cleaning-path persistence

Each path is a name plus six coordinate pairs. Save loads the whole list, replaces a
case-insensitively matching name or appends a new one, and rewrites the file. Load accepts a path
whose stored list has at least six entries and copies the first six into the controls. There is no
range check against any envelope, no unit, no rounding rule (the controls hold decimals and the
move path truncates to an integer) and no authorisation or provenance record.

### C.13 Shutdown and error handling

Error handling collapses every failure into one status message; some paths additionally stop
feedback. There is no error code, no correlation identifier and no distinction between a refused
command, a timeout and a lost connection. Command text and vendor error text are embedded in the
message.

---

## D. Defects observed in the Legacy baseline

Each defect is recorded with its consequence. These are observations about the Legacy source, not
findings about the machine.

- **D-01 — Liveness probe as connection state.** Every command is preceded by a separate read; a
  failed read is treated as "disconnected". A controller that is reachable but busy, or a command
  that is refused, is indistinguishable from a lost link.
- **D-02 — No single-outstanding rule.** Any number of callers can dispatch concurrently; nothing
  serializes commands per controller.
- **D-03 — No generation fence.** A poll pass or command started before a water-cannon switch can
  complete afterwards and be applied to the new context.
- **D-04 — Unconditional output clear on connect.** The connection path attempts to write the
  whole output bank before any Operator action; the helper returns success even when its liveness
  probe fails and nothing was sent, and the connect path ignores that value entirely.
- **D-05 — Brake commanded, never observed.** Brake release/set is a write only; no read confirms
  it, and motion starts immediately after a fixed delay regardless of the physical result.
- **D-06 — Command inside a read path.** The feedback event handler issues a brake command, so a
  poll can produce a write.
- **D-07 — Home reference defined only by polling.** Position zero is defined by a command issued
  from the poll loop when it sees the home bit. If the bit is never seen, the axis stays in the
  homing state with no timeout; if the bit is seen spuriously, position zero is defined at the
  wrong place. The explicit position define in the home command sequence is commented out.
- **D-08 — Two different position scalings.** The commanded position is converted with an extra
  halving that the actual position does not have, so the two displays disagree by construction.
- **D-09 — Acceleration arithmetic.** Acceleration and deceleration operands are produced by
  squaring the Operator-facing value and multiplying by a motor constant; speed is produced by a
  single multiplication. The three operands therefore do not share a unit.
- **D-10 — Target truncation.** The UI holds a decimal coordinate; the action path converts it to
  an integer before the floating-point conversion to controller counts, so a stored coordinate is
  not the coordinate that is sent.
- **D-11 — Hard-coded bit meanings and polarity.** The status word is decoded with fixed bit
  positions and "active low" assumptions (home, reverse limit, forward limit). The bit order and
  polarity for the installed family are unconfirmed, and there is no "unknown" representation: a
  parse failure becomes all-zero, which the UI then renders as "not active".
- **D-12 — Unreachable input read.** The digital-input read returns before issuing its command and
  its call site is commented out; the drive-alarm projection fed by it never updates.
- **D-13 — Parse failure becomes zero.** Both the integer and the double helper return zero when
  the text does not parse, so an unreadable status or position is displayed as a valid zero.
- **D-14 — One failure stops all feedback.** Any exception in a poll pass stops the loop for every
  axis and every output; nothing distinguishes a single bad response from a dead link.
- **D-15 — Duplicate and dead implementations.** Two output-clear implementations exist (one in a
  commented-out connection block, one live), and an entire earlier connection routine remains as
  dead code.
- **D-16 — Configuration without schema or atomicity.** Two JSON files beside the executable, no
  version, no integrity, no backup, non-atomic rewrite, and two conflicting default sets.
- **D-17 — Cleaning path without validation.** Six fixed pairs, no envelope check, no unit, no
  rounding rule, case-insensitive name replacement, and a load that tolerates extra stored pairs.
- **D-18 — Auto-move gating.** The step dispatcher is guarded by a moving flag that is read
  without synchronisation, the tick handler is asynchronous while the timer keeps ticking, and the
  completion logic depends on a separate counter updated by the poll loop.
- **D-19 — Reconnection without close.** A new controller object is constructed for each index
  switch; the previous one is not closed deterministically.
- **D-20 — Error text leaks command material.** Re-wrapped exceptions embed the full command text
  and vendor error text and present it to the Operator.
- **D-21 — Two dispatch paths.** The position reads bypass the shared dispatch path, so they skip
  the liveness probe and the shared error handling; a failure there behaves differently from the
  same failure on any other command.

---

## E. WJSS corrections and their status in this PR

"Corrected" below means the WJSS design already forbids the defect by construction, or PR #31
implements the model that makes it impossible. It never means the physical behaviour was verified.

| Defect | WJSS correction | Status |
| --- | --- | --- |
| D-01 | Connection state is never inferred from a command echo; the B1 session classifies the identity read and reports a refusal code instead | Enforced by B1 (existing) |
| D-02 | One session, one outstanding operation, fail-fast busy refusal; no queue | Enforced by B1 and B2 |
| D-03 | One generation per session, stale results rejected; a new generation requires a new session | Enforced by B1 and B2 |
| D-04 | No output write exists in any WJSS surface; the Legacy connect-time clear is excluded | Not implemented — excluded |
| D-05 | No brake command exists; brake state stays `UNKNOWN`; an unknown brake state blocks motion | Not implemented — gate closed |
| D-06 | A read path may not issue a command: the B2 session contains no I/O at all | Enforced by B2 |
| D-07 | No home definition or reset exists; homing is `NOT_HOMED`; an absolute Move requires a valid Home and commissioned limits | Not implemented — gate closed |
| D-08, D-09, D-10 | No conversion is implemented; native units are preserved and no engineering value is produced | Enforced by B2 model |
| D-11 | Interpretation states include `UNCONFIRMED`; no limit, home or polarity meaning is represented; unknown is never inactive or zero | Enforced by B2 model |
| D-12 | No input read exists; no alarm or drive state is derived | Not implemented — excluded |
| D-13 | No parse failure path exists: no read is issued, and an unavailable observation has no value at all | Enforced by B2 |
| D-14 | A failure is a refusal code on one outcome; there is no shared loop to stop | Enforced by B1 and B2 |
| D-15 | Dead code is not carried; only the referenced surface exists | Enforced by B2 |
| D-16, D-17 | No configuration file, no path persistence, no default promotion; candidate values stay `NOT_CONFIGURED` / `NOT_COMMISSIONED` | Not implemented — out of scope |
| D-18 | No auto-move, no timer, no step dispatcher; Motion execution is absent | Not implemented — out of scope |
| D-19 | Close is deterministic and idempotent; a closed session never reopens | Enforced by B1 and B2 |
| D-20 | Outcomes are redacted and carry a vocabulary code; no command text and no vendor error text | Enforced by B1 and B2 |
| D-21 | One dispatch surface: the B2 session has no alternate path, and a refusal is produced before anything is evaluated | Enforced by B2 |

Nothing in this table upgrades a `NOT VERIFIED` hardware fact to a verified one.

---

## F. Checkpoint mapping

### F.1 Command → checkpoint

| Command shape | Disposition | Checkpoint that may ever carry it |
| --- | --- | --- |
| `MG TIME` | BLOCKED | none at this checkpoint; a read needs an Owner-authorized read surface |
| `MG _TS{axis}` | BLOCKED (B2-OBS-1) | B2 contract only (PR #32 C1 candidate) |
| `TP {axis}` | BLOCKED (B2-OBS-2) | B2 contract only (PR #32 C1 candidate) |
| `RP {axis}` | BLOCKED (B2-OBS-3) | B2 contract only (PR #32 C1 candidate) |
| `MG @OUT[{output}]` | BLOCKED (B2-OBS-4) | B2 contract only (PR #32 C1 candidate) |
| `OP 0` | EXCLUDED | none |
| `TI0` | EXCLUDED | none |
| Home velocity, home switch search, begin motion | LATER | a future Owner-authorized motion stage (roadmap G-series) |
| Stop | LATER | a future Owner-authorized motion stage |
| Brake release / set | LATER | a future Owner-authorized motion stage |
| Acceleration, speed, deceleration, absolute target | LATER | a future Owner-authorized motion stage |
| Jog speed | LATER | a future Owner-authorized motion stage |
| Position define, encoder define | LATER | a future Owner-authorized motion stage |

### F.2 Checkpoint statement

- **B1** — the existing identity foundation: six typed identity exports, one open, three identity
  reads, deterministic close. It reads no axis and sends no raw command.
- **B2** — this PR: the typed observed-state vocabulary, the closed state model and the
  one-owner/one-outstanding/generation-fenced session. Because the verified subset is empty, the
  only reachable outcomes are `UNAVAILABLE` (valid subject, no authorized read surface) and
  `REFUSED` (request not allowed to proceed). No read is performed.
- **C1** — the next contract, documented in `docs/MOTION_OBSERVED_STATE_FOUNDATION.md` and **not
  implemented**. C1 would connect an Owner-authorized read surface to the Application; it needs a
  seventh typed export (or an equivalent vendor-supported read API), a single connection owner, a
  bounded response rule and the interpretation evidence that section G lists as missing.
- **LATER** — every motion, brake and position-definition command. None is prepared here.

This document does not add a roadmap stage, does not reorder the critical path and does not
replace any planning document.

---

## G. Evidence gaps, STOP items and Owner decisions

### G.1 Evidence gaps

1. The exact response text of the installed firmware is unknown for every read shape in this
   baseline; the Legacy parser is permissive (substring and integer-prefix parsing), so a matching
   parse is not evidence of a matching format.
2. The status-word bit order and polarity for the installed controller family are unconfirmed. The
   Legacy code assumes active-low home, reverse-limit and forward-limit bits at fixed positions;
   that assumption is not verified and is not adopted.
3. The meaning of the remaining status bits (including the motion bit) is unconfirmed beyond the
   Legacy comment that names it.
4. The electrical polarity of the brake outputs is unconfirmed; only the axis-to-output numbering
   is Owner-confirmed.
5. The physical unit of a controller count, and therefore every conversion to millimetres, is
   unconfirmed; the Legacy constants that express it are recorded by name only and are not
   promoted.
6. Whether the installed controller uses the same output numbering as the Owner-confirmed brake
   allocation for every cabinet is unconfirmed.
7. Whether an output read reflects the commanded or the physical output state is unconfirmed.
8. The behaviour of the home switch outside the polling window (debounce, re-trigger, release) is
   unconfirmed.
9. No timing, repeatability or failure-injection evidence exists for any command in this baseline.
10. The Legacy application's own defaults disagreed with each other, so no "Legacy default" can be
    treated as the intended configuration.

### G.2 STOP conditions reported to the Owner

- **S-1 — B2 has no authorized read surface.** Every candidate requires the raw
  command-and-response path, which the B1 authority line forbids and the source-boundary tests
  pin as a prohibited token. The verified subset is empty. PR #31 therefore implements the model
  and the refusal, not a read. **Owner decision required** before any read can exist.
- **S-2 — A seventh export would be an authorization change, not an implementation detail.** It
  would add a new native export to the allow-list and would need its own gate, its own Owner-local
  validation and a new session branch.
- **S-3 — No dependency, project or lock change was made** to avoid this, because none of them
  would authorize a read. A new project would also create a genuine lock file and require an
  Owner-local restore, which is out of scope here.
- **S-4 — B2-H (hardware) is not defined by this PR.** There is no B2 hardware operation to run:
  no read is authorized. The Owner-local step for this checkpoint is a software gate only
  (section F of `docs/MOTION_OBSERVED_STATE_FOUNDATION.md`).

### G.3 Owner decisions requested

1. Authorize, defer or reject a future **read-surface gate** that would permit a typed,
   allow-listed read of the four candidates (or a subset), with the exact export, the connection
   ownership rule and the response-format evidence named in advance.
2. Confirm whether the installed controller family's status-word bit order and polarity evidence
   exists; if it does not, decide whether any interpreted state may ever be produced or only
   `RAW_OBSERVED`/`UNCONFIRMED`.
3. Confirm the output numbering for every cabinet, or decide that output observation stays out of
   scope.
4. Confirm that no Legacy constant, default or coordinate may be promoted, even after a read is
   authorized, until a separate commissioning evidence gate proves it.

### G.4 Not verified by this document

No build, no test execution, no controller contact, no read, no motion, no brake operation and no
configuration change occurred. Every hardware statement in this document is a reading of Legacy
source text, not a measurement. The Agent environment has no .NET SDK, so no compile or test
result is claimed here.
