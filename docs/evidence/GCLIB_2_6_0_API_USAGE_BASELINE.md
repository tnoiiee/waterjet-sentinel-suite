# gclib 2.6.0 API Usage Baseline (Vendor-Derived, Redacted)

> **VENDOR-DERIVED API REFERENCE**
> **NOT A REPLACEMENT FOR VENDOR DOCUMENTATION**
> **NOT ROADMAP AUTHORITY**
> **NOT EXECUTABLE AUTHORITY BY ITSELF**
> **NOT HARDWARE AUTHORIZATION**
> **NOT MOTION AUTHORIZATION**
> **NOT OUTPUT-CONTROL AUTHORIZATION**

**Document class:** EVIDENCE ONLY. This is the canonical, redacted baseline of what the installed
vendor communication library (version 2.6.0) documents about the application programming interface
the WJSS motion-library adapter uses, and of what one Owner-local identity contact and one
Owner-local read-only observation trial actually returned. It authorizes no software change, no
configuration, no connection and no command. It competes with no planning document.

**Checkpoint.** PR #31 — B2 read-only observed-state foundation, closeout. Companion evidence:
`docs/evidence/LEGACY_MOTION_COMMISSIONING_BASELINE.md` (Legacy source behaviour) and
`docs/MOTION_OBSERVED_STATE_FOUNDATION.md` (the B2 model).

**Gate position.** The Motion authorization gate is CLOSED
(`MOTION_COMMISSIONING_GATE_NOT_OPEN`). Controller endpoint configuration is `NOT_CONFIGURED`; the
axes are `NOT_COMMISSIONED`; the brake state is unknown; no Home exists. Planned is not authorized,
and observation is not control. Nothing in this document changes any of that.

**Hardware position.** The Agent contacted no controller, loaded no library, opened no handle, read
no axis, sent no command, changed no NIC setting and started no vendor service while producing this
document. The two hardware records in sections H and I are **Owner-reported** results, reproduced
here in redacted form; the Agent did not run them and did not repeat them.

**Handling.** The vendor document was retrieved once through the connected Google Drive connector,
read-only, by exact filename. It was held outside the repository for analysis and is not committed
and not excerpted: every statement below is a paraphrase of a named entry, or a declaration
signature that the adapter already has to match. The temporary copy was deleted after extraction.

---

## A. Authority and provenance

### A.1 Source identity

| Item | Value |
| --- | --- |
| Exact filename | `gclib.pdf` |
| Exact-name matches found | 1 (zero or more than one would have been a STOP) |
| Document title (metadata) | `gclib` |
| Document subject (metadata) | `Galil Communications Library` |
| Documented library version | 2.6.0 |
| Pages | 355 |
| Generator | `Doxygen 1.14.0` |
| Retrieval | Google Drive connector, read-only, one file only |
| Other Drive files opened or inspected | none |
| Committed to this repository | no |
| Temporary local copy | created outside the repository, read-only, deleted after extraction |
| Reproduction in this file | paraphrases, declaration signatures, entry names and printed page numbers only |

The manual is the vendor's own reference for the installed library generation. It is evidence of
**what the vendor documents**, not evidence of **what this machine does**. Sections H and I carry
the only machine evidence in this document, and that evidence is Owner-reported.

### A.2 Page citation convention

Every citation below is a **printed page number** — the number printed in the page header or footer
of the vendor document. The printed page number equals the physical page index minus 34. This file
reproduces no dotted section numeral (the repository boundary scanner reads a four-group numeral as
an address), so sections are cited by **title and printed page** instead. That is deliberate and is
one of the properties the document-boundary tests pin.

### A.3 Authority order used to settle every question in this document

1. the Owner's instruction for this closeout;
2. the approved scope;
3. the exact committed source at the starting head;
4. `docs/MASTER_PLAN.md` (roadmap of record);
5. committed WJSS evidence — this file and the Legacy baseline;
6. the vendor document (`gclib.pdf`, 2.6.0);
7. the installed header and examples;
8. Legacy source behaviour.

Later items never overrule earlier ones. Where the vendor document is silent, this baseline records
`NOT_REVIEWED` or a gap rather than a conclusion.

### A.4 What this baseline can and cannot prove

It **can** prove: which interface entries exist in version 2.6.0; what their documented parameters,
returns and stated warnings are; that three distinct interface layers exist and are documented
separately; that the legacy option switches belong to the deprecated legacy entry and not to the
current one; and what one identity contact and one read-only trial returned on the Owner
workstation.

It **cannot** prove: any bit-level meaning, polarity, wiring, unit, tolerance, timing or physical
outcome; the exact response format of the installed firmware for any controller command; that a
documented entry behaves identically on this machine; or that a candidate command is safe. Those
statement classes are absent from this document by design.

---

## B. Three layers, never conflated

The vendor documentation describes three interfaces that are routinely confused. They are recorded
separately here, and the WJSS boundary uses only the first and a strictly limited part of the third.

| Layer | Name | What it is | Documented at | WJSS use |
| --- | --- | --- | --- | --- |
| L1 | Current C API (`gclib_*`) | The supported application programming interface of version 2.6.0 | printed pages 41-53 (C topic documentation) | Used by the B1 identity surface and by the B2 read boundary |
| L2 | Deprecated legacy C API (`G*`, including `GOpen`) | The earlier interface, explicitly marked for removal in a future release | Deprecated List, printed pages 27-28; entry documentation, printed pages 75-76 | **Not used anywhere.** `DEPRECATED_NOT_ALLOWED` |
| L3 | Controller command language (`MG`, `TP`, `RP`, and every other controller mnemonic) | Text interpreted by the controller itself, carried as an opaque string through a library call | **Not documented in this manual.** Only example invocations appear (printed pages 5-6); the manual defers the semantics elsewhere (see B.3) | A closed, literal, read-only subset only (B2), through one scoped export |

### B.1 Layer 1 — the current C API

The current interface is documented as one topic tree: connection management and controller
operations, each entry with its declaration, parameters, returns and, where relevant, a warning.
The connection example passes an address to the open entry (printed pages 4-5), and the manual
states that all gclib functions return a result value with zero indicating success (printed page 6).

Two properties of this layer matter for the boundary:

- the open entry's second parameter is a **controller address** (printed page 48), not an option
  string;
- process-wide behaviour such as the gcaps/direct selection is set by its own entry
  (`gclib_force_gcaps`, printed page 48) and is stated to be irreversible, not by an argument to the
  open entry.

### B.2 Layer 2 — the deprecated legacy C API

The Deprecated List (printed pages 27-28) marks the whole earlier family — address listing, open,
close, info, command, error, program/array, interrupt, message, motion-complete and others — as
"part of the deprecated C (Legacy) API, which will be removed in a future release". Each row names
its replacement; the open row names the current open entry.

The legacy entry documentation (printed pages 75-76) is the **only** option-switch table in the
manual: an address switch, a baud switch, a direct-connection switch, a subscribe switch, a timeout
switch, and further switches the manual states are ignored as of version 2.4.0 and kept only for
backward compatibility. A lone token with no switch is the address.

No part of WJSS uses this layer. It is recorded here because its syntax is the source of the
misuse in section D, and because a future request to use it must be refused on evidence, not on
taste.

### B.3 Layer 3 — the controller command language

The manual documents the transport of a command, not the command language:

- the commands example (printed pages 5-6) shows a terminal that passes typed text straight to the
  command entry and prints the response, and a program upload built from the same text;
- the command entry itself (printed page 53) documents the handle, the command text, the response
  buffer and the buffer length, states that the text is terminated with a carriage return, and
  states that a single command cannot be longer than 80 characters;
- where a value needs bit-level explanation, the manual **defers to the controller's own command
  reference** rather than repeating it: the motor-stop reason descriptions point at the controller's
  `SC` command reference (printed pages 59 and 61), and the interrupt status byte description points
  at the controller's `EI` command reference (printed pages 117 and 118).

There is no command reference in `gclib.pdf`: no entry defines `MG`, `TP`, `RP` or any other
controller mnemonic, no entry defines a status-word bit table, and no entry defines a limit, home,
brake or motion-completion semantic. Consequently:

- **every** controller-command semantic in WJSS is vendor-evidenced from a *different* document, or
  it is not evidenced at all and stays `NOT_REVIEWED` / `BLOCKED_PENDING_VENDOR_EVIDENCE`;
- this baseline records **no** controller-command semantics of its own. The B2 rows in section F.3
  name the request literals the adapter already issues and the interpretation limits already
  published in `docs/MOTION_OBSERVED_STATE_FOUNDATION.md`; nothing stronger is claimed here.

### B.4 Conflation rules

1. An option switch in the legacy open string is **never** evidence about the current open entry.
2. A process-wide setting (direct/no-gcaps) is **never** expressed as an argument to the open
   entry, and is **never** treated as a per-connection preference.
3. A controller mnemonic's meaning is **never** inferred from its name, from a Legacy comment, from
   a chat memory, or from its appearance in a vendor example.
4. A library return value is **never** presented as a controller response, and a controller response
   is **never** presented as a library result. They are different layers and different vocabularies.

### B.5 How the three layers map onto WJSS

| WJSS surface | Layer 1 | Layer 2 | Layer 3 |
| --- | --- | --- | --- |
| Gate A native probe | one version entry only | none | none |
| B1 identity session | force-direct, open, close, connection type, revision, serial | none | none |
| B2 read boundary | one command-and-response entry, resolved once | none | sixteen literal read-only requests |
| C1 application contract | not implemented | not implemented | not implemented |

---

## C. Verified contracts

"Verified" here means **the contract in the vendor document was read and matched against the
committed boundary** — not that the call was executed on this machine, and not that the vendor
behaviour is proven here. The eight contracts named by the closeout are listed first; three
supporting entries follow.

| # | Entry | Declared shape (as committed in the adapter) | Documented contract | Printed page | WJSS status |
| --- | --- | --- | --- | --- | --- |
| 1 | `gclib_force_gcaps` | `void gclib_force_gcaps(bool on)` | Forces future library calls to gcaps or to direct operation; the default prefers gcaps with a direct fallback; the selection is process-wide and **cannot be undone**; `on = false` stops gcaps from being used for any future call | 48 | `VERIFIED_FOR_WJSS` |
| 2 | `gclib_open` | `gclib_result gclib_open(gclib_handle *h, const char *address)` | Opens a connection, through gcaps if available and otherwise directly; on a serial connection the baud rate defaults to 115200 and must be set by the baud entry before communicating; the second parameter is the **address of the controller, obtained from the address listing** | 48 | `VERIFIED_FOR_WJSS` |
| 3 | `gclib_close` | `gclib_result gclib_close(gclib_handle *h)` | Closes the connection; the handle **is set to NULL on success**; an invalid handle is reported as an invalid argument | 49 | `VERIFIED_FOR_WJSS` |
| 4 | `gclib_address` | `gclib_result gclib_address(gclib_handle h, char *address, size_t len)` | Returns the connection address into a caller-supplied buffer; a buffer that is too small is reported as such | 49-50 | `REVIEWED_NOT_USED` |
| 5 | `gclib_revision_information` | `gclib_result gclib_revision_information(gclib_handle h, char *rev_info, size_t len)` | Returns the controller revision information into a bounded buffer; a NULL handle is an invalid argument, a closed connection is not-connected, an undersized buffer is buffer-too-small | 52 | `VERIFIED_FOR_WJSS` |
| 6 | `gclib_serial_number` | `gclib_result gclib_serial_number(gclib_handle h, uint32_t *serial_number)` | Returns the controller serial number, or zero on failure; a retrieval failure is an internal error | 52-53 | `VERIFIED_FOR_WJSS` |
| 7 | `gclib_command` | `gclib_result gclib_command(gclib_handle h, const char *command, char *buf, size_t len)` | Sends one command to the controller and returns its response; the text is terminated with a carriage return; one command cannot exceed 80 characters; a missing response is a timeout, a controller error reply is a command error, an undersized buffer is buffer-too-small | 53 | `VERIFIED_FOR_WJSS` (read-only literal subset only) |
| 8 | `gclib_error` | `const char *gclib_error(gclib_handle h)` | Returns the library error string; a NULL handle returns the last error of a call that takes no handle; the manual warns that the returned string is an internal string that **may change between releases** | 49 | `REVIEWED_NOT_USED` |
| — | `gclib_connection_type` | `gclib_result gclib_connection_type(gclib_handle h, gclib_connection_t *type)` | Returns the connection type of an open handle; a handle that does not refer to an open connection is an invalid argument | 50 | `VERIFIED_FOR_WJSS` |
| — | `gclib_addresses` | `gclib_result gclib_addresses(char *addresses, size_t len)` | Lists the available connection addresses; the discovery example uses it and the address-assignment flow, neither of which is used by WJSS | 47 (entry); 3-4 (example) | `REVIEWED_NOT_USED` |
| — | `gclib_set_baud_rate` | `gclib_result gclib_set_baud_rate(gclib_handle h, size_t baud_rate)` | Sets the baud rate of a serial connection after opening; WJSS connects over Ethernet, so this entry is never called | 49 | `REVIEWED_NOT_USED` |

### C.1 Notes that change how a boundary must be written

- **The force-direct entry cannot be undone.** It is therefore called exactly once, before the
  single open, by the process that owns the connection. No test, retry or second session may call it
  again in the same process.
- **The close entry zeroes the handle on success.** A boundary can therefore prove the close by
  checking both the returned result *and* the handle becoming zero; WJSS does exactly that and
  treats anything else as unproven cleanup.
- **The open entry's address is data, not syntax.** It is described as a value obtained from the
  address listing. It is not a command string and not an option list.
- **The command entry is general.** Nothing in its declaration restricts it to reads. The WJSS
  boundary is safe because the *table* is closed and literal — not because the vendor entry is
  narrow. That asymmetry is why the B2 source-boundary exception is scoped to one file and one
  token. The entry's name appears in this evidence record and in exactly one Product source file
  (the internal boundary); every other Product file is scanned and must stay clean.
- **The error entry is deliberately unused.** It returns a string the manual says may change
  between releases. WJSS maps results to its own canonical words instead, so no vendor text can
  reach an outcome, a log line or a test.
- **The revision and serial entries are identity reads, not axis reads.** They answer about the
  connection, not about motion state, and they do not observe the machine's condition.
- **`gclib_address` is not called.** The adapter already holds the validated endpoint it opened
  with; reading it back would add a call without adding evidence.

---

## D. The `gclib_open` misuse-regression rule

### D.1 The rule

> The value passed to the current open entry is the **validated controller address and nothing
> else**. It carries no option switch, no `--` token, no timeout value, no baud value, no
> `--direct`, no `--subscribe`, no quoted text, no trailing whitespace, no interpolation and no
> suffix of any kind. Direct/no-gcaps operation is selected once, process-wide, by the force-direct
> entry **before** the open. A serial baud rate is set by the baud entry **after** the open. Any
> change that needs switch syntax is a change to a deprecated layer and is refused
> (`DEPRECATED_NOT_ALLOWED`).

### D.2 What the vendor document actually says

| Question | Answer | Printed page |
| --- | --- | --- |
| What is the second parameter of the current open entry? | The controller address, obtained from the address listing | 48 |
| Where is an address obtained? | From the address listing, which prints one address per detected controller | 3-4 |
| Where are the option switches documented? | Only in the deprecated legacy open entry's parameter table | 75-76 |
| Which switches exist there? | Address, baud, direct, subscribe, timeout; further switches are stated to be ignored as of version 2.4.0 | 76 |
| What does a lone token mean there? | The address, with the address switch implicit | 76 |
| What does the current open entry return when the connection cannot be opened? | Not-connected | 49 |
| How is direct/no-gcaps selected in the current interface? | By the process-wide force-direct entry, which cannot be undone | 48 |

### D.3 Owner-local A/B observation (2026-10-17)

The Owner ran a minimal native diagnostic against the exact installed header, import library and
library, under the B1-H authorization, with the force-direct entry called before each attempt and
no identity read, discovery, address assignment, configuration, command, output access or motion:

| Variant | Value passed to the open entry | Result |
| --- | --- | --- |
| A — earlier form | the validated endpoint followed by the legacy option text (`--direct`, `--timeout`) | `GCLIB_NOT_CONNECTED`; refused, classified `OPEN_NOT_CONNECTED`, no retry |
| B — documented form | the validated endpoint alone | `GCLIB_SUCCESS`; handle returned and closed; handle zeroed |

Both variants were closed afterwards with the close entry reporting success. The Agent did not run
or repeat either variant. The endpoint value, the serial number and the exact transpose are not
reproduced here.

### D.4 What the A/B result does and does not prove

It **does** prove that on this machine, against this installed library, the option-suffixed value
does not open a connection while the bare validated address does — which is exactly the behaviour
the vendor document predicts for a value that is not an address.

It **does not** prove why the library rejects the longer form internally, that the same rejection
would occur for any other suffix, or that any controller or network state was the cause. It is not
a licence to experiment with the open value: the correct value is documented, and it is the only
value the boundary may build.

### D.5 Regression guards already in the tree

| Guard | Where | What it pins |
| --- | --- | --- |
| One builder | `MotionControllerApi.BuildOpenAddress` | returns the configuration's endpoint value and nothing else |
| One call site | B1 identity session and B2 trial session | exactly one open per session, with the built buffer |
| Address-shape rules | `MotionControllerEndpointConfiguration` | the endpoint must pass the documented shape before it can be used at all |
| No option text | source-boundary tests | no `--` switch, no option token and no suffix in the open path |
| Canonical refusal word | `MotionControllerApi.ClassifyOpenRefusal` | result 6 is `OPEN_NOT_CONNECTED`, never a numeric value and never vendor text |
| No retry | both sessions | a refused open ends the session |

### D.6 If switch semantics are ever required

They are not required today and are not authorized. A future request to use them must name the
deprecated layer explicitly, must produce fresh vendor evidence for it, must survive the intake
protocol in section G, and must be an Owner decision. It may not be introduced as a "small fix" to
the open address.

---

## E. Result enumeration order (0-7)

The manual lists the result enumerators in the order below (printed page 43) and states that zero
indicates success (printed page 6). The manual does not print numeric values; the installed header
declares the enumerators in that order, and the adapter records the resulting values as constants.
**No numeric value is ever printed, logged or returned by any WJSS surface** — the canonical words
below are the only form any outcome carries.

| Order | Enumerator | Canonical WJSS word when an open reports it | Boundary constant |
| --- | --- | --- | --- |
| 0 | `GCLIB_SUCCESS` | `GCLIB_SUCCESS` | `ResultSuccess` |
| 1 | `GCLIB_INVALID_ARGUMENT` | `OPEN_INVALID_ARGUMENT` | `ResultInvalidArgument` |
| 2 | `GCLIB_INTERNAL_ERROR` | `OPEN_INTERNAL_ERROR` | `ResultInternalError` |
| 3 | `GCLIB_TIMEOUT` | `CONNECTION_TIMEOUT` | `ResultTimeout` |
| 4 | `GCLIB_COMMAND_ERROR` | `OPEN_COMMAND_ERROR` | `ResultCommandError` |
| 5 | `GCLIB_BUFFER_TOO_SMALL` | `OPEN_BUFFER_TOO_SMALL` | `ResultBufferTooSmall` |
| 6 | `GCLIB_NOT_CONNECTED` | `OPEN_NOT_CONNECTED` | `ResultNotConnected` |
| 7 | `GCLIB_NOT_SUBSCRIBED` | `OPEN_NOT_SUBSCRIBED` | `ResultNotSubscribed` |

A value outside this order is classified `OPEN_RESULT_UNRECOGNISED`. It is never folded into a
known failure, never displayed as a number, and never treated as success. The mapping lives in one
place (`MotionControllerApi.ClassifyOpenRefusal`) and is shared by the B1 identity session and the
B2 trial, so the two surfaces cannot drift.

The connection-type enumeration is a separate list with Ethernet first (value 1), then serial and
PCI (printed pages 45-46). WJSS accepts only the Ethernet type and reports `CONNECTION_TYPE_MISMATCH`
otherwise.

---

## F. API and command registry

### F.1 Status vocabulary

| Status | Meaning |
| --- | --- |
| `VERIFIED_FOR_WJSS` | The vendor contract was read at the cited printed page, the committed boundary matches it, and the use is authorized for the surface named in the row. |
| `REVIEWED_NOT_USED` | The contract was read and matched, and no WJSS surface calls it. Reviewed so that a future proposal cannot claim ignorance. |
| `DEPRECATED_NOT_ALLOWED` | The vendor marks the entry as deprecated. It may not be used, and no WJSS surface resolves it. |
| `BLOCKED_PENDING_VENDOR_EVIDENCE` | A candidate whose use would require vendor evidence this closeout does not have (for the controller command language, the controller's own command reference). Not implemented, not authorized. |
| `NOT_REVIEWED` | **The default.** No entry exists in this registry, so nothing is known and nothing is permitted. Absence is not permission. |

### F.2 Library-entry registry

| Entry | Status | Surface that uses it | Why |
| --- | --- | --- | --- |
| `gclib_force_gcaps` | `VERIFIED_FOR_WJSS` | B1 identity session, B2 trial | sole direct/no-gcaps authority, called once before the open |
| `gclib_open` | `VERIFIED_FOR_WJSS` | B1 identity session, B2 trial | exactly one open per session, bare validated address |
| `gclib_close` | `VERIFIED_FOR_WJSS` | B1 identity session, B2 trial | deterministic close, handle zeroed and confirmed |
| `gclib_connection_type` | `VERIFIED_FOR_WJSS` | B1 identity session | rejects a non-Ethernet connection |
| `gclib_revision_information` | `VERIFIED_FOR_WJSS` | B1 identity session | bounded identity read, model token only |
| `gclib_serial_number` | `VERIFIED_FOR_WJSS` | B1 identity session | read and withheld; never rendered |
| `gclib_command` | `VERIFIED_FOR_WJSS` | B2 read boundary only | one scoped export, sixteen literal read-only requests, bounded response |
| `gclib_address` | `REVIEWED_NOT_USED` | none | the validated endpoint is already held; no read-back is needed |
| `gclib_error` | `REVIEWED_NOT_USED` | none | vendor error text may change between releases and is prohibited in outcomes |
| `gclib_addresses` | `REVIEWED_NOT_USED` | none | discovery is out of scope and unauthorized |
| `gclib_set_baud_rate` | `REVIEWED_NOT_USED` | none | the connection is Ethernet; no serial baud is set |
| `gclib_version` | `VERIFIED_FOR_WJSS` | Gate A native probe only | version equality proof, no controller handle |
| `GOpen` and every other legacy `G*` entry | `DEPRECATED_NOT_ALLOWED` | none | vendor-deprecated; its option syntax must never reach the current layer |

### F.3 Controller-command registry

These rows are **not** library entries. They are controller-language requests, which is layer 3
(B.3). No row here is executable outside the surfaces stated, and no row records a semantic that
the vendor document for the controller has not been read to support.

**Implemented, read-only, inside the closed B2 plan**

| Request literal | Candidate | Status | Native unit | Interpretation |
| --- | --- | --- | --- | --- |
| `MG _TSA`, `MG _TSB`, `MG _TSC`, `MG _TSD` | B2-OBS-1 | `VERIFIED_FOR_WJSS` | `STATUS_BITS` | `UNCONFIRMED` |
| `TP A`, `TP B`, `TP C`, `TP D` | B2-OBS-2 | `VERIFIED_FOR_WJSS` | `CONTROLLER_UNITS` | `RAW_OBSERVED` |
| `RP A`, `RP B`, `RP C`, `RP D` | B2-OBS-3 | `VERIFIED_FOR_WJSS` | `CONTROLLER_UNITS` | `RAW_OBSERVED` |
| `MG @OUT[1]`, `MG @OUT[2]`, `MG @OUT[3]`, `MG @OUT[4]` | B2-OBS-4 | `VERIFIED_FOR_WJSS` | `LOGICAL_STATE` | `UNCONFIRMED` |

The sixteen literals above are the **entire** command surface of WJSS. There is no seventeenth
literal, no caller-supplied request, no format string and no concatenation on the request path.

**C1 candidates — non-executable, not authorized, not prepared**

| Candidate | Status | Evidence position |
| --- | --- | --- |
| `ST` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | controller-language mnemonic; not documented in `gclib.pdf`; the controller's own command reference has not been reviewed for this closeout |
| `JG` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above |
| `BG` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above |
| `HV` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above |
| `HM` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above |
| `AC` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above |
| `SP` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above |
| `DC` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above |
| `PA` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above |
| `DP=0` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above; the `=0` operand form is also part of what must be evidenced |
| `DE=0` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above; the `=0` operand form is also part of what must be evidenced |
| `SB` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above; this is an output write and is additionally outside every authorized surface |
| `CB` | `BLOCKED_PENDING_VENDOR_EVIDENCE` | as above; this is an output write and is additionally outside every authorized surface |

No C1 candidate has a request literal, a plan entry, a session path, a test fixture, a
configuration key or a public member. `MotionCommandGate.IsClosed` remains `true`, the candidate
values remain `NOT_CONFIGURED` / `NOT_COMMISSIONED`, and no roadmap item is created here.

### F.4 The default rule

If an entry does not appear in F.2 or F.3, its status is **`NOT_REVIEWED`**. `NOT_REVIEWED` means:
no evidence, no permission, no implementation, no test and no roadmap position. Absence from this
registry is never an argument for use, and a reviewer must answer a proposal to use such an entry
with the intake protocol in section G rather than with a judgement call.

---

## G. Mandatory New-API / New-Command Intake Protocol

### G.1 When it applies

Before any additional library entry, controller command, request literal, response parser,
connection option or configuration key may enter the Product — including a change that "only
extends a table", "only adds one test", or "only reads one more value". It applies equally to C1
and to every later stage.

### G.2 The fifteen steps

| # | Step | Artefact required | Blocks on failure |
| --- | --- | --- | --- |
| 1 | **Name the layer.** State whether the proposal is current C API, deprecated legacy API or controller command language. | one line in the evidence record | anything unclassified |
| 2 | **Name the exact vendor source and version.** Document title, version, printed page. A web copy, a remembered snippet or a header alone does not qualify. | citation row | any uncited entry |
| 3 | **Record the contract.** Declaration or command form, parameters, return shapes, stated warnings. | contract table row | any paraphrase from memory |
| 4 | **State the operation direction.** Read-only or writing. "Probably read-only" is not an answer. | explicit statement | any write on a read surface |
| 5 | **Close the subject domain.** Enumerate every accepted subject (axis, output, index) and prove that nothing outside the enumeration can be addressed. | enumeration + refusal rule | open-ended domains |
| 6 | **Prove the request is a literal.** The request text must come from an immutable table keyed by a typed intent. No interpolation, no caller text, no format call, no concatenation. | source reference | any text path |
| 7 | **Bound the request and the response.** Length bound before the call, fixed response buffer, single terminated printable token, explicit refusal shapes. | bounds + parser rule | unbounded or permissive parsing |
| 8 | **Record the native unit and refuse conversion.** No millimetres, no percentages, no derived value. | unit word | any conversion |
| 9 | **State the interpretation limit.** `VERIFIED`, `RAW_OBSERVED` or `UNCONFIRMED`, with the missing evidence named. | interpretation word + gap | an unearned `VERIFIED` |
| 10 | **Check the Legacy evidence.** Record what the Legacy application did with the same entry, if anything. Existing Legacy usage is not authorization. | Legacy baseline row | silent imitation of Legacy |
| 11 | **Check this registry.** A missing or non-permitting status stops the proposal. | registry row | `NOT_REVIEWED`, `DEPRECATED_NOT_ALLOWED`, `BLOCKED_PENDING_VENDOR_EVIDENCE` |
| 12 | **Prove the boundary.** Run the repository boundary scan and the source-boundary tests; add an allow-list entry only in the exact file and with the exact token the exception names. | scan output + test names | any widened or wildcard exception |
| 13 | **Prove the negative space.** Show that no write, control, motion, discovery, subscription, configuration or output-mutation request can be formed from the new surface. | negative-space tests | a surface that could be widened later |
| 14 | **Record the Owner decision.** Date, scope, gate position, and whether the authorization opens anything. Authorization to *read* is not authorization to *act*. | dated decision row | any implicit authorization |
| 15 | **Update atomically.** Source, tests, registry, evidence, runbook, changelog and the gate statement change in one reviewed commit; the gate stays CLOSED unless the Owner explicitly opens it. | one commit | partial or documented-only changes |

### G.3 Prohibitions

1. No entry by chat memory, Legacy name, header comment, online copy, vendor example or naming
   resemblance.
2. No widening of an allow-list, an exception file or a token scope to "make it work".
3. No string, buffer, handle or format parameter on a request path — ever.
4. No vendor error text, endpoint, serial, native path, file hash, request text or environment value
   in any outcome, log, exception, test fixture, document or commit.
5. No write, control, motion, brake, home, position-define, output-mutation, discovery,
   subscription, program/array/firmware or configuration entry on a read surface.
6. No deprecated-layer syntax in a current-layer argument (section D).
7. No new dependency, project, package pin, lock file or fixture as part of an API or command
   intake; a genuine need stops the intake and is reported separately.
8. No hardware contact, configuration, NIC change or vendor service as part of an intake.
9. No interpretation upgrade without the named evidence — and never because a value "looks
   reasonable".
10. No C1 implementation, and no roadmap promotion, under this protocol. C1 needs its own
    authorization (section K).

### G.4 Evidence record shape

```text
entry:                 <library entry or mnemonic>
layer:                 CURRENT C API | DEPRECATED LEGACY API | CONTROLLER COMMAND LANGUAGE
vendor source:         <title> <version> printed page <n>
contract:              <declaration or command form>; returns <shapes>; warnings <none|text>
direction:             READ-ONLY | WRITING
subject domain:        <closed enumeration>
request:               <literal, or NOT APPLICABLE>
bounds:                request <= <n> characters; response buffer <n> bytes; accepted text <= <n>
native unit:           <word>
interpretation:        VERIFIED | RAW_OBSERVED | UNCONFIRMED   (missing evidence: <name>)
legacy usage:          <method, or NONE>
registry status:       VERIFIED_FOR_WJSS | REVIEWED_NOT_USED | DEPRECATED_NOT_ALLOWED |
                       BLOCKED_PENDING_VENDOR_EVIDENCE | NOT_REVIEWED
owner decision:        <date> <scope> <gate position>
redaction review:      no endpoint, serial, path, hash, request text or vendor error text
```

---

## H. B1-H — identity first contact: PASS

**Authorization.** One identity contact, one controller session, one open, three identity reads,
deterministic close — per `docs/MOTION_IDENTITY_FIRST_CONTACT_RUNBOOK.md`. Executed by the Owner, on
the Owner workstation, at the head recorded in the closeout. The Agent did not run, repeat or
observe it.

**Reading rule.** Four things are recorded separately below and must never be collapsed into one:
what the harness OBSERVED, how the harness CLASSIFIED it, what the Owner CONFIRMED manually, and
what the Owner finally ACCEPTED. The Owner's decision settles the B1-H outcome; it does not
retroactively change the harness result, and no field below is back-filled from a later decision.

### H.1 Harness observation

| Item | Observed value |
| --- | --- |
| Gate A native proof | PASS |
| Endpoint | `ENDPOINT_CONFIGURED` (the value is never recorded) |
| Open attempts | 1 |
| Open classification | `GCLIB_SUCCESS` |
| Connection | Ethernet; the connection-type read passed |
| Revision information | `DMCB140 Rev 1.0c2` (bounded, terminated and printable) |
| Serial number | observed and withheld (`SERIAL_OBSERVED_REDACTED`); the value is never rendered |
| Motion or output-control command | NONE — this surface resolves no command export, so no command of any kind was issued |

### H.2 Harness classification

| Item | Value |
| --- | --- |
| Exact expected model token | `DMC-B140-M` |
| Exact expected token present in the observed revision text | NO |
| Model comparison | `MODEL_CONFIRMATION_REQUIRED` |
| Outcome code | `MODEL_CONFIRMATION_REQUIRED` |
| Outcome accepted | NO — refused, non-accepted software outcome |
| Harness exit | 2 — the harness returns zero only for an accepted outcome; a `MODEL_CONFIRMATION_REQUIRED` outcome is non-accepted |
| Controller handle closed | TRUE — close confirmed and the handle zeroed |
| Native modules released | TRUE |

The observed revision text does not carry the exact expected model token, so the harness followed
its documented manual-confirmation path: it neither accepted nor rejected physical identity and
handed the decision to the Owner. That classification is a statement about the observed STRING; it
is not a statement that the machine is the wrong model, and it is not a failure of the connection.

### H.3 Owner confirmation and final acceptance

| Item | Value |
| --- | --- |
| OWNER CONFIRMATION | the observed revision `DMCB140 Rev 1.0c2` identifies the expected Controller model `DMC-B140-M` |
| Basis of the confirmation | Owner installation knowledge, compared against the observed revision text |
| FINAL OWNER ACCEPTANCE | B1-H WSB1 / MC-WSB1 identity first contact: **PASS** |
| B1-H status in this baseline | **PASS** |

### H.4 The automatic software path is separate and was not the observed result

`MODEL_MATCHED` → `IDENTITY_SESSION_COMPLETE` → exit 0 is the harness path that runs when the
observed revision text DOES contain the exact token. It is proven only by the focused software
tests, which inject a synthetic revision containing the token. It is **not** what this hardware
contact produced, and no field in H.1, H.2 or H.3 is restated to match it.

Both paths still carry `OWNER_MANUAL_CONFIRMATION_REQUIRED` as the logical-identity status: even an
automatic token match is a software string comparison, never a physical identity confirmation. The
difference is only whether the harness itself already knows the string matched.

The serial number remains withheld. The observed revision text is published because it is the
evidence the Owner's manual confirmation was made against and the Owner authorized it for this
closeout; it describes a firmware/model string and carries no endpoint, credential, path or serial.

### H.5 What B1-H establishes, and what it does not

It establishes that the documented open contract works on this machine with the bare validated
address, that the identity reads answer inside their bounds, and that the lifecycle closes and
unloads deterministically. The identity **PASS** rests on the Owner's manual confirmation of the
observed revision text (H.3), not on an automatic token match.

It does **not** establish any axis, limit, home, brake, fault, motion or unit fact. It is an identity
contact, not a motion commissioning step, and it opens no gate.

---

## I. B2-H — read-only observed-state trial: PASS

**Authorization.** One read-only observation trial: one open, one deterministic pass over the fixed
sixteen typed intents, then boundary invalidation, close and unload — per
`docs/MOTION_OBSERVATION_TRIAL_RUNBOOK.md`. Executed by the Owner. The Agent did not run, repeat or
simulate it, and performed no additional hardware contact of any kind in this closeout.

### I.1 Reported lifecycle

| Item | Value |
| --- | --- |
| Gate A native proof | PASS |
| Open attempts | 1 |
| Open classification | `GCLIB_SUCCESS` |
| Reads accepted | 16 of 16, in plan order |
| Generation | one, unchanged for the pass |
| Observation boundary invalidated | TRUE (before the close) |
| Controller handle closed | TRUE |
| Native handles released | TRUE |
| Cleanup counts beyond the single pass | zero |
| Harness exit | 0 |
| Trial code | `OBSERVATION_TRIAL_COMPLETE` |

### I.2 The sixteen readings

Value text is reproduced exactly as the boundary accepted it — no padding, no rounding, no
reformatting and no unit conversion. The three columns that matter are the native unit, the
interpretation word and the value; nothing else is derived from them.

| # | Kind | Subject | Native value text | Native unit | Interpretation | Code |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `AXIS_STATUS` | axis A | `15.0000` | `STATUS_BITS` | `UNCONFIRMED` | `OBSERVATION_STATUS_WORD_RAW` |
| 2 | `AXIS_STATUS` | axis B | `15.0000` | `STATUS_BITS` | `UNCONFIRMED` | `OBSERVATION_STATUS_WORD_RAW` |
| 3 | `AXIS_STATUS` | axis C | `15.0000` | `STATUS_BITS` | `UNCONFIRMED` | `OBSERVATION_STATUS_WORD_RAW` |
| 4 | `AXIS_STATUS` | axis D | `15.0000` | `STATUS_BITS` | `UNCONFIRMED` | `OBSERVATION_STATUS_WORD_RAW` |
| 5 | `COMMAND_POSITION` | axis A | `0` | `CONTROLLER_UNITS` | `RAW_OBSERVED` | `OBSERVATION_POSITION_VALUE_RAW` |
| 6 | `COMMAND_POSITION` | axis B | `0` | `CONTROLLER_UNITS` | `RAW_OBSERVED` | `OBSERVATION_POSITION_VALUE_RAW` |
| 7 | `COMMAND_POSITION` | axis C | `0` | `CONTROLLER_UNITS` | `RAW_OBSERVED` | `OBSERVATION_POSITION_VALUE_RAW` |
| 8 | `COMMAND_POSITION` | axis D | `0` | `CONTROLLER_UNITS` | `RAW_OBSERVED` | `OBSERVATION_POSITION_VALUE_RAW` |
| 9 | `ACTUAL_POSITION` | axis A | `0` | `CONTROLLER_UNITS` | `RAW_OBSERVED` | `OBSERVATION_POSITION_VALUE_RAW` |
| 10 | `ACTUAL_POSITION` | axis B | `0` | `CONTROLLER_UNITS` | `RAW_OBSERVED` | `OBSERVATION_POSITION_VALUE_RAW` |
| 11 | `ACTUAL_POSITION` | axis C | `0` | `CONTROLLER_UNITS` | `RAW_OBSERVED` | `OBSERVATION_POSITION_VALUE_RAW` |
| 12 | `ACTUAL_POSITION` | axis D | `0` | `CONTROLLER_UNITS` | `RAW_OBSERVED` | `OBSERVATION_POSITION_VALUE_RAW` |
| 13 | `DIGITAL_OUTPUT` | output 1 | `0.0000` | `LOGICAL_STATE` | `UNCONFIRMED` | `OBSERVATION_OUTPUT_STATE_UNCONFIRMED` |
| 14 | `DIGITAL_OUTPUT` | output 2 | `0.0000` | `LOGICAL_STATE` | `UNCONFIRMED` | `OBSERVATION_OUTPUT_STATE_UNCONFIRMED` |
| 15 | `DIGITAL_OUTPUT` | output 3 | `0.0000` | `LOGICAL_STATE` | `UNCONFIRMED` | `OBSERVATION_OUTPUT_STATE_UNCONFIRMED` |
| 16 | `DIGITAL_OUTPUT` | output 4 | `0.0000` | `LOGICAL_STATE` | `UNCONFIRMED` | `OBSERVATION_OUTPUT_STATE_UNCONFIRMED` |

### I.3 Redaction statement for this section

The readings above are native controller state text. They contain no endpoint, no serial number, no
host, no path, no hash, no request text and no vendor error string. They are recorded because the
Owner authorized publishing the sixteen observed values as part of this closeout; the raw harness
envelope, the JSON document, the endpoint statement, the serial number and every other
machine-identifying value remain uncommitted. The B1-H revision text published in section H is a
firmware/model string, not an identifier of the machine's location or serial. Recording a value is
not an interpretation: sections I.4 and J list what may not be concluded from it.

### I.4 What must not be inferred from these sixteen values

1. **No bit meaning.** The status word is one number. No bit position, no bit name and no bit
   polarity is derived here, and no "state" is claimed for any axis.
2. **No limit, home or fault state.** Nothing here says whether a limit is active, whether an axis
   is homed, or whether a drive is faulted.
3. **No motion completion.** The values do not say whether any axis is moving, stopped, enabled or
   disabled.
4. **No brake state.** The output values carry no wiring, polarity, brake or interlock meaning.
5. **No engineering unit.** `0` is a controller-unit text. It is not a millimetre, an inch, a
   degree or a percentage, and no encoder scaling exists.
6. **No physical meaning for `TP` or `RP`.** The two position values keep their Legacy labels only;
   the vendor description and the Legacy label of which value is commanded and which is measured
   disagree for this machine, so both stay `RAW_OBSERVED`.
7. **No safe state.** A zero reading is not a safe reading, and a stopped reading is not a proven
   stop.
8. **No authorization.** A successful read opens no gate, changes no candidate value and authorizes
   no command.

---

## J. Evidence gaps

These are the reasons the B2 interpretation stays weak and the reason C1 cannot be designed yet.

1. **Status-word bit order and polarity** for the installed controller family are unconfirmed. No
   vendor bit table for this model has been reviewed in this closeout.
2. **The meaning of the observed status word value** is therefore unconfirmed, including whether the
   same value would be produced by a different axis state.
3. **The `TP` / `RP` label disagreement** is unresolved: the Legacy label and the vendor description
   of which value is commanded and which is measured do not agree for this machine.
4. **Output numbering** is confirmed only for the Owner-stated allocation 1-4; every other cabinet
   allocation is unknown.
5. **Whether an output read reflects the commanded or the physical output** is unknown.
6. **Electrical polarity of any output**, and therefore of any brake function, is unknown.
7. **The physical meaning of one controller count** is unknown, so no conversion can exist.
8. **The controller's own command reference** has not been reviewed in this closeout. Every C1
   candidate therefore stays `BLOCKED_PENDING_VENDOR_EVIDENCE`; `gclib.pdf` does not document the
   command language at all (B.3).
9. **The installed firmware's exact response format** for any command is unknown; the parser accepts
   one bounded numeric token and refuses everything else rather than learning the format by
   observation.
10. **Timing, repeatability and failure injection** are unmeasured: one pass, one generation, no
    repetition.
11. **The deprecated legacy layer was never exercised** by WJSS, so no behaviour of it is evidenced
    beyond the vendor text and the Owner A/B in section D.
12. **The library's internal error strings** were never captured or compared, by design.

None of these gaps is closed by this document, and none may be closed by inference from the section
H or I values.

---

## K. PR #32 — C1 successor boundary (documented, NOT IMPLEMENTED)

### K.1 What C1 is

C1 is the next contract: the application-facing boundary that would make the already-implemented B2
read surface usable by the Application. It is written down in
`docs/MOTION_OBSERVED_STATE_FOUNDATION.md` section 6 and **does not exist in code, tests,
configuration or contracts**.

### K.2 What PR #32 may contain

Only what its own Owner authorization names. It may not contain, under this baseline:

- any new library entry beyond the seven already-verified entries (F.2);
- any controller command beyond the sixteen literals (F.3);
- any write, control, motion, brake, home, position-define, output-mutation, discovery,
  subscription, program/array/firmware or configuration operation;
- any interpretation upgrade from `RAW_OBSERVED` or `UNCONFIRMED` without the evidence in section J;
- any dependency, project, package pin or lock change that this closeout did not identify;
- any hardware contact performed by the Agent, and any repetition of B1-H or B2-H;
- any relaxation of a redaction rule, an allow-list or an exception scope.

### K.3 Entry criteria for C1

1. Owner authorization naming the surface and the exact entries.
2. The evidence in section J that the claimed interpretation needs.
3. A single connection owner at the application boundary, with B1's rules preserved: one open, one
   outstanding operation, one generation, no retry, no reconnect, deterministic close and unload.
4. A freshness rule, so an observed value can never be read as a current or authorized state.
5. Attestation that the Motion authorization gate stays CLOSED and the candidate values stay
   `NOT_CONFIGURED` / `NOT_COMMISSIONED`.

### K.4 Status

**NOT IMPLEMENTED.** No export, no session, no parser, no route, no Snapshot member, no UI, no test
and no configuration. `MotionCommandGate.IsClosed` remains `true`. No C1 candidate in section F.3
has an executable form anywhere in the repository.

### K.5 Documentation punchlist for PR #32 (recorded here, not changed here)

A secondary drift review found two stale XML documentation comments in the B2 model. They are
recorded as a punchlist because this repair is a B1-H evidence-provenance correction and Product
Source is deliberately unchanged.

| Location | Current comment says | Why it is stale |
| --- | --- | --- |
| `adapters/motion-library/MotionObservedState.cs`, member `InterpretationState` | "In this revision only `UNAVAILABLE` is reachable" | The committed B2 read boundary produces `RAW_OBSERVED` and `UNCONFIRMED` for accepted reads, and `REFUSED` and `UNAVAILABLE` for the other paths — four reachable words, as `MotionObservedStateVocabulary.ReachableInterpretationStates` pins |
| `adapters/motion-library/MotionObservedState.cs`, factory `Unavailable` | "The only state this revision can produce" and "no authorized read surface exists" | A read surface exists; the factory now describes only the no-value path, not the whole revision |

Both comments are documentation only: they are not compiled into behaviour, no member reads them,
and no test asserts them. Correcting them would be comment-only, but it is still a Product Source
change, so it is **not** made in this pass. The `MotionObservationSession` comment that a session
*without* a read boundary can only report `UNAVAILABLE` remains accurate and is not part of this
punchlist.

PR #32 (or a separate documentation-only correction) may fix the two comments with no behaviour
change, and must not change any interpretation logic while doing so.

---

## L. Validation position of this document

**Performed in Arena:** exact-name retrieval by the Drive connector (one match, read-only); full
text extraction of the vendor document outside the repository; printed-page verification of every
citation in sections B, C, D and E; review of every changed surface; boundary scans (`S1`-`S9`);
section, registry and protocol completeness checks; leakage scans for endpoint, serial, path, hash,
request text and vendor error text; source-boundary and document-boundary tests authored (not
executed here); `git diff --check`; project and lock drift checks; clean-worktree proof.

**Integrity correction (2026-10-17).** An earlier revision of section H stated that the observed
revision contained the exact model token and that the harness therefore returned `MODEL_MATCHED`,
`IDENTITY_SESSION_COMPLETE` and exit 0. That was wrong. Section H now records, as separate fields,
what the harness observed (`DMCB140 Rev 1.0c2`), how it classified that string
(`MODEL_CONFIRMATION_REQUIRED`), what the Owner confirmed manually, and what the Owner finally
accepted. B1-H remains **PASS**. The earlier text is not silently rewritten out of the record: the
correction is stated here and in `CHANGELOG.md`, and the automatic path is described in H.4 as a
separate software path that this contact did not produce.

**NOT RUN in Arena:** the .NET build, the locked restore, the focused suite and the full suite. No
.NET SDK is available in the Agent environment, so no compile or test result is claimed.

**Not verified anywhere:** every item in section J.

`SOURCE AUTHORED — YES`; `STATIC CHECKS — PASS`; `COMPILE — NOT VERIFIED BY AGENT`;
`TESTS — NOT VERIFIED BY AGENT`; `IMPLEMENTATION COMPLETE — NOT CLAIMED BY AGENT`.
