# Manual Motion Commissioning Foundation — Software Design Record

**Status: DEVELOPMENT CHECKPOINT RECORD. NOT Owner-reviewed. NOT merged. No hardware action has
been taken and none is authorized by this document.**

Scope-gate authority line (unchanged from the approved motion foundation Scope):

`APPLICATION-OWNED SOFTWARE FOUNDATION ONLY / NO CONTROLLER CONNECTION / NO CONTROLLER READ /
NO MOTION COMMAND / NO BRAKE OUTPUT / NO RESET ENCODER / NO HOME / NO JOG / NO MOVE / NO ADAM
ACCESS / NO TC WORK / NO CLEANING, PUMP OR VALVE WORK / NO ENDPOINT CONFIGURATION / NO NIC CHANGE
/ NO LEGACY VALUE PROMOTED TO PRODUCT CONFIGURATION.`

This record describes **software that exists in the repository at this revision**. Every value the
software publishes is either Owner-confirmed data or an explicit not-configured / not-commissioned /
unknown status. Nothing here is device evidence.

---

## 1. What this revision is

The manual motion commissioning foundation is the Application-owned software surface that will later
drive the four motion controllers that move the eight Water Jets. At this revision it contains:

* the Owner-confirmed motion topology (four controllers, sixteen axes, eight Water Jet axis
  allocations, the brake output plan and the limit input plan);
* the standing commissioning state of every controller and axis — all not configured, not connected,
  not commissioned, not homed;
* the motion command model (intent, evaluation, session, refusal vocabulary);
* an **authorization gate that is CLOSED**, which refuses every motion intent on every axis before
  any hardware path is reached;
* the read-only publication of all of the above in the Runtime Snapshot and in the Preview
  Engineering page.

It does **not** contain a controller library, a connection, an endpoint, a driver, a command
encoder, a homing routine, a jog routine, a brake write, an encoder reset, or any device access of
any kind. There is no code path in this revision that can produce a controller command string.

---

## 2. Owner-confirmed data (encoded as data, never derived)

| Fact | Value | Basis |
| --- | --- | --- |
| Axis role | A and C are the **Horizontal Axis**; B and D are the **Vertical Axis** | Owner-confirmed |
| Brake output | axis A → output 1, B → output 2, C → output 3, D → output 4 | Owner-confirmed |
| WJ1 (WSB1) | A, B | Owner-confirmed |
| WJ2 (WSB1) | C, D | Owner-confirmed; conflicts with Drawing rev1 (§3) |
| WJ3 (WSB2) | C, D | Owner-confirmed |
| WJ4 (WSB2) | A, B | Owner-confirmed; conflicts with Drawing rev1 (§3) |
| WJ5 (WSB3) | C, D | Owner-confirmed |
| WJ6 (WSB3) | A, B | Owner-confirmed |
| WJ7 (WSB4) | A, B | Owner-confirmed |
| WJ8 (WSB4) | C, D | Owner-confirmed |
| Controller model | DMC-B140-M (four axes) | Owner-provided WSB cabinet drawing |
| Controllers | four, one per WSB cabinet | **Inferred** — see §3 |
| Position sense | axis A: encoder; axes B, C, D: installed switch set | Owner-confirmed |
| Limit inputs | Reverse, Home and Forward on every axis (three inputs) | Owner-confirmed |
| Initial Jog Speed | 10, **controller units**, Legacy candidate, unverified, display only | Owner-provided; never executable |

The Operator-facing axis wording is **"Horizontal Axis"** and **"Vertical Axis"** only. No other
axis wording (X/Y, left/right, up/down, cannon axis numbering) exists anywhere in the software.

---

## 3. Inferences and unresolved items (explicit, never hidden)

| Item | Status | Why it is not confirmed |
| --- | --- | --- |
| One controller per WSB cabinet | `INFERRED_FROM_OWNER_CONFIRMED_MAPPING` | It follows from the confirmed Water Jet allocation (each WSB drives exactly one A,B pair and one C,D pair), but the Owner has not separately confirmed the controller count per cabinet. |
| WJ2 / WJ4 axis allocation | `DRAWING_REV1_CONFLICT_WJ2_WJ4`, `OWNER_DECISION_REQUIRED` | The Drawing rev1 expectation for these two Water Jets is the other axis pair on the same cabinet. Both cannot be correct. The conflict is **published with the state**, never suppressed, and the affected allocation must not be used for motion until the Owner rules. |
| Limit input polarity | `UNCONFIRMED` on all sixteen axes | The switches are installed and their behaviour on the existing hardware is real, but the electrical meaning is established only by reading the Legacy implementation and the Drawing against the installation. Until then the polarity is never assumed active high or active low. |
| Counts → millimetre conversion | `UNRESOLVED`, per axis, `MEASURED_DURING_COMMISSIONING` | No conversion is published. Values are counts and controller units only. |
| Motion parameters (speeds, accelerations, limits) | `NOT_CONFIGURED` / `NOT_COMMISSIONED` | Nothing has been measured or configured in the Product. The Legacy Jog Speed is carried as a display value with status `LEGACY_CANDIDATE_UNVERIFIED`. |
| End stops, software limits, homing offsets | `NOT_COMMISSIONED` / `NOT_VERIFIED` | Not measured; not authorized. |
| Controller library mechanism | `NOT_SELECTED`, `NOT_INSTALLED` | See §9. |

---

## 4. Authorization gate

The gate is **CLOSED**. It is the single authority that decides whether a motion intent may leave the
Application, and in this revision it refuses every intent on every axis of every controller, before
any hardware path is reached.

Gate reason: `MOTION_COMMISSIONING_GATE_NOT_OPEN` — the separate motion commissioning execution gate
(the G2/G3 hardware authorization) is not open.

The gate is not a permissive component with a flag. It has no open method, no override, no
environment switch and no configuration that can open it. Opening belongs to the later execution gate
and to a separate Owner authorization.

### Refusal vocabulary (fixed, ordered, complete)

1. `MOTION_COMMISSIONING_GATE_NOT_OPEN` — the gate reason; always first.
2. `GCLIB_NOT_INSTALLED` — the controller communication library is not installed.
3. `CONTROLLER_ENDPOINT_NOT_CONFIGURED` — no controller endpoint is configured.
4. `CONTROLLER_NOT_CONNECTED` — the controller is not connected (covers communication loss).
5. `MOTION_PARAMETERS_NOT_CONFIGURED` — motion parameters are not configured.
6. `SOFTWARE_LIMITS_NOT_COMMISSIONED` — software limits are not commissioned.
7. `AXIS_NOT_HOMED` — the axis is not homed.
8. `LIMIT_STATE_UNKNOWN` — the limit input state is unknown.
9. `BRAKE_STATE_UNKNOWN` — the brake state is unknown.
10. `HARDWARE_COMMANDS_NOT_AUTHORIZED` — hardware commands are not authorized.

The nine standing preconditions are reported **alongside** the gate reason, never instead of it, so
the Operator sees everything that must change before motion becomes possible.

---

## 5. Motion command model

| Concept | Definition at this revision |
| --- | --- |
| Intent | `JOG`, `MOVE`, `HOME`, `STOP`, `RELEASE_BRAKE`, `RESET_ENCODER`. Six intents; no other intent exists. An intent is a request for evaluation, never a command string. |
| Evaluation | A pure function of the intent and the published state: `Authorized = false`, `CommandState = REFUSED`, `HardwareCommandIssued = false`, `HardwareCommandState = NOT_ISSUED`, plus the ordered refusal list. It reads no device and has no side effect. |
| Parameters | A parameter carries a **display value**, a **unit** (`controller units`), a **status** and an `Executable` flag. A parameter that is not commissioned is display only and is never sent to a controller. |
| Session | `NO_SESSION`. A session is the only context in which hardware execution may later occur; while the gate is closed none can be opened. The command record is session scoped and **never persisted** (`JournalPersistence = NONE`). |
| Serialization rule (future) | At most one motion command in flight per controller. Not enforceable and not enforced in this revision, because no command can be authorized: the invariant exists as a design statement, not as dead machinery. |
| Unknown intent or unknown axis | A programming error, rejected by exception, never reported as a refused command. |

Totals published: evaluated 0, authorized 0, refused 0, hardware commands issued 0, state
`NO_COMMANDS`.

---

## 6. Brakes and limit inputs

* **Brake outputs** are allocated A→1, B→2, C→3, D→4 and are published as logical identities
  (`OUTPUT_1`…`OUTPUT_4`). No brake command has ever been issued from this software
  (`BrakeCommandState = NONE`); the brake state is not read and not inferred (`UNKNOWN`,
  `NOT_AVAILABLE`).
* **Limit inputs** Reverse, Home and Forward are published per axis with `PolarityStatus =
  UNCONFIRMED` and `SignalState = UNKNOWN`. The software never assumes a polarity and never treats an
  unknown input as inactive.
* Hard-limit direction blocking, bounded commissioning speed and the controlled-stop behaviour are
  **design statements with `UNRESOLVED` / `NOT_VERIFIED` status** in this revision. They are not
  implemented, because there is no command path to protect yet; they are listed so that no surface
  can imply they exist.

---

## 7. Home

Homing is defined as a lifecycle vocabulary so the later execution gate has one canonical state set
to implement:

`IDLE → PRECHECK → RELEASE_BRAKE → SEARCH_REVERSE → HOME_LIMIT_DETECTED → CONTROLLED_STOP →
SET_ZERO → VERIFY_ZERO → COMPLETE`

with the failure states `COMMUNICATION_LOSS`, `INVALID_LIMIT_STATE`, `REVERSE_LIMIT_BEFORE_HOME`,
`FORWARD_LIMIT_ACTIVE_AT_START`, `HOME_TIMEOUT`, `STOP_FAILED`, `ZERO_VERIFICATION_FAILED`,
`OPERATOR_STOP`.

No homing routine exists and none is run. Every axis publishes `NOT_HOMED` with status `UNKNOWN`
(the state is not read from any device). An absolute `MOVE` requires a valid Home plus commissioned
software limits; neither exists, so every axis publishes `MoveAuthorizationState = REFUSED` with the
complete ten-item refusal list.

---

## 8. Motion-local protections (not Cleaning-process interlocks)

The following are **motion-local** protections. They protect the axis and the commissioning operator
and are published in their own block. They are **not** the Cleaning-process interlocks, are never
merged with them, and an unverified protection here says nothing about a process interlock:

emergency stop visibility (`NOT_AVAILABLE` here, because nothing is connected), stop-circuit
verification (`NOT_VERIFIED`), controlled-stop capability (`UNRESOLVED`), controlled-stop
verification (`NOT_VERIFIED`), isolation verification (`NOT_VERIFIED` — pump, valve and process
isolation are out of scope for this foundation), local protection status (`NOT_VERIFIED`) and
encoder-reset verification (`NOT_VERIFIED`).

---

## 9. Controller library / integration boundary (NOT selected, NOT installed)

No controller communication library is referenced, installed, pinned or imported anywhere in the
repository at this revision. The mechanism decision remains **OPEN**, and it is a recorded STOP
condition. The boundary a future adapter must respect:

1. The adapter appears only behind the separate execution gate; this software revision never
   constructs it.
2. It is Application-owned and self-owned in its own project, mirroring `adapters/tc-acquisition`
   (its own project file, central package management pin, no Contracts/Domain reference required).
3. It exposes intents, never a raw command surface: no UI, no route and no configuration may carry a
   controller command string.
4. Its endpoint is Owner-local and confidential: the only endpoint statement any committed layer may
   carry is the redacted identity `ENDPOINT_NOT_CONFIGURED` / `ENDPOINT_CONFIGURED`. No address, no
   host name and no port value may enter a contract, a fixture, a log line, a UI state or a document.
5. Licence, version and native-architecture consequences are reviewed before selection, and the
   selection is a separate Owner decision with its own gate.

---

## 10. Where the software lives

| Path | Role |
| --- | --- |
| `packages/contracts/MotionCommissioning.cs` | The wire contract: the motion commissioning projection and every status/refusal vocabulary. No behaviour. |
| `packages/application/Motion/MotionTopologyCatalog.cs` | The Owner-confirmed topology and the one open mapping conflict. Data only. |
| `packages/application/Motion/MotionCommandGate.cs` | `MotionAuthorizationGate` (CLOSED), the intent and evaluation records, and the evaluation function. |
| `packages/application/Motion/MotionCommissioningSession.cs` | The session: `NO_SESSION`, refused open, refused Stop-all. |
| `packages/application/Motion/MotionCommissioningProjector.cs` | Composes the projection, its canonical text and its fingerprint. Pure; reads nothing. |
| `packages/contracts/wjss-contracts-ts/{types.ts,validate.mjs}` | The TypeScript mirror and the structural validator (the C# records are authoritative). |
| `packages/commissioning/src/motionCommissioning.mjs` | The read-only **Preview** projection of the same confirmed design: data and wording only, no gate logic and no device access. |
| `apps/ops-preview` | The Engineering Motion Commissioning page: a new read-only section and one GET route. |
| Tests | `tests/runtime.tests/Motion/MotionCommissioningTests.cs`, `packages/contracts/wjss-contracts-ts/test/motion.validate.test.mjs`, `packages/commissioning/test/motionCommissioning.test.mjs`, `apps/ops-preview/test/motionCommissioning.{render,server}.test.mjs`. |

The projection is published in the Runtime **Snapshot only**. It is never carried by a Delta: the
Runtime Delta has no motion member, and the contract validator refuses a Delta that contains one.

---

## 11. Publication and the Public-Repository Boundary

* The Runtime composes the projection at initial composition and publishes it from revision 1. No
  commit path in this revision changes it.
* The published projection contains no network address, no port value, no host name and no
  vendor-specific command string. Controllers are named by the WSB cabinet that holds them.
* The Preview route reads the Runtime Snapshot **on demand only** (the section's Refresh control);
  the design projection never waits on a Runtime host. An absent or malformed Runtime answer is an
  explicit state (`NOT_READ`, `NOT_PUBLISHED`, `RUNTIME_UNAVAILABLE`, `INVALID_RUNTIME_RESPONSE`),
  never a substituted value.
* Boundary scan S1–S9: clean (0 findings) at this revision.

---

## 12. Exclusions (not in this revision)

Controller connection or read; controller library installation or pinning; endpoint configuration;
NIC changes; Jog; Move; Home; brake output; encoder reset; ADAM access; TC acquisition work;
Cleaning, pump and valve work; sensor coordinate integration; coordinated motion; AutoSequence;
Production; a second Runtime or a second command authority; a browser-owned device session; a raw
command surface in any UI; persistence of any motion command record; scope creep into any of the
above.

---

## 13. STOP conditions (stop and request an Owner decision)

1. Any mapping conflict not already recorded (axis, Water Jet, cabinet or controller).
2. Any polarity conflict, or a polarity that is confirmed for some axes and not others.
3. Any brake-output ambiguity (allocation, polarity, or an output shared between axes).
4. Any counts-to-millimetre conversion conflict, or a conversion that is not per axis.
5. A controller library version that cannot be selected without a licence or public-repository
   impact.
6. Any native-architecture conflict (x64/arm64) for a controller library.
7. A real endpoint, host name or port entering Git, logs, fixtures, contracts or documentation.
8. Application ownership moving to the browser or the UI.
9. A second Runtime or a second command authority.
10. An absolute Move before a valid Home and commissioned software limits.
11. Any dependency on Cleaning, pump or valve state.
12. Scope creep into coordinated motion, sensor coordinates, AutoSequence or Production.
13. Any hardware action before the separate G2/G3 authorization.
14. Any change that departs from the agreed near-term sequence: Galil Manual Motion Commissioning →
    TC Commissioning → Motion + Sensor Coordinate Integration → Cleaning Logic Integration.
15. Any attempt to promote a Legacy value into canonical Product configuration.

---

## 14. Validation performed and required

**Performed in Arena (this revision):** `node --check` on every new and edited JavaScript file;
commissioning package tests 238/238; contracts-TS 54/54; ops-preview 170/170; kiosk 22/22;
mapping-config 82/82 and 261/261 (7 skipped, pre-existing); tc-bench 19/19; boundary-scan tests
33/33; boundary scan S1–S9 **0 findings**.

**NOT RUN in Arena:** the whole .NET side. No SDK is available in the sandbox, so the C# build, the
C# unit tests and the fixture-parity gate are **NOT RUN** and no C# compile or test result is claimed.

**Required Owner-local validation (the validation of record):**

```bash
dotnet build WaterJetSentinelSuite.sln -c Release
dotnet test WaterJetSentinelSuite.sln -c Release
WJSS_UPDATE_FIXTURES=1 dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj
node tools/boundary-scan/boundary-scan.mjs .
```

The golden fixture `packages/contracts/fixtures/snapshot.seed0.json` does not yet carry the `motion`
key, because the committed file is generated by the .NET generator. The Node port
(`tools/fixture-generator/generate.mjs`) was updated to emit `"motion": null`, matching the C#
serialization exactly, so the Owner-local `WJSS_UPDATE_FIXTURES=1` run produces the expected single
addition and no other diff.

---

## 15. Changed / Unchanged / Not Verified

**Changed:** new motion contract (`packages/contracts/MotionCommissioning.cs`); new Application
model (`packages/application/Motion/*`); `RuntimeState`, `OperationalSnapshot` and the Snapshot
projector carry the nullable motion section; the initial composition publishes it; the TypeScript
mirror, the contract validator and the Preview (package module, server route, page section and
styles) were extended; new focused tests in four suites; the fixture generator emits the new member;
one existing navigation-count assertion updated from seven sections to eight.

**Unchanged:** the Product authority path (single-writer Runtime State Store, atomic publication,
Snapshot/Delta semantics); the Stage identity `0.4C-K2` and the Preview boundary; the TC acquisition
contract and evidence positions; the protected topology baseline (108 Sensors / 216 channels); the
loopback-only bind host; the Delta contract (motion is refused there); every refusal reason, status
and boundary rule that already existed; no lock file, no project reference, no central package pin
and no dependency of any kind was added.

**Not Verified:** everything about hardware. No controller was contacted, read or configured; no
controller library was installed or selected; no axis was homed; no brake was released; no encoder
was reset; no motion command was issued; no endpoint was configured; no limit polarity was measured;
no unit conversion was measured; no Legacy value was promoted to configuration; no Owner decision in
§3 was taken; the WJ2 / WJ4 conflict remains open; the .NET build and tests were NOT RUN in Arena.
