# WJSS — Physical Hardware Execution Runbook (PR #45 gate closure) — 2026-11-07

**Owner authority:** หน้างานจริงอนุมัติโดย Owner การรันนี้คือ PRODUCTION-READY ไม่ใช่แบบฝึกหัด
**Head of record:** `608aa3b35f7ae7c0b6d16ef6842ae7e19f6e8f83` (PR #45 — Owner-local 1787/1787, build 0W/0E)
**Executables:** `<repo>\apps\runtime\bin\Release\net10.0\Wjss.Runtime.exe` (build สดที่ head นี้แล้ว — ห้าม build ใหม่ระหว่างขั้น)
**Scripts เต็มรูปแบบ (guarded lifecycle):** `docs/TRIAL_A_B_HARDWARE_EXECUTION_PACKAGE.md` §4.4 (Trial A), §5.x (Trial B),
`docs/ACCELERATED_CLEANING_DRY_TRIAL_PACKAGE.md` §4–§9 (integrated dry) — runbook นี้คือลำดับ master + ผลที่คาด; ถ้ารัน script จาก package ให้ override `$ExpectedHead` เป็น SHA ปัจจุบันของ branch (package เดิม pin `c9c4a2d0` — เก่าแล้ว)

## 0. Gate register — สิ่งที่แต่ละขั้นปิด

| HOLD ที่ค้างอยู่ | ปิดที่ขั้น |
|---|---|
| REAL WAGO ENDPOINT CONNECTIVITY: NOT EXECUTED | **Stage A** |
| Pump-ready rule บนสัญญาณจริง (AI-003+, strict > setpoint) | **Stage A** |
| Polarity commissioning DI-004/DI-005 (uncommissioned ⇒ fail closed) | **Stage B** |
| REAL FIELD-DEVICE ACTUATION (IV1 coil + FC01 readback) | **Stage C** |
| Pump lifecycle + selection/enable/run + gate + cleanup บน hardware | **Stage D** |
| WSB Upper/Lower limit-switch reclass (corrected workbook 2026-11-01) + process-image mirroring | **Stage E** |
| Motion WSB1 no-load Stop trial (เคย prepare ไว้เท่านั้น) | **Stage F — optional** |

## 1. กฎความปลอดภัยหน้างาน (ห้ามข้าม)
1. E-Stop ทดสอบก่อนเริ่ม 1 ครั้ง; ใครอยู่นอกตู้ต้องรู้ตำแหน่ง E-Stop
2. Stage A, B, E = **อ่านอย่างเดียว** — ห้ามมีใครสั่งปั๊มจาก Panel ตอนอ่านยกเว้นที่ขั้นระบุ "pump RUN (local)"
3. Stage C/D = มี coil ทำงานจริง —ยืนยันไม่มีคนอยู่หน้า WJ head, วาล์ว-manual ทุกตัวอยู่ในตำแหน่งที่ Owner ตัดสินใจแล้ว (§6 ของ package ACCELERATED — "DRY PHYSICAL ARRANGEMENT — OWNER DECISION" ให้บันทึกการตัดสินใจลงสมุดหน้างาน)
4. Water supply: ปิด/ล็อกตามการตัดสินใจ dry arrangement ของ Owner; ปั๊มห้ามดูดน้ำถ้า Owner ยังไม่สั่ง
5. หยุดทันที (กด E-Stop แล้วโทรแจ้งกัน) เมื่อ: เสียงมอเตอร์ผิดปกติ / กลิ่นไหม้ / แรงดันโชว์ > ค่าที่ Owner ตั้งเป็นเพดานเอง / การสั่นผิดรูป แล้ว **ค่อย** บันทึก exit code + log ภายหลัง — ความปลอดภัยนำหลักฐานเสมอ
6. ห้ามแก้ environment variable ระดับ User/Machine ของ Windows — ตั้งเฉพาะใน PowerShell process ที่รันเท่านั้น

## 2. Master prep (ทุก stage เริ่มด้วย block นี้เสมอ — หน้าต่าง PowerShell เดิม, ตามลำดับ)
```powershell
cd <repo>
# hygiene: ล้าง WJSS_* + TargetPath ของโพรเซสนี้
Get-ChildItem Env: | Where-Object { $_.Name -like 'WJSS_*' } | ForEach-Object { Remove-Item ("Env:" + $_.Name) }
Remove-Item Env:TargetPath -ErrorAction SilentlyContinue
# identity check — ต้องตรงกับ head ของ PR ตอนนี้ ไม่ใช่ค่าที่ pin ใน package เก่า
(git rev-parse HEAD)                      # ต้องได้ SHA ที่คุณเพิ่ง pull
(git status --porcelain)                  # ต้องว่าง
$EXE = ".\apps\runtime\bin\Release\net10.0\Wjss.Runtime.exe"
Test-Path $EXE                             # True
$EV  = "$HOME\wjss-evidence\2026-11-07"    # โฟลเดอร์หลักฐานส่วนตัว — ห้าม commit
New-Item -ItemType Directory -Force $EV | Out-Null
```
- ค่า site-specific (IP/PORT/UNIT/SETPOINT จริง) ใช้ตอนพิมพ์เข้า console เท่านั้น **ห้ามเขียนลงไฟล์ที่จะ commit หรือ paste ให้ผมทั้งก้อน** — ก่อนส่ง log ให้ scrub: `(Get-Content $log) -replace '\b\d{1,3}(\.\d{1,3}){3}\b','<IP>' | Set-Content $log`
- Consent phrases: **ห้ามพิมพ์ลงเอกสาร/แชท** — Owner อ่านค่าจริงจาก source: `packages/contracts/ProcessIoContracts.cs` (คลาส `ProcessIoConsentPhrases`, ใช้ constant name ที่ระบุแต่ละ stage; เปรียบเทียบ ordinal-exact)

## 3. Stage A — Trial A: physical Pump-ready READ-ONLY (`PhysicalDiAiRead`)
```powershell
$env:WJSS_DEVICE_PROFILE            = 'TEST_HARDWARE'
$env:WJSS_PUMP_READY_READ_TRIAL     = 'AI-003_ONLY'
$env:WJSS_PROCESS_IO_HOST           = '<IP ของ T8 ที่หน้างาน>'
$env:WJSS_PROCESS_IO_CONSENT        = '<ค่า ProcessIoConsentPhrases.PhysicalDiAiRead>'
$env:WJSS_PROCESS_IO_CATALOGUE      = (Resolve-Path '.\docs\evidence\process-io-inventory-2026-10-22.csv').Path
$env:WJSS_PUMP_READY_SETPOINT_BAR   = '15'
$env:WJSS_PUMP_READY_FRESHNESS_MS   = '2000'
& $EXE 2>&1 | Tee-Object "$EV\trialA-off.log"; "EXIT=$LASTEXITCODE"
```
- **A1 (ปั๊มหยุด):** คาด `verdict` เป็น "not ready / below setpoint" + exit **0**; zero-write เชิงโครงสร้าง — ยืนยันว่าไม่มี coil/register ใดขยับ (สังเกตที่ตู้) ถ้า `connect refused/timeout` → connectivity FAIL: ตรวจ `WJSS_PROCESS_IO_HOST/PORT/UNIT_ID`, firewall, แล้วรันซ้ำ **ครั้งเดียว** (bounded, no retry loop; repeat ได้เฉพาะเมื่อแก้ config แล้ว)
- **A2 (ปั๊ม RUN จาก Panel — ผู้ปฏิบัติ: Owner เท่านั้น):** รอแรงดันนิ่ง แล้วรันคำสั่ง `& $EXE ... trialA-on.log` ซ้ำหนึ่งครั้ง: คาด reading ผ่าน freshness และ `PumpReady=true` (strictly > 15 bar) — ตัวเลข bar ที่เห็นให้ **บันทึกในสมุดหน้างานเท่านั้น**
- Gate ปิดเมื่อ: A1+A2 exit 0 + verdict ตรงคาดสองฝั่ง ⇒ connectivity + AI-003 signal path + strict-greater rule บนของจริง ✓

## 4. Stage B — Polarity commissioning (DI-004/DI-005)
1. อ่านสถานะ DI-004/DI-005 ตอนปั๊มหยุด (Trial A log ไม่มี DI — ใช้ Modbus diagnostic ที่ Owner มี หรืออ่านจาก log ของ Stage C/D ที่ print feedback state)
2. อ่านตอนปั๊ม RUN (จาก Panel): DI-004 ต้องเปลี่ยนสถานะจริง — จดว่า "RUN ⇒ level HIGH หรือ LOW"; DI-005 ตอนปกติต้องนิ่งฝั่ง inactive
3. บันทึกตาราง: `RunningPolarity = ACTIVE_HIGH|ACTIVE_LOW`, `FaultPolarity = ...` ลงสมุด — ค่านี้คือสิ่งที่ Stage D จะป้อน (ถ้ายังไม่ commission, Product จะ **refuse Start fail-closed ตามที่ออกแบบไว้** — ไม่ใช่บั๊ก)
4. ถ้าไม่สามารถทำให้ DI-004 กระดิกได้จริง (สาย/คอนแทกเตอร์) ⇒ Stage D ยังทำไม่ได้ — บันทึกไว้แล้วข้ามไป Stage F ได้, อย่าฝืน

## 5. Stage C — Trial B: IV1 OPEN/CLOSE single-DO (`PhysicalDoSingle`)
```powershell
Get-ChildItem Env: | Where-Object { $_.Name -like 'WJSS_*' } | ForEach-Object { Remove-Item ("Env:" + $_.Name) }
$env:WJSS_DEVICE_PROFILE             = 'TEST_HARDWARE_DRY'
$env:WJSS_DRY_IV_TRIAL_MODE          = 'SOFTWARE_SIMULATED_AI003'
$env:WJSS_DRY_IV_TRIAL_VALVE         = 'IV1'
$env:WJSS_DRY_IV_TRIAL_PUMP_STOPPED  = 'PUMP_STOPPED_OWNER_ATTESTED'   # Owner ยืนยันด้วยสายตาว่าปั๊มหยุดจริงก่อนพิมพ์
$env:WJSS_PROCESS_IO_HOST           = '<IP>'
$env:WJSS_PROCESS_IO_CONSENT        = '<ค่า ProcessIoConsentPhrases.PhysicalDoSingle>'
$env:WJSS_PROCESS_IO_CATALOGUE      = (Resolve-Path '.\docs\evidence\process-io-inventory-2026-10-22.csv').Path
$env:WJSS_PUMP_READY_SETPOINT_BAR   = '15'
$env:WJSS_PRESSURE_LOW_BAR          = '<Owner ตั้ง>'
$env:WJSS_PRESSURE_HIGH_BAR          = '<Owner ตั้ง>'
$env:WJSS_VALVE_SUPERVISION_MS      = '5000'
& $EXE 2>&1 | Tee-Object "$EV\trialB.log"; "EXIT=$LASTEXITCODE"
```
- ลำดับที่คาดหวัง: initial-closed confirmed → OPEN dispatched → **coil ที่ตู้ขยับจริง + FC01 readback TRUE** → field confirmation → CLOSE dispatched → readback FALSE → closed confirmed → session CLOSED → exit **0**
- `IV_TRIAL_REFUSED_INITIAL_CLOSED_NOT_CONFIRMED` = วาล์วไม่ได้ปิดอยู่จริง → แก้หน้างานแล้วรันครั้งเดียวใหม่
- `..._FIELD_CONFIRMED` ไม่เกิด/`CONTRADICTORY` = สัญญาณ feedback ขัดกับการกระทำ → หยุด stage นี้ จด code ส่งผม **ห้าม retry เกิน 1 ครั้ง** (fail-closed by design)
- Gate ปิดเมื่อ: ACTUATION จริง + readback/mirror ตรง ⇒ "REAL FIELD-DEVICE ACTUATION" ✓ (+ หลักฐาน process-image half ของ mirror gate)

## 6. Stage D — Integrated Cleaning-Path DRY cycle (full consent ผ่าน `PhysicalDoSingle` dry gate)
เงื่อนไขเข้า: A ✓, B ✓ (polarities commissioned), C ✓
1. Owner ตัดสินใจ+บันทึก "DRY PHYSICAL ARRANGEMENT" และ "REPRESENTATIVE ROUTE" (package ACCELERATED §6, §7) — ถ้า TwinCAT/WSB controller พร้อม ให้ set ทั้ง 7 motion variables ตาม §4 "Motion (Gate A)" ของ package นั้น (manual-motion consent, profile file, endpoint, lib dir + 2 digests)
2. Env เพิ่มจาก block ของ C (ลบ `WJSS_DRY_IV_TRIAL_*` ก่อน!):
```powershell
$env:WJSS_DRY_COMMAND_MODE           = 'TEST_HARDWARE_DRY'
$env:WJSS_PUMP_RUNNING_POLARITY      = '<จาก Stage B>'   # ACTIVE_HIGH | ACTIVE_LOW
$env:WJSS_PUMP_FAULT_POLARITY        = '<จาก Stage B>'
$env:WJSS_CLEANING_PATH_STORE        = '<path owner-local>'
$env:WJSS_SENSOR_PARAMETER_CSV       = '<path owner-local>'
$env:WJSS_CLEANING_SENSOR_ID         = '<canonical Sensor id ของรอบนี้>'
& $EXE 2>&1 | Tee-Object "$EV\drycycle.log"; "EXIT=$LASTEXITCODE"
```
(ห้าม set `WJSS_PUMP_READY_READ_TRIAL`/`WJSS_DRY_IV_TRIAL_MODE` พร้อมกัน — selector ขัดกัน = refuse ตามแบบ)
- ผลคาดหวัง: console รายงาน ordered steps แล้ว `steps=<n>` + exit **0** (terminal result); exit 3 = ถึง bounded step budget — **ห้าม re-run เพื่อลุ้น**; ให้ส่ง log ทั้งก้อน
- ยืนยันด้วยสายตาที่ตู้: selection pair → DO-003 TRUE **ก่อน** DO-007 TRUE; AO ขึ้นทีละ 2 mA/1000 ms; ตอนจบ: ramp ลง → DO-007 FALSE → DO-003 FALSE → not-running → ownership released (log/เสียง relay เป็นพยาน)
- ถ้ากลางรอบมี fault จริง (E-stop/เกินแรงดัน): Product ต้องพาเข้า stop path เอง — จด exit + โค้ด refusal/terminal state ส่งผม นี่คือผล **บวก** ถ้าพฤติกรรมตรงกับ state machine

## 7. Stage E — WSB limit switches (Upper/Lower reclass) + mirror
1. สั่งเลื้อน WSB ด้วย manual ของเครื่อง (นอกเส้นทาง WJSS) ไป **Upper end**: คาด DI addresses FC02 `20..27` ฝั่งที่ active ตรง label "Upper" ของ inventory ปัจจุบัน (`docs/evidence/...csv` — แก้ reclass แล้ว)
2. ไป **Lower end**: คาด `28..35` ฝั่งตรง label "Lower"
3. ตอนพัก (axes ค้าง): คาดทุกตัว inactive — บันทึก 3 ภาวะลงตารางผล
4. กระบวนการนี้คือหลักฐานปิด "process-image mirroring (DEFERRED)" ฝั่ง DI; ฝั่ง DO พิสูจน์แล้วจาก FC01 readback ของ Stage C/D

## 8. Stage F (optional) — Motion WSB1 no-load Stop trial
รันตาม `docs/MOTION_STOP_TRIAL_RUNBOOK.md` เฉพาะเมื่อหน้างาน TwinCAT พร้อมและ Owner ประกาศเอง; ไม่พร้อม = ข้าม, gate คง NOT EXECUTED (ไม่ใช่ความล้มเหลวของ PR นี้)

## 9. Standard stop / cleanup ทุกกรณี
- ปกติ: trial เองจะปิด session, de-energise ตามลำดับมาตรฐาน (DO-007 F → verify → DO-003 F → verify → wait not-running → release ownership) ก่อน process ออก
- ผิดปกติ/ค้าง: ที่ตู้ — ตัด DO-007 (Run) ก่อน, ยืนยัน, แล้ว DO-003 (Enable), ยืนยัน, รอปั๊มนิ่ง, ปลด mode อัตโนมัติที่ Panel; ห้ามถอดสายขณะ coil มีไฟ
- หลังทุก stage: `Get-ChildItem Env: | Where-Object Name -like 'WJSS_*' | Remove-Item` (log ไม่มีค่า sensitive นอกจาก fingerprint แบบ hash-prefix ตาม redaction ของ Product เอง)

## 10. Exit-code / refusal map (ย่อ)
| Exit | ความหมาย | Action |
|---|---|---|
| 0 | verdict บรรลุตาม contract | บันทึกผล ✓ |
| 2 | typed refusal ก่อน bind (profile/endpoint/consent/catalogue/attestation) | แก้ config แล้วรันครั้งเดียวใหม่ |
| 3 (Stage D) | bounded step budget หมด — ไม่มี terminal | ส่ง log ทั้งก้อน ห้าม re-run |
| 4 | startup refused (selector ผิด/ชนกัน) | ตรวจ hygiene block ให้ตรง stage |
| `*_REFUSED_READ_ONLY_CONSENT` / `_CONSENT_MISSING` / mismatch | phrase ไม่ตรง ordinal-exact | พิมพ์ใหม่จาก constant source |
| `IV_TRIAL_REFUSED_*_NOT_FIELD_CONFIRMED` / `CONTRADICTORY` | การกระทำขัดกับ feedback | หยุด stage — นี่คือ fail-closed ทำงาน |
| `PUMP_READY_TRIAL_REFUSED_ENDPOINT` / `PROCESS_IO_ENDPOINT_REJECTED` | endpoint ผิด/ถึงไม่ได้ | ตรวจ IP/ firewall |

## 11. ตารางผลที่ต้องส่งผม (หลัง scrub แล้วเท่านั้น)
```text
Stage A1 off:   exit=..  verdict_line=<บรรทัดสรุป โดยไม่ตัดบริบท>
Stage A2 run:   exit=..  ready_seen=true/false  (bar = บันทึกส่วนตัว)
Stage B:        Running=<HIGH|LOW> Fault=<HIGH|LOW>  method=<Modbus diag|trial log>
Stage C:        exit=..  open_confirmed=.. close_confirmed=.. readback=..
Stage D:        exit=..  steps=.. terminal=<state>   order_verified=DO-003-before-007? down-ramp?
Stage E:        upper_end=.. lower_end=.. idle_all_low=true/false
Stage F (ถ้าทำ): runbook result=..
log files: scrubbed? YES/NO   evidence folder: <ชื่อเฉพาะ, ไม่ใช่ path เต็ม>
```
