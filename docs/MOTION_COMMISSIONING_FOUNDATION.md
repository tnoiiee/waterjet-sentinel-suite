# Manual Motion Commissioning Foundation — Software Design Record

**Status: DEVELOPMENT CHECKPOINT RECORD. NOT Owner-reviewed. NOT merged. No hardware action has
been taken and none is authorized by this document.**

> **Owner-approved canonical Equipment topology adoption supersession (2026-10-25).** The Water
> Jet → WSB cabinet → axis-pair allocation, the §2 table, the §3 evidence-note wording and the
> motion topology revision identity in this record are superseded for current authority by the
> Owner-approved canonical Equipment topology adoption: current identity
> `OWNER_CANONICAL_TOPOLOGY_2026_10_25` / `MOTION_COMMISSIONING_DESIGN_2026_10_25` with
> WJ1 = A,B (WSB1), WJ2 = C,D (WSB1), WJ3 = C,D (WSB2), WJ4 = A,B (WSB2), WJ5 = A,B (WSB3),
> WJ6 = C,D (WSB3), WJ7 = A,B (WSB4), WJ8 = C,D (WSB4). `OWNER_CONFIRMED_REV2` /
> `OWNER_CONFIRMED_REV1` and `MOTION_COMMISSIONING_DESIGN_REV2` /
> `MOTION_COMMISSIONING_DESIGN_REV1` remain the historical identities of the earlier
> allocations and of this record's dated §15 history; the dated record below is preserved, and
> the current-facing §2 / §3 statements are corrected in place. The adoption changes no gate,
> refusal, axis-role (A/C Horizontal, B/D Vertical) or brake-output (A→1, B→2, C→3, D→4) fact,
> and authorizes no motion beyond the existing scope.

> **Canonical Topology Adoption supersession (2026-10-21; superseded for current authority by
> the Owner-approved canonical Equipment topology adoption of 2026-10-25 above; retained as
> history — HISTORICAL / SUPERSEDED: DO NOT USE FOR CURRENT RUNTIME, PROFILE, COMMISSIONING, OR
> HARDWARE).** The Water Jet → WSB cabinet →
> axis-pair allocation, the §2 table, the §3 evidence-note wording and the motion topology
> revision identity in this record are superseded for current authority by the Owner
> Canonical Topology Adoption: current identity `OWNER_CONFIRMED_REV2` /
> `MOTION_COMMISSIONING_DESIGN_REV2` with WJ1 = A,B (WSB2), WJ2 = A,B (WSB1), WJ3 = C,D (WSB1),
> WJ4 = C,D (WSB2), WJ5 = A,B (WSB3), WJ6 = A,B (WSB4), WJ7 = C,D (WSB3), WJ8 = C,D (WSB4).
> `OWNER_CONFIRMED_REV1` / `MOTION_COMMISSIONING_DESIGN_REV1` remain the historical identity
> of the earlier allocation and of this record's dated §15 history; the dated record below
> is preserved, and the current-facing §2 / §3 statements are corrected in place. The
> adoption changes no gate, refusal, axis-role (A/C Horizontal, B/D Vertical) or brake-output
> (A→1, B→2, C→3, D→4) fact, and authorizes no motion, no C/D Manual Motion and no
> output 3/4 write.

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
* the read-only publication of all of the above as a **static catalogue** on the dedicated read-only
  catalogue route, and in the Preview Engineering page.

It does **not** contain a controller library, a connection, an endpoint, a driver, a command
encoder, a homing routine, a jog routine, a brake write, an encoder reset, or any device access of
any kind. There is no code path in this revision that can produce a controller command string.

---

## 2. Owner-confirmed data (encoded as data, never derived)

| Fact | Value | Basis |
| --- | --- | --- |
| Axis role | A and C are the **Horizontal Axis**; B and D are the **Vertical Axis** | Owner-confirmed |
| Brake output | axis A → output 1, B → output 2, C → output 3, D → output 4 | Owner-confirmed |
| WJ1 (WSB1) | A, B | Owner-confirmed (current canonical topology, 2026-10-25) |
| WJ2 (WSB1) | C, D | **Canonical (current)** — Owner-confirmed; the Drawing rev1 discrepancy recorded against the superseded REV1 row is evidence only (§3) |
| WJ3 (WSB2) | C, D | Owner-confirmed (current canonical topology, 2026-10-25) |
| WJ4 (WSB2) | A, B | **Canonical (current)** — Owner-confirmed; the Drawing rev1 discrepancy recorded against the superseded REV1 row is evidence only (§3) |
| WJ5 (WSB3) | A, B | Owner-confirmed (current canonical topology, 2026-10-25) |
| WJ6 (WSB3) | C, D | Owner-confirmed (current canonical topology, 2026-10-25) |
| WJ7 (WSB4) | A, B | Owner-confirmed (current canonical topology, 2026-10-25) |
| WJ8 (WSB4) | C, D | Owner-confirmed (canonical under every recorded allocation; unchanged) |
| Controller model | DMC-B140-M (four axes) | Owner-provided WSB cabinet drawing |
| Controllers | four, one per WSB cabinet | **Inferred** — see §3 |
| Position sense | axis A: encoder; axes B, C, D: installed switch set | Owner-confirmed |
| Limit inputs | Reverse, Home and Forward on every axis (three inputs) | Owner-confirmed |
| Initial Jog Speed | 10, **controller units**, Legacy candidate, unverified, display only | Owner-provided; never executable |

The Operator-facing axis wording is **"Horizontal Axis"** and **"Vertical Axis"** only. No other
axis wording (X/Y, left/right, up/down, cannon axis numbering) exists anywhere in the software. The
physical controller axis letter (A/B/C/D) may appear **only as diagnostic identity beside the role**
("Horizontal Axis · C"), never instead of it.

The eight Water Jet rows are **explicit data, not a rule**. The mapping is not derived from a parity
rule, from the Water Jet ordinal, or from the cabinet: among the even Water Jets WJ2 (C,D) and
WJ4 (A,B) take different axis pairs, among the odd Water Jets WJ1 (A,B) and WJ3 (C,D) take
different axis pairs while WJ5 (A,B) and WJ7 (A,B) agree where any alternating rule would
disagree, and the two Water Jets of every WSB cabinet take different axis pairs. No
odd/even formula and no cabinet-derived formula reproduces this table, so none is implemented;
tests assert the exact eight rows instead.

---

## 3. Inferences and unresolved items (explicit, never hidden)

| Item | Status | Why it is not confirmed |
| --- | --- | --- |
| One controller per WSB cabinet | `INFERRED_FROM_OWNER_CONFIRMED_MAPPING` | It follows from the confirmed Water Jet allocation (each WSB drives exactly one A,B pair and one C,D pair), but the Owner has not separately confirmed the controller count per cabinet. |
| WJ2 / WJ4 Drawing rev1 discrepancy | `DRAWING_REV1_CONFLICT_RECORDED`, `EVIDENCE_ONLY_NOT_CANONICAL`, `NOT_PHYSICALLY_RESOLVED` | Historical note against the superseded REV1 allocation: under `OWNER_CONFIRMED_REV1`, WJ2 = C,D on WSB1 and WJ4 = A,B on WSB2, while the Drawing rev1 expectation for these two Water Jets was the other axis pair on the same cabinet. The current canonical allocation is `OWNER_CANONICAL_TOPOLOGY_2026_10_25` (WJ2 = C,D on WSB1; WJ4 = A,B on WSB2 — the same axis-pair placement as the superseded REV1 allocation), so this remains **not** an open decision and **not** ambiguity: it is recorded historical evidence. The note is published with the catalogue, never suppressed, and it changes no mapping: all eight rows publish `CONFIRMED_OWNER` with `OWNER_CONFIRMED` provenance. The physical installation has **NOT** been resolved, and nothing in the REV2 adoption or the current canonical allocation asserts which allocation the installed wiring matches. |
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
| `packages/contracts/MotionCommissioning.cs` | The wire contract: the motion commissioning catalogue and every status/refusal vocabulary. No behaviour. |
| `packages/application/Motion/MotionTopologyCatalog.cs` | The Owner-confirmed topology (canonical, explicit data) and the one recorded Drawing evidence note. Data only. |
| `packages/application/Motion/MotionCommandGate.cs` | `MotionAuthorizationGate` (CLOSED), the intent and evaluation records, and the evaluation function. |
| `packages/application/Motion/MotionCommissioningSession.cs` | The session: `NO_SESSION`, refused open, refused Stop-all. |
| `packages/application/Motion/MotionCommissioningProjector.cs` | Composes the static catalogue, its canonical text and its fingerprint. Pure; reads nothing. |
| `packages/contracts/wjss-contracts-ts/{types.ts,validate.mjs}` | The TypeScript mirror and the structural validator (the C# records are authoritative). |
| `packages/commissioning/src/motionCommissioning.mjs` | The read-only **Preview** catalogue of the same confirmed design: data and wording only, no gate logic and no device access. |
| `apps/ops-preview` | The Engineering Motion Commissioning page: a read-only section and the dedicated read-only GET route `/api/motion-commissioning/catalogue`. |
| Tests | `tests/runtime.tests/Motion/MotionCommissioningTests.cs`, `packages/contracts/wjss-contracts-ts/test/motion.validate.test.mjs`, `packages/commissioning/test/motionCommissioning.test.mjs`, `apps/ops-preview/test/motionCommissioning.{render,server}.test.mjs`. |

### The catalogue is static: it is not a Snapshot member and not a Delta member

The catalogue is ~40 KB of content that does **not** change with a revision. Carrying it inside every
Runtime Snapshot would re-send it on every revision (in the committed fixture it would be roughly a
third of the Snapshot payload) for content that never changes and that contains no observed state. It
is therefore served by its own dedicated read-only route, and the Snapshot stays reserved for the
**dynamic observed motion state** the Product will publish later:

* controller connection state; axis state; command and encoder position; moving / stopped;
* Reverse / Home / Forward input state; brake commanded and brake observed;
* active command lifecycle; fault and refusal; timestamps; quality.

**None of that dynamic state is authorized in this revision**, so the Snapshot carries no motion
member at all — not even a null placeholder. The contract validator refuses a `motion` member in a
Snapshot **and** in a Delta, so the separation is enforced by the contract, not only by a comment:

* `GET /api/motion-commissioning/catalogue` — the static catalogue. It performs **no Runtime read**,
  takes no query parameter and carries no dynamic state. The payload states its own placement
  (`static: true`, `carriedBySnapshot: false`, `carriedByDelta: false`, `runtimeReadRequired: false`).
* Snapshot / Delta — **no** `motion` member. `validate.mjs` refuses one in either envelope, and a
  test asserts that the committed fixture carries none.

There is one canonical source for the mapping: the Owner-confirmed table above, encoded once per
language (C# `MotionTopologyCatalog`, JS `MOTION_WATER_JET_BINDINGS`) and cross-checked by tests in
both. The Preview route serves the JS catalogue; a Product-side consumer would read the C# catalogue
through a Product route. No third table exists.

---

## 11. Publication and the Public-Repository Boundary

* The Application model composes the catalogue from Owner-confirmed data. It depends on no Runtime
  host, no revision and no device, and no commit path in this revision changes it.
* The published catalogue contains no network address, no port value, no host name and no
  vendor-specific command string. Controllers are named by the WSB cabinet that holds them.
* The Preview catalogue route reads **no Runtime at all**, and the section has no Refresh control: a
  static catalogue has nothing to refresh. The page therefore never couples its boot to a second
  host, and no Runtime state — published, absent or malformed — can be substituted for the catalogue.
* The served UI carries no vendor vocabulary: the physical controller axis letter is printed as
  diagnostic identity (A/B/C/D), never with a vendor name.
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

1. Any mapping conflict not already recorded (axis, Water Jet, cabinet or controller) — including a
   new Drawing discrepancy, or any attempt to treat a recorded evidence note as ambiguity or as an
   open decision again.
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
commissioning package tests 239/239; contracts-TS 55/55; ops-preview 171/171; kiosk 22/22;
mapping-config 82/82 and 261/261 (7 skipped, pre-existing); boundary-scan tests 33/33; boundary scan
S1–S9 **0 findings**.

**Snapshot static-payload evidence (measured):** the committed golden fixture
`packages/contracts/fixtures/snapshot.seed0.json` is **122,110 bytes** and carries **no** `motion`
key; the static catalogue is **32,163 bytes** as served by the Preview route (41,003 bytes for the
C#-shaped mirror the contract tests use). The separation removes that payload from every Snapshot
revision and from every Delta.

**NOT RUN in Arena:** the whole .NET side. No SDK is available in the sandbox, so the C# build, the
C# unit tests and the fixture-parity gate are **NOT RUN** and no C# compile or test result is claimed.

**Required Owner-local validation (the validation of record):**

```bash
dotnet build WaterJetSentinelSuite.sln -c Release
dotnet test WaterJetSentinelSuite.sln -c Release
WJSS_UPDATE_FIXTURES=1 dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj
node tools/boundary-scan/boundary-scan.mjs .
```

The golden fixture `packages/contracts/fixtures/snapshot.seed0.json` carries **no** `motion` key, and
**no fixture regeneration is required** for this correction: the Snapshot schema returns to the
baseline it had before the motion member was added, and the committed fixture was generated from that
baseline. The Node port (`tools/fixture-generator/generate.mjs`) had been emitting `"motion": null`
to match the C# serialization of the nullable member; that line is now removed, because a Snapshot
with a `motion` member — null included — is refused by the contract validator. The remaining
C#/Node generator divergence for `tcAcquisition` is **pre-existing** and is untouched here.

---

## 15. Changed / Unchanged / Not Verified

**Changed:** new motion contract (`packages/contracts/MotionCommissioning.cs`); new Application
model (`packages/application/Motion/*`) whose catalogue is **static**; the TypeScript mirror, the
contract validator and the Preview (package module, the dedicated catalogue route, page section and
wording) were extended; new focused tests in four suites; the Node fixture generator no longer emits a
`motion` member; one existing navigation-count assertion updated from seven sections to eight.

**Changed by the focused correction:** the canonical WJ2 / WJ4 mapping is now `CONFIRMED_OWNER` with
`OWNER_CONFIRMED` provenance and the Drawing rev1 discrepancy is carried as an **evidence note**
(`DRAWING_REV1_CONFLICT_RECORDED`, `EVIDENCE_ONLY_NOT_CANONICAL`, `NOT_PHYSICALLY_RESOLVED`) instead
of an open decision; `OWNER_DECISION_REQUIRED` no longer exists in the vocabulary; the static
catalogue was **removed from the Runtime Snapshot** (contract, Runtime state, Snapshot projector and
initial composition) and is served by the dedicated read-only route
`GET /api/motion-commissioning/catalogue`; the Snapshot-coupled Preview route and the section's
Refresh control were removed; the physical controller axis letter is rendered only as diagnostic
identity beside the role.

**Unchanged:** the Product authority path (single-writer Runtime State Store, atomic publication,
Snapshot/Delta semantics — the Snapshot simply has no motion member); the Stage identity `0.4C-K2`
and the Preview boundary; the TC acquisition contract and evidence positions; the protected topology
baseline (108 Sensors / 216 channels); the loopback-only bind host; the closed authorization gate and
every refusal reason, status and boundary rule that already existed; no controller contact, no
endpoint, no command route, no gclib and no hardware capability; no lock file, no project reference,
no central package pin and no dependency of any kind was added.

**Not Verified:** everything about hardware. No controller was contacted, read or configured; no
controller library was installed or selected; no axis was homed; no brake was released; no encoder
was reset; no motion command was issued; no endpoint was configured; no limit polarity was measured;
no unit conversion was measured; no Legacy value was promoted to configuration; no Owner decision in
§3 was taken; the WJ2 / WJ4 Drawing discrepancy remains **not physically resolved** (recorded as
evidence only, never claimed closed); the .NET build and tests were NOT RUN in Arena.
