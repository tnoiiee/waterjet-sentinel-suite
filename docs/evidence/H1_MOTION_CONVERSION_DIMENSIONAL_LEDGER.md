# H1 Manual Motion — Dimensional Analysis Ledger (per-axis conversion)

**Document class:** EVIDENCE AND DERIVATION RECORD. It records one derivation, its authority and
its expected values. It authorizes no hardware operation, no command and no Product surface.
`docs/MASTER_PLAN.md` remains the roadmap of record; `docs/MOTION_COMMISSIONING_FOUNDATION.md`
remains the commissioning authority; `docs/SAFETY_BOUNDARY.md` remains the safety boundary.

**Gate position (unchanged).** The Motion authorization gate stays CLOSED
(`MOTION_COMMISSIONING_GATE_NOT_OPEN`); the axes stay `NOT_COMMISSIONED`.

**Hardware position.** No controller was contacted, opened, read, configured or commanded while
producing this document. The owner-local build is the validation of record; the Arena sandbox has
no .NET SDK, so nothing here is a compiled result.

**Supersedes.** The pulse-domain constants removed in this pass
(`ManualMotionPulseDomain.EncoderPulsesPerRevolution = 10000`,
`.DrivePulsesPerRevolution = 5000`, `.EncoderCountsPerDriveCount = 2`). Those were Product
constants. They are now five per-axis engineering settings read from the axis profile, and every
quantity below is derived from them.

---

## 1. The five per-axis inputs

Independent on each of axes A, B, C and D. Absent is a real state: an axis missing any of them has
no conversion, and every operation that needs one is refused.

| # | Profile property | Meaning | Unit | Schema range |
| --- | --- | --- | --- | --- |
| I1 | `encoderPulsesPerRevolution` | encoder counts produced by one MOTOR revolution | counts/rev | 1 … 10,000,000 (integer) |
| I2 | `drivePulsesPerRevolution` | drive (reference) pulses consumed by one MOTOR revolution | pulses/rev | 1 … 10,000,000 (integer) |
| I3 | `mechanicalGearRatio` | OUTPUT revolutions per MOTOR revolution (`0.1` = 1:10 reduction) | output rev / motor rev | > 0, ≤ 7 decimal places |
| I4 | `pitchMillimetresPerRevolution` | travel produced by one OUTPUT revolution | mm / output rev | > 0, ≤ 7 decimal places |
| I5 | `positionToleranceMillimetres` | accepted distance from the target for a Move to complete | mm | > 0, ≤ 7 decimal places |

Owner-confirmed values for the MC-WSB1 A/B pair used in this ledger: I1 = 10,000, I2 = 5,000,
I3 = 0.1, I4 = 5. C/D are **not** assumed: they are whatever their own profile says, and a missing
value there refuses the conversion on those axes only.

## 2. The twelve formulas

Every row is derived from the rows above it. `decimal` throughout; no floating point, no rounding
except where stated; no literal appears in the code that is not one of the five inputs.

| # | Quantity | Formula | Input unit → output unit | Expected (A/B) |
| --- | --- | --- | --- | --- |
| F1 | travel per motor revolution | `I3 × I4` | (out rev/motor rev) × (mm/out rev) = **mm/motor rev** | 0.1 × 5 = **0.5** |
| F2 | TP normalization ratio (DERIVED, read-only) | `I2 ÷ I1` | (pulses/rev) ÷ (counts/rev) = **dimensionless** | 5,000 / 10,000 = **0.5** |
| F3 | PA command units per millimetre | `I2 ÷ F1` | (pulses/rev) ÷ (mm/motor rev) = **command units/mm** | 5,000 / 0.5 = **10,000** |
| F4 | RP feedback units per millimetre | `I2 ÷ F1` | (pulses/rev) ÷ (mm/motor rev) = **RP counts/mm** | 5,000 / 0.5 = **10,000** |
| F5 | TP feedback units per millimetre | `I1 ÷ F1` | (counts/rev) ÷ (mm/motor rev) = **TP counts/mm** | 10,000 / 0.5 = **20,000** |
| F6 | command and reference share one count density | `F3 == F4` | boolean | **true** |
| F7 | reference counts per command count | `F4 ÷ F3` | RP counts ÷ command unit = **dimensionless** | 10,000 / 10,000 = **1** |
| F8 | comparison target (the one comparison domain) | `PA operand × F7` | command units → **RP counts** | see F11 |
| F9 | comparison tolerance | `I5 × F4` | mm → **RP counts** | 0.1 mm → 1,000 counts |
| F10 | TP normalized into the reference domain | `raw TP × F2` | TP counts → **RP counts** | see F11 |
| F11 | RP normalized into the reference domain | `raw RP × 1` (stated, not omitted) | RP counts → **RP counts** | identity |
| F12 | mutual agreement (a separate question from target agreement) | `|F10 − F11| ≤ F9` | boolean | — |

F8, F10 and F11 are the only three quantities the comparison ever sees, and all three are expressed
in **RP counts**. That is the single documented comparison domain.

## 3. The commanded target of the H1 P1 points

Both points use the same conversion; only the millimetre figure differs.

| Point | Axis | Millimetres | F3 → commanded PA operand | F4 → expected RP (F11) | F5 → expected raw TP (before F10) | F10 (normalized TP) |
| --- | --- | --- | --- | --- | --- | --- |
| P1 | A (Horizontal) | 180 mm | 180 × 10,000 = **1,800,000** | **1,800,000** | **3,600,000** | 3,600,000 × 0.5 = **1,800,000** |
| P2 | B (Vertical) | 200 mm | 200 × 10,000 = **2,000,000** | **2,000,000** | **4,000,000** | 4,000,000 × 0.5 = **2,000,000** |

For each point the commanded target (F8), the normalized theoretical reading (F10) and the normalized
reference reading (F11) resolve to the **same number in the same domain** with **no unexplained
factor**: 1,800,000 for P1, 2,000,000 for P2. The factor between a raw TP count and a raw RP count is
exactly F2 = 0.5, which is I2 ÷ I1 — the drive's count density over the encoder's — not a
divide-by-two literal and not a magic number.

## 4. The Legacy formulas, re-expressed against this model

The Legacy application computed three things. Each maps onto a row above without a residual factor:

| Legacy behaviour (paraphrased) | This model | Note |
| --- | --- | --- |
| command counts = `mm ÷ (ratio ÷ pulsesPerRev) ÷ pitch` | F3 | The ratio and pitch terms combine to F1; the pulses-per-rev term is I2, the drive density — the same density the reference generator counts in. |
| TP millimetres = `rawTP × (ratio ÷ pulsesPerRev) × pitch ÷ 2` | F10 inverted by F2 | The `÷ 2` is the encoder/drive count-density ratio (10,000 ÷ 5,000), i.e. F2 applied in the opposite direction — not an independent constant. |
| RP millimetres = `rawRP × (ratio ÷ pulsesPerRev) × pitch` | F11 inverted by F4 | No `/2` term: the reference reading is already in the reference domain, which is why only the theoretical reading is normalized. |

The Legacy `/2` is therefore **the same quantity as F2**, derived from the same two pulses-per-rev
figures. In this product it is computed per axis at run time and is displayed read-only; it is not
editable, because an editable factor would be a second, competing authority for the same physical
relationship.

## 5. The factor of ten: where it is, and where it is not

The hardware evidence of record is a pair of readings taken on an axis of this machine:

| Reading | Value |
| --- | --- |
| raw TP | 35,999,997 → normalized 17,999,998.5 (F10) |
| raw RP | 18,000,000 (F11) |
| difference | 1.5 counts = 3 raw TP counts × 0.5 |

The 1.5-count difference is unit-free and is explained exactly: the encoder stopped 3 counts short of
its commanded multiple. Nothing about that figure depends on the millimetre scale.

The magnitude, however, does. Applying F4 to 18,000,000 RP counts gives **1,800 mm**, and applying F5
to 35,999,997 TP counts gives **1,800 mm** as well. So the trial of record is internally consistent at
a commanded distance of 1,800 mm, not 180 mm. Read the other way — take the commanded distance as
180 mm and the sample implies 100,000 RP counts/mm and 200,000 TP counts/mm — the profile of record
would be wrong by exactly a factor of ten in F1 (0.5 mm per motor revolution instead of 5 mm per motor
revolution, or a ratio of 1 instead of 0.1).

**Determination.** The factor of ten is not a missing multiplier in the conversion; it is a difference
in the physical parameters that were in effect when the sample was taken. The conversion is not
allowed to absorb it, because absorbing it would make a wrong profile look right. Consequently:

1. No code multiplies or divides by ten. There is no such literal anywhere in the conversion.
2. The conversion resolves correctly for the profile it is given, and the ledger above states what it
   will do for A/B.
3. The remaining question — whether the axis really has 0.5 mm per motor revolution or 5 mm per motor
   revolution with a 1:1 reference path — is an **Owner-local profile question**, answered by reading
   the controller's own configuration (the pulses-per-rev and gearing actually loaded in it) and by one
   contact-measured travel check. It is not answerable from the Arena sandbox and it is not a units
   defect in this code.
4. Until that is answered, the Move comparison stays fail-closed on the physical question: a Move that
   ends with the two readbacks agreeing but neither within tolerance of the target is reported as
   `MOVE_TARGET_NOT_REACHED_READBACKS_AGREE` and **no point completes**, so P1 never advances to P2 on
   a scale that has not been reconciled.

## 6. Galil command and feedback semantics the model rests on

Verified against the vendor command reference, not inferred from this repository:

| Command | Vendor meaning | Domain used here |
| --- | --- | --- |
| `TP <axis>` | tell position of the **encoder** | I1 (encoder counts) → F5, F10 |
| `RP <axis>` | report the **commanded reference** position | I2 (drive/reference counts) → F4, F11 |
| `PA <axis>=n` | position absolute, the final destination of the next move | I2 (reference counts) → F3, F8 |
| `DP <axis>=0` | defines the current position; vendor states it sets both the TP and RP values | zero reference for both domains |
| `DE <axis>=0` | defines the auxiliary (dual) encoder position | zero reference for the dual loop |

Because `PA` and `RP` share one density by construction (F6/F7), the commanded operand and the
reference readback need **no additional derived conversion** between them — F7 is 1 — while `TP`
always needs F2. That is the whole relationship: one derived ratio, applied to one of the two
readbacks, and the identity stated explicitly for the other.

## 7. Every conversion and every initialisation command in this product

| Surface | What it does with the conversion |
| --- | --- |
| `ManualMotionPulseDomain` | holds I1–I4 and derives F1–F11; refuses to exist if any input is missing or ≤ 0 |
| `ManualMotionAxisProfile` / `ManualMotionProfileParser` | reads I1–I5 per axis; rejects an unknown key, a non-numeric value, a zero, a negative, or more than seven decimal places |
| `ManualMotionSession` | reads the conversion only from the accepted engineering capture; refuses a Move whose axis has no conversion; compares only in RP counts (F8/F9 vs F10/F11) |
| `ManualMotionSnapshot` | publishes I1–I5 and the four derived values per axis, read-only, with the capture generation |
| `ManualMotionSimulatedPort` | reports simulated TP as the profile's own density multiple of RP; a profile change changes the readback with no source change |
| `ManualMotionCleaningPathBoundary` | maps `MOVE_TARGET_NOT_REACHED_READBACKS_AGREE` to the Move timeout family, so P1→P2 cannot advance on it |
| Initialisation commands issued | `SB`/`CB` (brake output), `AC`/`SP`/`DC` (profile), `PA` (target), `BG` (begin), `HM`/`HV` (home), `DP`/`DE` (zero), and the interrogation `TP`, `RP`, `MG _TS<axis>`, `MG @OUT[n]`. No gearing (`GA`/`GR`) and no `MT`/`PF` command is issued or interpreted by this product, so no hidden count-density change is introduced by WJSS itself. |

## 8. What this document does not authorize

It does not declare the conversion finished in the physical sense. It declares the model exact,
per-axis, factor-free and fail-closed, and it states the one empirical question that remains. Hardware
remains **NOT EXECUTED**, and the prerequisite for a hardware re-trial is recorded in the change
report that accompanies this document.
