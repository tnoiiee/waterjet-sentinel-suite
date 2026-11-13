# Legacy source rev2 review — the Pump Outlet live zero and the water-level span

**Date:** 2026-11-07 · **Trigger:** Owner field contradiction on the Stage A run · **Authority for the review:**
the Owner's instruction to read the Legacy source pack **rev2** through the connected Drive and analyse the
code instead of reasoning from assumptions · **Result:** two Product scaling corrections, one recorded
function-code divergence to be settled in the field, and one superseded documentation claim.

---

## 1. Source read

| Item | Value |
| --- | --- |
| File | `repomix-output-exact-lagacy-source-pack-rev2.md` (Legacy `WaterCannonControl` WinForms app, packed) |
| Size | 4,015,268 bytes · 69,982 lines · 42 packed files |
| Predecessor read for the rev1/rev2 diff | `repomix-output-exact-lagacy-source-pack.md` (3,362,177 bytes · 57,439 lines) |
| Device topology in the pack | two Modbus devices and one motion controller; the device addresses are not reproduced here |
| Where the analog chain lives | `FormWCAndPump.cs` (fieldbus read + pump UI + AO write), `FormMain.cs` (auto-clean sequence incl. the Pump-pressure gate), `FormDataCenter.cs` (register grid), `Config.json` / `Modbus_Config.json` |

The pack contains three near-identical copies of the main form (`FormMain.cs`, `FormMain - Copy.cs`, and the
`FormWCAndPump.cs` variant). Every conclusion below was checked in **all** copies, so no finding rests on one
stale duplicate.

## 2. What the Legacy code actually does

### 2.1 `map()` — the clamped linear map

```
if (x >= in_max) x = in_max;
if (x <= in_min) x = in_min;
return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
```

Identical to the Product's `ProcessIoEngineering.LegacyMap` (clamps at **both** ends). The adoption of the
clamping convention is correct and is not changed here.

### 2.2 Pump outlet pressure — a two-stage chain that consumes the live zero twice

rev2 (`FormWCAndPump.cs` copy, and the same two lines in the `FormMain` copies):

```
pumpPressure = map(_dataProvider.PumpPressureRegister, 0, 32767, 0, 20);   // raw -> 0..20 mA
pumpPressure = Math.Round(map(pumpPressure, 4, 20, 0, 40), 2);             // 4..20 mA -> 0..40 bar
```

rev1 has the **same two lines**. So this is not a rev1/rev2 drift: the Product inherited the Legacy formula
faithfully, and the Legacy formula itself assumes that the channel reports 0..20 mA across 0..32767.

### 2.3 Every other analog row in the same Legacy file uses ONE stage

| Row | Legacy formula | Product conversion | Verdict |
| --- | --- | --- | --- |
| WC / cannon outlet pressure (`WCPressureRegister[0..7]`) | `map(raw, 0, 32767, 0, 40)` | `WcPressureBar` = same, one stage | matches |
| Water tank level | rev2 `map(raw, 0, 32767, 0, 240)`, then `map(level, 0, 240, 0, 100)`; **rev1 used 0..270** | was `0..270` | **rev1 value — corrected below** |
| Water level percent | equivalent to `map(raw, 0, 32767, 0, 100)` | same | matches |
| Pump speed AO write | rev2 `map(pumpCurrentOut, 4, 20, 0, 32767)`; **rev1 used `0, 20`** | `PumpAnalogCommand.TryToRaw` = `4..20 mA -> 0..32767` | matches rev2 (the commissioned Pump path) |
| Boiler/DCS rows (main steam 0..200, coal flow, power load, tilting −30..60, air flow, RHTemp) | single or two stage, DCS device | not adopted (not Suite-owned Channels) | out of scope |

The AO write line is decisive for the module convention: the Legacy software **drives** the pump inverter with
`4 mA = raw 0`, i.e. these WAGO channels are scaled over the 4..20 mA signal span. rev1 wrote the same channel
with a 0..20 mA span and rev2 fixed it to 4..20 mA. The input side of the same card family was never fixed.

### 2.4 Register and bit indices — the adopted bindings are right

`HoldingRegisters[0]` = water tank level, `[2]` = pump pressure (commented "Pressure before pump of Old
software"), `[3..10]` = WC 1..8. The Product binds `AI-001+ → 0`, `AI-002+ → 1`, `AI-003+ → 2`, `AI-004+…
AI-011+ → 3…10` on the input-register area, which reproduces those indices (the Stage A field read of
`AI-002+` returning a live value confirms index 1 physically exists). Digital: rev2 reads `ReadCoils(1, 0,
100)` and takes limit switches at `Coils[20..27]` (opened) and `Coils[28..35]` (closed), pump select at
`Coils[0..1]`, feeder bank at `Coils[7,8,10,9]`, motor valves at `Coils[12,13,15,16,18,19]`. The numeric
addresses match the adopted bindings exactly (including the crossed feeder pair order and the two skipped
motor-valve addresses); see §5 for the one divergence.

### 2.5 The Legacy Pump-ready gate

```
_processMsg = "Checking the Pump Pressure > 15.0 Bar...";
if (pumpPressure >= 15.0) { ... pass ... }
```

Same 15 bar number as the Product. The Product is **stricter** (`Bar > 15`, `SequencingPressure.PumpReady`),
and the Owner ruling that AI-003 is the sole Pump-ready source is unchanged. The display lamp thresholds
(`pumpPressure >= 5.0`, `cannonInCleaningPressure >= 5.0 / <= 1.0`) are colour indicators, never gates, and
are not adopted. `Config.json → AutoCleaningConfig.PressureSetpoint: 18` is the pump's auto-mode pressure
target (`PRESSURESETPOINT_AUTOMODE`), a different quantity from the ready gate — and it is what makes §3
urgent.

## 3. Why the two-stage chain is a defect on this hardware, not a style choice

The commissioned channel is configured **4-20 mA** with user limits `32767 / −32768` (the Owner's own
configuration evidence, recorded as EV-04 for slot 13 / channel 3 of the 750-471, and restated in the adopted
identity as `4-20 mA; 0-40 bar`). So `raw 0` already *is* 4 mA. Subtracting a live zero a second time in
software scales every reading by `40/32767` instead of `(raw→mA→bar)`, i.e. it reports `0.649 ×` the truth
wherever the truth is mid-scale:

| raw | current loop | Legacy/Product two-stage | corrected single stage | what the plant actually sees |
| --- | --- | --- | --- | --- |
| 580 (pump stopped) | 4.283 mA | 0.354 mA → **0.0000 bar** (clamped at in_min) | **0.708 bar** | ~0 bar, a normal live-zero rest value — no fault |
| 13631 (pump running, Stage A) | 10.656 mA | 8.320 mA → **10.7999 bar** | **16.6399 bar** | the Owner's independent reading: 16.x bar |

Two consequences, both safety-relevant:

1. **The effective Pump-ready threshold is silently 20.0006 bar, not 15 bar.** The two-stage map reaches
   15 bar only at raw 16383.5, which is 20.0006 bar of real pressure. A commissioning or Production sequence
   that waits for `bar > 15` therefore keeps the pump running up to more than 20 bar before it proceeds. With
   the pump's own auto-mode target at **18 bar** (legacy `PressureSetpoint`), the two-stage map reports 12.50
   bar, so the gate **never opens at all** — the sequence cannot complete no matter how long the pump holds.
2. Every threshold expressed in bar (Pump-ready, low-pressure supervision, high-pressure abort) is evaluated
   against an under-scaled value: the aborts arrive late, and the confirmations arrive late or never.

The fail-closed behaviour that matters is untouched by the fix. A loop below 4 mA is reported by this channel
as a **negative** word; the observation builder refuses `raw` outside `0..32767` as `Quality.BAD`
(`ProcessIoFoundation` `inDomain` check), so an open circuit or an under-range transmitter is refused rather
than clamped into a plausible value. Under the old chain, that protection was incidental (clamping hid the
distinction); after the fix it is explicit and it is the only path to a value.

## 4. Corrections made in this change

| # | Site | Before | After | Reason |
| --- | --- | --- | --- | --- |
| 1 | `ProcessIoEngineering.PumpOutletPressureBar` | `raw → 0..20 mA`, then `4..20 mA → 0..40 bar` | `raw 0..32767 → 0..40 bar` (one stage) | §3 — live zero applied by the channel; matches the Owner ruling `4-20 mA; 0-40 bar` and both field data points |
| 2 | `ProcessIoEngineering.WaterTankLevel` | `raw → 0..270` | `raw → 0..240` | §2.3 — rev2 replaced the rev1 span; the adoption had kept rev1 |
| 3 | `legacyEvidence.mjs` `ANALOG_CONVERSIONS['AI-003+']` / `['AI-001+']` | two steps / 270 | one step / 240 | mirror parity with 1 and 2 |
| 4 | `ProcessIoFoundationTests` | asserted the pump row is *never* the WC/IV mapping; `WaterTankLevel(32767) = 270` | asserts the pump row **is** the same commissioned pressure conversion (role, not scale, separates the rows); `= 240`; pins the two field anchors | the old assertion pinned the defect; the new one pins the correction |
| 5 | `ProcessIoPumpLifecycleTests` bracket pair | 16448 / 16317 (bracketed the two-stage boundary at raw 16383.5) | 12352 / 12223 (bracket the real 15 bar boundary at raw 12287.6) | fixture re-derivation so "just below" still means just below. Every verdict is preserved: the below word still refuses, the above word still accepts. No acceptance rule was weakened |
| 6 | `docs/CURRENT_STATE.md` mapping register; `docs/PHYSICAL_HARDWARE_EXECUTION_RUNBOOK_2026-11-07.md` Stage A expectations | two-stage claim; pump-run baseline expected as a refusal | corrected claims; pump-run baseline expected as `Accepted` / exit 0 **on a build at this head** | the runbook must state what the corrected code does |

**Deliberately not changed:** the AO Pump command map (`PumpAnalogCommand`, already rev2's `4..20 mA`);
`WcPressureBar`; `WaterTankLevelPercent` (algebraically unchanged by rev2's chain); the strict `>` at 15 bar;
AI-003 as the sole Pump-ready source; AI-002 as diagnostic-only; every endpoint, consent and authorization
boundary. `ProcessIoEngineering.TryPumpSpeedRaw` keeps the rev1 `0..20 mA` span on purpose: it is the general
bench helper, it is not the Pump command path, its shape is pinned by a parity test, and the physical bench
write path passes a raw word directly (`ProcessIoBenchRunner` never scales it). That asymmetry — the *simulated*
bench branch maps a raw value as if it were mA — is recorded here as a known, sim-only wart for a future Owner
decision; it cannot reach hardware.

## 5. Divergence to settle at Stage E (documentation only, no code change)

The Legacy code reads the limit-switch bits from the **coil** space (`ReadCoils`, FC01) at addresses 20..35,
while the Product reads the same addresses from the **discrete-input** space (FC02). Both agree on the numbers;
they disagree on the function code, and the corrected workbook only fixes the Upper/Lower split. So Stage E
must report which space actually answers: an FC02 read that returns the switch states is a pass; an FC02 read
that faults or sits constant while an FC01 read at the same addresses tracks the switches is evidence that the
DI image is coil-mapped on this controller — a `WJSS_CONFIG` decision for the Owner, never a silent fallback in
code.

## 6. What this changes for the held hardware gates

The Stage A pump-off run (raw 580) stays a **refusal** under the corrected map (0.708 bar ≤ 15) — the
fail-closed branch is still proven on real signal. The Stage A pump-run run (raw 13631) must be **re-run on a
build at this head**: it now converts to 16.6399 bar, which is strictly above 15, so the trial must report
`pump_ready=TRUE`, `strict_comparison=16.6399 > 15 => TRUE`, code `Accepted`, **exit 0**. The previously
reported exit 2 with `PUMP_READY_REFUSED_AT_OR_BELOW_SETPOINT` was correct behaviour of defective code, and it
is reclassified: it is evidence of the scaling defect, not a passing Pump-ready demonstration. The optional
low-setpoint demo (A3, `WJSS_PUMP_READY_SETPOINT_BAR=10`) is no longer needed to show the accepted branch, but
it remains useful as a second data point; it must never be used as a production default.

The Owner's build of record must therefore be **recreated at this head** before Stages B–E continue, and the
full Release test suite re-run: this change edits Product code, so the previous 1787/1787 pass no longer
describes the binary under test.

## 7. One-line confirmation the Owner can make at the panel

The corrected map assumes the channel is scaled 4..20 mA (raw 0 = 4 mA). If the channel were instead scaled
0..20 mA, raw 580 at rest would be an under-range fault, not 4.28 mA. Observed: `quality=GOOD`, no underflow
diagnosis, a resting value of 0.708 bar and a running value that matches the Owner's independent 16.x bar —
all three select 4..20 mA scaling. If the panel configuration ever shows the pump channel at `0-20 mA`, stop
and report it: that would reopen this correction rather than contradict it.
