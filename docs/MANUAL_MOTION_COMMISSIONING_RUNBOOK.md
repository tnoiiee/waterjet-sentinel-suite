# Manual Motion Commissioning Runbook — WSB1 A/B/C/D

**Document class:** OWNER-LOCAL SOFTWARE VALIDATION AND COMMISSIONING
BOUNDARY RECORD. This runbook covers the isolated
`apps/motion-commissioning` application proposed by PR #34. It does not
authorize hardware use. A separate post-merge Owner Scope Gate must
authorize the exact physical WSB1 activity before the hardware-capable mode
is invoked with real Owner-local values.

**Scope lock.** WSB1 only. Axis A is Horizontal and uses brake output 1;
Axis B is Vertical and uses brake output 2; Axis C is Horizontal and uses
brake output 3; Axis D is Vertical and uses brake output 4. There is no
WSB2–WSB4, coordinated simultaneous multi-axis motion (no synchronized
Begin, interpolation, contouring, gearing or combined-axis command),
automatic sequence, cleaning path, standby return,
Runtime/API/Kiosk/Preview live motion or Production composition in this
scope. Independent per-axis operations may overlap in time: two axes may
each hold an active operation in the same interval because their typed
calls are interleaved one at a time through the single serialized
controller path. Software support for Axis B/C/D was added in
Phase 1A (2026-10-22) as software only: the completed hardware record
remains the Axis A Hardware Trial (FINAL PASS, Owner record). No B/C/D
hardware trial or hardware authorization exists in this runbook; B/C/D
wiring and route continuity rest on the Legacy Actual Run evidence (see
`docs/evidence/MANUAL_MOTION_LEGACY_CONTINUITY.md`), and hardware use of
any axis still requires the separate Owner Scope Gate.

**Diagnostic identity (Canonical Topology Adoption, 2026-10-21; Phase 1A,
2026-10-22).** Under the canonical topology (`OWNER_CONFIRMED_REV2`), the
WSB1 Axis A / Axis B pair is diagnostically the axis pair of **WJ2** —
installed LEFT / UPPER, Target Coverage RIGHT / UPPER, Axis A Horizontal,
Axis B Vertical — and the WSB1 Axis C / Axis D pair is diagnostically the
axis pair of **WJ3** — installed REAR / UPPER, Target Coverage FRONT /
UPPER, Axis C Horizontal, Axis D Vertical. The software addresses **WSB1
Axis A/B/C/D and brake outputs 1–4**. There is still no Water Jet
selection and no Cleaning Path execution (Cleaning Path is Phase 3);
Home/DP/DE/Move real commissioning values remain `NOT_CONFIGURED` until
the Phase 2 profile gate; WSB2–WSB4 remain unauthorized (Phase 1B); and
none of this authorizes hardware activity.

**Validation position at authoring.** The authoring sandbox has no .NET SDK.
Restore, build, .NET tests and lock generation were therefore **NOT RUN**
there. No native DLL was loaded, no endpoint was contacted and no Controller
command was executed. The Owner-local exact-head run in section 3 is the
required software validation of record.

## 1. What the software can and cannot do

The Application session exposes only typed requests:

- brake release and brake apply for one axis;
- hold-to-run Jog positive or negative for one axis;
- per-axis Stop;
- Home for one axis;
- define position zero (DP semantics) for one axis;
- define dual zero (DE semantics) for one axis, only when explicitly
  permitted by the Owner-local profile; its effect remains unverified;
- one bounded absolute Move for one axis after a verified Home in the same
  session;
- raw status, TP, RP and brake-output observations, shown separately from
  profile-based interpretation; and
- a redacted, codes-only operation/session log.

The axis workflow is deliberately ordered:

1. **Brake:** motion operations require a profile-attributed brake release
   before any Begin. Brake commands are command-state only; the observed
   output remains separate and is not presented as physical proof.
2. **Jog:** brake release → configured settle → signed Jog speed → Begin.
   Releasing the control, losing capture/focus, heartbeat expiry, maximum
   hold or session close enters the supervised Stop path; the brake is
   applied only after the profile's stopped-observation rule is satisfied.
3. **Home:** brake release → settle → Home speed → Home search → Begin.
   A fresh active home input enters Stop → define position zero → TP/RP
   zero verification → brake apply. No Home is claimed from dispatch alone.
4. **Manual zero:** define position zero is accepted only when the session
   can verify TP/RP near zero afterward. Manual DP/DE clears the
   session's prior Home claim because it changes the coordinate basis.
5. **Move:** requires a verified session Home and a target inside the
   Owner-local native-unit bounds. The sequence is brake release → settle →
   acceleration → speed → deceleration → absolute target → Begin, followed
   by observation of inactivity and TP/RP within the configured tolerance,
   then brake apply. No relative target exists.
6. **Stop:** Stop pre-empts ordinary work and uses the previously approved
   typed per-axis Stop path (A/B/C/D). It is dispatch only. `StopConfirmed` is always
   false. A Stop timeout does **not** make the software invent a stopped
   state and does **not** automatically claim a brake application.

Manual Motion operations are independent per axis: at most one operation
per axis, and multiple axes (A/B/C/D) may hold active operations during the
same interval. An active operation on Axis A never blocks an otherwise
valid operation on B, C or D (and likewise for every other axis); same-axis
exclusion remains mandatory — while one axis runs, Home, Jog, Move, DP, DE
and standalone Brake on THAT axis are refused, while Stop on that axis stays
accepted. Stop is per axis and touches only the requested axis. Hold-to-run
Jog presses and their heartbeats are per axis. Session close stops every
active motion operation and awaits each axis independently; a communication
or native fault is session-global and terminally represents every active
operation, with one serialized best-effort Stop per axis whose motion
began. This independent per-axis motion is NOT coordinated trajectory
control: there is no synchronized Begin, interpolation, contouring,
combined-axis Stop or automatic Cleaning Path, and no simultaneous start or
arrival is ever promised. The Legacy commissioned workflow requires
Horizontal/Vertical command independence; this model restores it. A
communications fault moves the session to `FAULTED`; there is no retry,
reconnect or replay. A stale generation result is rejected. Native calls
are serialized by one controller-wide gate in hardware-capable composition,
and final cleanup is invalidate → close → release primary module → release
companion module.

The software cannot express a drive-readiness write (no SH/MO), a relative
move (no PR), an output-bank write (no OP), a burn/save command (no BN),
soft-limit writes, a Controller program, discovery, raw command text or an
engineering-unit conversion.

## 2. Owner-local profile

The profile is external to Git. `WJSS_MANUAL_MOTION_PROFILE` names one fully
qualified local JSON file of at most 65,536 bytes. The file is read once and
never written by the application. Its path and values must not be committed,
pasted into evidence or included in logs.

The strict schema is `wjss.manual-motion-profile/1`, controller `WSB1`, with
exactly `schema`, `controller`, `session` and `axes` at the top level.
Unknown, duplicate, wrongly typed or out-of-range properties refuse the
whole profile. Comments and trailing commas are not accepted. Absent and
`null` values both mean `NOT_CONFIGURED`; no Legacy value or software
default is substituted. An operation that needs a missing value is refused,
while otherwise independent typed operations can still be evaluated.

### Public-safe structural example only

The following is a shape example, **not** a commissioning profile. Every
operation value is deliberately `null`, so it must not be used to seek motion.
The only numeric entries are the already Owner-confirmed brake-output
allocations: A → 1 and B → 2.

```json
{
  "schema": "wjss.manual-motion-profile/1",
  "controller": "WSB1",
  "session": {
    "observationIntervalMilliseconds": null,
    "observationFreshnessMilliseconds": null,
    "jogHeartbeatTimeoutMilliseconds": null,
    "jogMaximumHoldMilliseconds": null
  },
  "axes": {
    "A": {
      "brakeOutput": 1,
      "brakeReleaseAction": null,
      "brakeSettleMilliseconds": null,
      "jogSpeed": null,
      "homeSpeed": null,
      "homeTimeoutMilliseconds": null,
      "moveAcceleration": null,
      "moveSpeed": null,
      "moveDeceleration": null,
      "moveMinimumTarget": null,
      "moveMaximumTarget": null,
      "positionTolerance": null,
      "motionTimeoutMilliseconds": null,
      "stopTimeoutMilliseconds": null,
      "motionStatusBit": null,
      "motionActiveValue": null,
      "homeStatusBit": null,
      "homeActiveValue": null,
      "dualZeroPermitted": null
    },
    "B": {
      "brakeOutput": 2,
      "brakeReleaseAction": null,
      "brakeSettleMilliseconds": null,
      "jogSpeed": null,
      "homeSpeed": null,
      "homeTimeoutMilliseconds": null,
      "moveAcceleration": null,
      "moveSpeed": null,
      "moveDeceleration": null,
      "moveMinimumTarget": null,
      "moveMaximumTarget": null,
      "positionTolerance": null,
      "motionTimeoutMilliseconds": null,
      "stopTimeoutMilliseconds": null,
      "motionStatusBit": null,
      "motionActiveValue": null,
      "homeStatusBit": null,
      "homeActiveValue": null,
      "dualZeroPermitted": null
    }
  }
}
```

For a real Owner-local profile, every value must be established under the
separate hardware commissioning gate. In particular:

- `observationIntervalMilliseconds` is the cadence between the starts of
  complete observation cycles, not a delay between individual reads. Each
  cycle reads, per profile-allowed axis, the status word first, then TP,
  RP and the configured brake-output state, one native read per session
  step, so the configured brake output remains under observation while an
  operation runs and the status word is refreshed every cycle. This
  scheduling semantics changes no hardware authorization: simulation and
  automated tests remain software evidence only.
- `brakeReleaseAction` accepts the typed profile values for setting or
  clearing the individual output; electrical polarity remains
  `UNCONFIRMED` until separately proven and recorded.
- Motion/home status bit numbers and active values are local commissioning
  interpretation inputs, not Legacy defaults.
- Speeds, acceleration, deceleration and targets are native controller
  units only.
- `moveMinimumTarget` must be below `moveMaximumTarget` when both are
  configured, and the two status-bit numbers must differ when both are
  configured.
- An axis may be omitted entirely; it is then not allowed by the profile.

Git ignore patterns cover `**/*.wjss-manual-motion-profile.json`, but that is
only a guardrail. Keep the actual profile outside the repository.

## 3. Owner-local exact-head software validation

Run this section on the exact PR head before review/merge. Do **not** set
`WJSS_MANUAL_MOTION_CONSENT`, `WJSS_MANUAL_MOTION_PROFILE`, the real endpoint
or the real Gate A values for this software-only validation. Fake-kernel and
simulated-port tests must not load a native module or contact a Controller.

```powershell
git rev-parse HEAD
git status --porcelain

dotnet restore WaterJetSentinelSuite.sln

git status --porcelain

dotnet build WaterJetSentinelSuite.sln -c Release --no-restore

dotnet test tests/runtime.tests/Wjss.Runtime.Core.Tests.csproj -c Release --no-build
dotnet test tests/motion-library.tests/Wjss.MotionLibrary.Tests.csproj -c Release --no-build
dotnet test WaterJetSentinelSuite.sln -c Release --no-build

node tools/boundary-scan/boundary-scan.mjs .
git diff --check
```

`Directory.Build.props` enables lock-file restore. The new application lock
at `apps/motion-commissioning/packages.lock.json` may be produced only by
the genuine `dotnet restore` above. Never hand-author or reformat it.
Report the exact head, each command's exit/result, and any lock drift for
review. Stop on unexplained changes to an existing lock, any failed build or
test, or any evidence that a test loaded a vendor module or used a network
endpoint.

## 4. Simulated smoke check

After the software validation passes, the simulated mode may be launched
locally with the profile variable unset:

```powershell
Remove-Item Env:WJSS_MANUAL_MOTION_PROFILE -ErrorAction SilentlyContinue
dotnet run --project apps/motion-commissioning/Wjss.MotionCommissioning.csproj -c Release --no-build -- simulated
```

Expected software observations:

- the banner is visibly labelled as simulated;
- no native endpoint, endpoint value or local path is displayed;
- Axis A is labelled Horizontal and Axis B Vertical;
- raw observations update independently of any physical device;
- only one operation can be active;
- Jog ends on release/capture loss and on heartbeat or maximum-hold expiry;
- Home, DP verification, DE-unverified and bounded absolute Move use the
  synthetic simulation state only;
- `StopConfirmed` never becomes true; and
- closing the window ends the session without reopening a port.

A simulated pass is not hardware evidence, not commissioning and not proof
of any profile value, status-bit meaning, brake polarity, coordinate basis,
travel bound or physical stop.

## 5. Hardware-capable composition boundary

The exact invocation is:

```text
Wjss.MotionCommissioning.exe wsb1-hardware
```

Exit `2` means a precondition was refused before native load/open; exit `4`
means the invocation shape was invalid. Exit `0` after the window closes is
an application-lifecycle result only, never a stopped/safe/commissioned
claim.

Before a profile is loaded, the invocation gate consumes the one-use exact
value in `WJSS_MANUAL_MOTION_CONSENT` from the application process and
resolves the established Owner-local endpoint and Gate A variables. The
Owner must also unset consent in the invoking shell afterward. At the
session open, the owner performs Gate A proof, restricted companion/primary
load and loaded-path proof, approved export resolution, one direct-mode
selection and one open. If profile parsing refuses, the session does not
open the port. No output includes the profile path/value set, endpoint,
native path/hash/version, consent, exception text, command text or raw
native reply.

### Required separate gate

Even after PR #34 is merged, do not run `wsb1-hardware` against WSB1 until
the Owner records a separate hardware Scope Gate that at minimum establishes:

1. the exact merged commit and Owner-local validation result;
2. the physical no-load/safe-state preparation and responsible operator;
3. the commissioned profile values and their evidence, without putting the
   values in Git;
4. the allowed first operation, permitted axes and stop/abort criteria;
5. confirmation that drive-readiness handling outside this software is in
   the intended state, because this software writes no SH/MO;
6. the observed brake polarity and status-bit interpretation evidence to be
   captured, rather than assumed; and
7. the evidence fields that may be returned in redacted form.

Until that gate exists, every real profile value remains
`NOT_CONFIGURED`, every axis remains `NOT_COMMISSIONED`, every electrical or
status interpretation remains `UNCONFIRMED`, and the correct hardware
action is **do not invoke**.

## 6. Evidence and non-claims

Record only the exact commit, command/result codes expressly designed for
redacted reporting, test counts and pass/fail results, and whether cleanup
flags reported invalidate/close/release completion. Never record endpoints,
local paths, hashes, serial identifiers, profile values, consent, native
reply text or exception details in Git, PR text or chat.

Nothing in a software or simulated pass proves:

- that a physical brake released or applied;
- that a drive was ready;
- that an axis stopped;
- that a home switch, status bit or output polarity has the modelled
  meaning;
- that a native-unit target is inside safe physical travel; or
- that WSB1 or either axis is commissioned.

Related evidence: `docs/evidence/MANUAL_MOTION_LEGACY_CONTINUITY.md`,
`docs/evidence/LEGACY_MOTION_COMMISSIONING_BASELINE.md`, and
`docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md`.
