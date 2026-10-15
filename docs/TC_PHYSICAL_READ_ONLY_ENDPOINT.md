# One-Node Physical TC Read-Only — Endpoint Policy (Owner decision R1)

**Status:** current — this document is the single authority for how the one-Node physical read-only
TC acquisition obtains its endpoint. It is a design and boundary document: it authorizes nothing by
itself, and every execution gate below still requires its own Owner decision.

Supersedes any earlier wording that placed a device address in a committed configuration file.

---

## 1. The rule

The physical WAGO coupler address and the dedicated NIC configuration are **Owner-local confidential
deployment information** (Owner decision R1). They are:

- **not committed** — not in any tracked file, comment, test name, assertion, error message, sample
  configuration, documentation, changelog, generated fixture, API response, UI output or log;
- **not derived** — not from the legacy `ip_modbus` field, not from the committed test
  configuration, and not from any other committed value;
- **not inferred** — never selected because an address happens to be present.

The endpoint exists only on the Owner's machine, only for the duration of the process that needs it,
and it is the **Owner who supplies it**, outside the repository.

## 2. Where the endpoint comes from

Exactly one external source, read at startup:

| Item | Value |
| --- | --- |
| Endpoint variable | `WJSS_TC_NODE_01_ENDPOINT` |
| Mode variable | `WJSS_TC_ACQUISITION_MODE` |
| Mode values | `DETERMINISTIC` (default when absent) or `PHYSICAL_READ_ONLY` |
| Node / Cabinet identity | `TC-NODE-01` / `CAB1` — fixed logical identity, never taken from the value |

There is deliberately **no committed configuration path**. No repository file can supply, override or
default the address, and the resolver accepts only an environment reader. The resolver performs
**no I/O**: it validates a string. It does not probe, ping, resolve, scan or enumerate anything.

## 3. Fail-closed refusals

Startup is refused — with nothing bound, and with no value in the refusal text — when the endpoint is:

- absent, blank or whitespace only;
- not a single bare host token: any scheme, port suffix, path, query, fragment, user-info, separator,
  whitespace, host name or second value;
- not four decimal octets in range, or an octet written with an ambiguous leading zero;
- loopback, unspecified, broadcast, multicast, reserved, link-local, or in a documentation-only or
  benchmarking range (no device can answer there, and the committed test addresses live in one of
  those ranges — so they can never be used as a physical endpoint);
- accompanied by more than one configured endpoint value;
- supplied while the acquisition mode is not the explicit physical read-only mode.

The mode is never inferred from the endpoint variable, and there is **no silent fallback** in either
direction: a refused physical mode never becomes a deterministic run, and a deterministic run never
reads the endpoint variable at all.

## 4. What the endpoint may never become

- It is never serialized. The Runtime contract has no endpoint field, and the record carries only the
  redacted logical identity `ENDPOINT_CONFIGURED`.
- It is never logged or displayed. The startup diagnostic prints the redacted identity, and the
  in-memory handle renders only that identity, so interpolation cannot leak the value.
- It is never part of evidence, a fixture, an API response, or UI state.
- It never becomes a control input. Nothing in the physical read-only slice writes, commands,
  dispatches or actuates anything.

## 5. What the physical slice still is not

A successful read establishes **only** that 40 raw words came back. It does not establish the process
image, the mapping parity, scaling, signed representation, byte order, word order, wire-break or
reverse-polarity sentinel meaning, cold-junction compensation, field timing, or the transaction and
response unit identifiers. Every one of those remains **NOT VERIFIED**, the record remains
**QUARANTINED**, and the commissioning state remains **NOT COMMISSIONED**.

The two legacy sentinel candidates supplied by the Owner (`0x7FFF` and `0xF830`) are carried as raw
hexadecimal and decimal evidence with their legacy classification and **no ordinary temperature**: a
candidate number is never substituted for a sentinel.

## 6. Not authorized by this policy

No NIC application, no WAGO contact, no port-502 access, no physical Modbus execution, no T01
execution, and no physical evidence claim. Write function codes, write-capable imports, discovery,
subnet scans, ICMP sweeps, retries, reconnects, unbounded polling, and any key, signature, token or
automatic acceptance mechanism remain out of scope — the last group was **cancelled** by Owner
decision and is not restored here.

## 7. Owner-local checklist

1. Set `WJSS_TC_ACQUISITION_MODE=PHYSICAL_READ_ONLY` (and nothing else — no other mode is accepted).
2. Set `WJSS_TC_NODE_01_ENDPOINT` in the Owner-local environment only. Never place it in a file that
   the repository tracks.
3. Confirm the process refuses to start when the variable is absent (the fail-closed check).
4. Confirm the startup line shows `endpoint=ENDPOINT_CONFIGURED` (or `ENDPOINT_NOT_CONFIGURED`) and
   that no address appears anywhere in the console output.
5. Confirm `git status` is clean and `git grep` finds no address after the run.
