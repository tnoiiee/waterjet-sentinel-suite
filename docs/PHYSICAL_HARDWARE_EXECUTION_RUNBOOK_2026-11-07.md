# WJSS Physical Hardware Execution Runbook v2 — 2026-11-07

> เอกสารนี้แทนที่รุ่นแรก (รุ่นแรกเขียนงงและให้คนรันหาข้อมูลมากรอกเอง) — v2 ออกแบบให้ **copy → paste → รัน** เท่านั้น
> ผลลัพธ์ทุก stage มีตัวขับตรวจให้เอง และก้อนข้อความสำหรับส่งให้ผมถูก scrub IP ให้อัตโนมัติ
> ตัวขับ = ไฟล์ PowerShell ที่ commit ใน repo (ดูข้อ 0.3) ไม่ใช่บล็อกที่ต้อง copy จาก markdown อีกต่อไป

## 0. ขั้นตอนที่ต้องรัน (copy ทั้งบล็อคไปวางได้เลย)

> โค้ดทั้งหมดอยู่ใน `tools/hardware-runbook/wjss-field-driver.ps1` (ไฟล์จริงใน repo) — บล็อก `powershell` ในเอกสารนี้จึงมีแต่คำสั่งสั้นที่รันได้ทันที

### ข้อ 0.1 — ดึงโค้ด + build + รัน .NET matrix ทั้งหมด (ทำทุกครั้งที่ผมแจ้งว่า commit ใหม่แล้ว)

```powershell
$Repo = (Get-ChildItem -Path $HOME -Recurse -Depth 3 -Filter 'WaterJetSentinelSuite.sln' -ErrorAction SilentlyContinue | Select-Object -First 1).DirectoryName
if (-not $Repo) { $Repo = Read-Host 'Repo path (folder containing WaterJetSentinelSuite.sln)' }
Set-Location $Repo
$env:DOTNET_NOLOGO = '1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
git fetch origin
git rev-parse --short HEAD
# HEAD must be the sha I reported in my last message; if it is older, pull:
git pull --ff-only origin arena/b179a92e-waterjet-sentinel-suite
# if the pull refuses (not fast-forward, or local changes): STOP and send me the message. Never --force, never reset.
dotnet restore WaterJetSentinelSuite.sln
dotnet build WaterJetSentinelSuite.sln -c Release --no-restore
dotnet test WaterJetSentinelSuite.sln -c Release --no-build
```

ต้องเห็นอะไร (ส่งตัวเลขจริงมาให้ผม 3 บรรทัดนี้):

| คำสั่ง | บรรทัดที่คาดหวัง |
| --- | --- |
| `dotnet build` | `0 Warning(s)` และ `0 Error(s)` |
| `dotnet test` (ทั้ง matrix) | `Failed: 0` และ `Skipped: 0` (รอบก่อนที่ `608aa3b` = `Passed: 1787`; รอบนี้ N เปลี่ยนได้เล็กน้อย — รายงานตัวเลขจริง) |
| ถ้ามี Failed > 0 | **ห้ามแก้ test/tolerances เอง** — ส่งชื่อ test + ข้อความ assert ทั้งก้อนให้ผม |

### ข้อ 0.2 — ซ้ำเฉพาะจุดที่การแก้ scaling กระทบ (รันในหน้าต่างเดียวกับข้อ 0.1 เพราะใช้ `$Repo` ร่วมกัน)

```powershell
Set-Location $Repo
dotnet test tests/runtime.tests/Wjss.Runtime.Core.Tests.csproj      -c Release --no-build --filter "FullyQualifiedName~ProcessIo"
dotnet test tests/api.tests/Wjss.Runtime.Api.Tests.csproj           -c Release --no-build --filter "FullyQualifiedName~ProcessIoPumpReadyReadTrial"
dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj     -c Release --no-build
```

บล็อคที่สามคือ fixture parity — ผมตรวจ `tests/integration/FixtureGenerator.cs` แล้วว่ามันใส่ `PumpOutletPressureBar = 16.0` เป็นค่าคงที่ ไม่เรียก conversion chain ⇒ **ต้องเขียวโดยไม่แตะอะไร** และ **ห้ามตั้ง `WJSS_UPDATE_FIXTURES=1`** เด็ดขาดถ้าผมยังไม่สั่ง (มันเขียนทับ golden)

### ข้อ 0.3 — เตรียมหน้าต่างงานหน้างาน (วางครั้งเดียวต่อ 1 หน้าต่าง PowerShell)

ตัวขับงานหน้างาน**ไม่ได้อยู่ในไฟล์ markdown นี้อีกต่อไป** — มันคือไฟล์จริงที่ commit ไว้ใน repo: `tools/hardware-runbook/wjss-field-driver.ps1`
(เหตุผล: บล็อกที่ต้อง copy จากไฟล์ .md ต้องหาทางเลี่ยง syntax ของ markdown ซึ่งเคยทำพังมาแล้วจริง ๆ — ทั้ง backtick ที่กลายเป็น
ตัว escape ใน PowerShell และการประกอบอักขระเป็นสายเพื่อสร้าง fence (เคยใช้ `char` คูณจำนวนครั้ง ซึ่ง PowerShell ไม่มี operator นี้)
เมื่อโค้ดเป็นไฟล์ `.ps1` จริงใน repo จึงไม่มีเรื่องประกอบสายเหลืออยู่เลย และ **PowerShell จะ parse โค้ดทั้งไฟล์ตอน dot-source**
— typo, type ที่ไม่มีจริง, operator ที่ไม่มี จะโผล่ทันทีตอนโหลด ไม่ใช่ตอนยืนอยู่หน้าปั๊ม)

วางสามบรรทัดนี้ (ถ้ายังไม่ได้เปิดหน้าต่างใหม่ ให้ทำข้อ 0.1 ก่อน เพราะใช้ `$Repo` ร่วมกัน):

```powershell
$Repo = 'C:\dev\waterjet-sentinel-suite'      # same value you used in 0.1
. (Join-Path $Repo 'tools\hardware-runbook\wjss-field-driver.ps1')   # วงเล็บครอบจำเป็น: dot-source ต้องการ expression ไม่ใช่ชื่อ cmdlet
Test-WjssDriver
Export-WjssStageFiles
```

`Test-WjssDriver` ไม่ใช่ formal check — มัน**รันโค้ดจริงบน PowerShell เครื่องคุณ** (ตัวเดียวกับที่จะรัน stage):

- parse ไฟล์ตัวขับและ parse ไฟล์ `stage-*.ps1` ทั้ง 11 ไฟล์ที่ export ออกมา (syntax ผิด = เจอตรงนั้นเลย)
- ตรวจว่า stage ครบ 11 ตัว ไม่มี consent literal/endpoint literal หลงอยู่ในโค้ดของ stage
- รัน `& <exe>` จริงใน mode startup-refusal (ไม่แตะ device) แล้วดูว่า exit code/ refusal มาจริง
- เทียบตัวอ่าน log, ตัวตัดสิน verdict, ตัว scrub IP กับ log สังเคราะห์ (บรรทัดที่มี `=` ซ้ำในค่า, junk line, key ซ้ำ, ค่าที่พิมพ์ตัวพิมพ์ใหญ่ต่างกันเลยต้องถูกจับ)
- ตรวจ frame ที่ใช้อ่านบิต: ความยาว 12 ไบต์, protocol id ต้องเป็น 0, length 6, start/quantity เป็น big-endian — พร้อมเคส reply ที่ device ตอบ exception / สั้นเกิน / function code ไม่ตรง
- เทียบ keys ที่ใช้ใน `-Expect`/`-Show` กับ keys ที่ trial นั้นพิมพ์จริง และ grep ชื่อ `WJSS_*` ทุกตัว + ค่าที่ผม assert ทุกตัวว่ามีอยู่ใน source ของ Product จริง

อะไรก็ตามที่ grep ไม่เจอ = `DRIVER SELF-TEST FAILED` แล้วตัวขับจะ**ไม่ปล่อยให้รัน stage**

ส่วนบรรทัด `Export-WjssStageFiles` คือตัวสร้าง `$env:USERPROFILE\wjss-evidence\<วันที่>\stage-<name>.ps1` ทั้ง 11 ไฟล์ ซึ่งเป็นสิ่งที่รันจริง (ห้ามแก้เอง)

หมายเหตุ: ในไฟล์ `tools/` ผมไม่พิมพ์ชื่อ protocol ของยี่ห้อ device (กฎ boundary-scan S3 ใน repo) — protocol ที่ใช้จริงคือ Modbus TCP FC01/FC02 ซึ่งเอกสารนี้เป็นคนอธิบายเอง ตัวขับเลยใช้คำว่า "device TCP port / unit id"

ค่าควบคุม: ตัวขับ**ถาม** repo path / IP / port / unit id / catalogue ตอน dot-source (port 502, unit 1, catalogue ใน repo — กด Enter เพื่อเอา default) ถ้าอยาก override หลังโหลดแล้ว:

```powershell
$script:Port = 502       # TCP port - the same value the poller reads and the trial is given
$script:UnitId = 1       # unit id (slave address)
$script:Cat  = 'C:\FULL\PATH\TO\YOUR\T8_CATALOGUE.csv'   # default: docs\evidence\process-io-inventory-2026-10-22.csv
```

catalogue ต้องเป็นสำเนาของคุณเองที่ path **นอก repo tree** (ตาม `docs/TRIAL_A_B_HARDWARE_EXECUTION_PACKAGE.md:148`) —
ตัวขับจะปฏิเสธ path ที่อยู่ใต้ `$Repo`

### ข้อ 0.4 — รัน stage ตามลำดับ (ห้ามข้าม A1 ซึ่งคือ "ปั๊มหยุด")

รันตามลำดับนี้ **ทีละบรรทัด** (ไฟล์ทั้ง 11 เกิดจาก `Export-WjssStageFiles` ในข้อ 0.3 — ห้าม copy โค้ดจากเอกสารนี้ไปรันเอง และห้ามแก้ไฟล์ `stage-*.ps1`) — A1 รันตอนปั๊มหยุด, A2 รันตอนปั๊มรัน, B off/on เช่นกัน, ห้ามรันคู่ที่ติดกันแล้วลืมเปลี่ยนสถานะปั๊ม:

```powershell
& "$script:Ev\stage-A1-off.ps1"     # pump STOPPED, exit 2 expected - that is the PASS
& "$script:Ev\stage-A2-run.ps1"     # pump RUNNING, exit 0 + pump_ready=TRUE
& "$script:Ev\stage-B-off.ps1"      # read DI-001..008, pump stopped
& "$script:Ev\stage-B-on.ps1"       # read DI-001..008, pump running - I diff the two
& "$script:Ev\stage-C-iv1.ps1"      # IV1 open/close, one actuation
& "$script:Ev\stage-D-pump.ps1"     # D variant 1: pump ladder only, NO valve boundary composed
& "$script:Ev\stage-D-pump-mv.ps1"  # D variant 2: the same ladder WITH the motorized main valve (block 1.27) - PASS at 16f78bc, valve bounds 45000 ms
& "$script:Ev\stage-D-sim.ps1"      # D variant 3: real IV + real path on a DECLARED pump pressure
& "$script:Ev\stage-D-dry.ps1"      # D variant 4 (wet): real pump + real IV, extra ack required
& "$script:Ev\stage-E-idle.ps1"     # all switches at rest: FC02 + FC01 together
& "$script:Ev\stage-E-moved.ps1"    # after moving each WSB switch by hand
```

แต่ละ stage พิมพ์บรรทัดสุดท้ายเป็น `RESULT <stage>: PASS` หรือ `RESULT <stage>: CHECK` — ส่งก้อนใต้เส้นประ cyan มาให้ผมทุกครั้ง

### ข้อ 0.5 — สิ่งที่ต้องส่งกลับมาให้ผมหลังทุก stage

1. ก้อนที่อยู่ใต้ `--- copy everything below to the assistant ---` (script พิมพ์ให้ + เก็บในไฟล์ `$script:Ev\stage-<name>.send.txt` ซึ่ง scrub IP แล้ว)
2. ถ้าผมขอ log ดิบ ให้ส่งจากไฟล์ `.log` / `.err.log` ในโฟลเดอร์เดียวกัน (raw log ไม่ถูก scrub เพราะไม่ออกจากเครื่องคุณ — ห้ามเอาไปโพสต์ที่อื่น)
3. ตัวเลข build/test จากข้อ 0.1
4. **เฉพาะ**เมื่อมี `CHECK`/`ERROR`: log ดิบทั้งไฟล์ `Get-Content $script:Ev\stage-A2.log -Raw` (ห้ามแก้ข้อความ ห้ามลบบรรทัด)

log ดิบทั้งหมดอยู่ใน `$env:USERPROFILE\wjss-evidence\<วันที่>\` — **ห้าม `git add` / ห้าม commit**

### ข้อ 0.6 — กติกาเวลาเจอ error

copy ข้อความ error ทั้งก้อน (บรรทัดแดง) ส่งผม แล้ว**ห้ามแก้ script เอง** รวมถึงห้ามแก้ไฟล์ `stage-*.ps1` ที่ export ออกมา (มันถูกสร้างใหม่จากตัวขับเสมอ และถ้าคุณแก้มัน `Test-WjssDriver` จะฟ้องว่าไม่ตรงกับตัวขับ) — ผมจะแก้ที่ตัวขับแล้ว commit ใหม่ให้เอง

**สถานะ scale ของ Pump pressure (แก้ที่ Product แล้ว):** ตัวแปลงตอนนี้คือ `bar = raw / 32767 × 40` (ช่อง AI‑003 ถูก config เป็น 4‑20 mA ⇒ `raw 0 = 4 mA` อยู่แล้ว) — ตัวอย่างจริงที่หน้างาน: `raw 580 = 4.283 mA = 0.708 bar` (ปั๊มหยุด, ปกติ), `raw 13631 = 10.656 mA = 16.6399 bar` (ปั๊มรัน) หลักฐานทั้งชุดอยู่ใน `docs/evidence/LEGACY_REV2_SCALING_CORRECTIONS_2026-11-07.md`

---

## 1. กฎความปลอดภัย (ห้ามข้าม)

- Stage **A, B, E = อ่านอย่างเดียว** ห้ามสั่งปั๊ม/วาล์วจาก Panel ระหว่างอ่าน ยกเว้นบรรทัดที่เขียนว่า "pump RUN"
- **C และ D** เป็นการสั่งงานจริง (actuation) — ห้ามรันถ้า **B ยังไม่ผ่าน** และ (สำหรับ D) ยังไม่ได้ตัดสินใจข้อ `docs/ACCELERATED_CLEANING_DRY_TRIAL_PACKAGE.md` §6/§7
- consent phrase พิมพ์จากค่าคงที่ใน `packages/contracts/ProcessIoContracts.cs` (`ProcessIoConsentPhrases.*`) เท่านั้น — ห้ามจดไว้ที่อื่น ห้ามถ่ายรูป และตัวขับจะลบตัวแปร env ของ consent ท้ายทุก stage เสมอ (`SetEnvironmentVariable(name, $null)` ในบล็อก `finally` — ลบแม้ stage จะ throw)
- IP ของ T8 ใช้ในหน่วยความจำของหน้าต่างนี้เท่านั้น — ไม่ถูกเขียนลงไฟล์ไหนใน repo และถูก scrub ออกจากก้อนที่ส่งผม
- ถ้าเกิดอะไรผิดปกติระหว่าง C/D: สั่งที่ Panel ตามลำดับ **Run (DO‑007) = FALSE ก่อน แล้ว Enable (DO‑003) = FALSE** (ห้ามกลับลำดับ) แล้วหยุดส่ง log

## 2. ตัวขับงานหน้างาน (`tools/hardware-runbook/wjss-field-driver.ps1`) — what is in it, so nothing here is a black box

ไฟล์เดียวรับผิดชอบทุกสิ่งที่เคยอยู่ในบล็อก §2 รุ่นเก่า และใน stage fence ทั้ง 8 บล็อก — **โค้ดมีที่มาที่เดียวคือ `.ps1` นี้**
ส่วน markdown นี้มีไว้สำหรับคำอธิบาย/เงื่อนไข/เกณฑ์เท่านั้น (ถ้าสองส่วนนี้ไม่ตรงกัน ถือว่า .ps1 ถูกและผมจะแก้ doc)

| function | หน้าที่ |
|---|---|
| `Set-WjssDevice` | ถาม repo path / IP / port / unit / catalogue ตอนโหลด (ถามซ้ำจนกว่าจะตอบครบ 3 ครั้งแล้ว throw) — catalogue ต้องอยู่นอก repo |
| `Test-WjssBuild` | เทียบเวลา: ถ้ามี `.cs` ใต้ `apps` หรือ `packages` ที่ใหม่กว่า exe จะ throw ให้ build ก่อน — แล้วคืนบรรทัด `HEAD + driver version + exe sha256(12 อักขระ) + เวลาที่ build` (sha นี้ใช้เทียบข้ามรอบได้ แต่ไม่ pinsายเป็นค่าคงที่ เพราะ build บนเครื่องคุณคนละเครื่องกับผม) |
| `Read-WjssConsent` | อ่าน consent แบบซ่อนตัวอักษร แปลงเป็น `SecureString` เก็บในหน่วยความจำเท่านั้น (ไม่มี path ให้แก้) |
| `Clear-WjssEnv` | ลบ `WJSS_*` ท้ายทุกบlock (ค่า test ซ้อนกันไม่ได้) |
| `Get-WjssKeyValuePairs` | parse log `key=value` (first-key-wins, `=` ในค่าใช้ได้, ทิ้งบรรทัดไม่มี `=`) |
| `Resolve-WjssVerdict` | เทียบ exit code + ทุก key ที่ต้องตาม; ค่าขึ้นต้น `~` = "ต้องลงท้ายด้วย" (ใช้กับบรรทัด `strict_comparison` ที่มีตัวเลข bar ปนอยู่) |
| `Save-WjssSend` | scrub dotted-quad → `<IP>` **ก่อน**เขียนไฟล์ แล้ว print ใต้เส้นประ cyan |
| `Invoke-WjssStage` | `Start-Process -Wait -PassThru` + redirect stdout/err เป็นไฟล์ (ไม่ pipe ผ่าน PowerShell ⇒ ไม่มี NativeCommandError), export env, `Clear-WjssEnv` + `Read-WjssConsent` อัตโนมัติ, ปิด env ใน `finally`, auto-attach 40 บรรทัดท้ายเมื่อ CHECK |
| `Read-WjssField` / `Confirm-WjssState` | อ่าน key เดี่ยวจาก log / ยืนยันสถานะปั๊มโดย Owner พิมพ์เอง (fail-closed) |
| `Read-WjssBits` / `Format-WjssBits` | FC02 (discrete inputs) / FC01 (coils) แบบ raw TCP — เจอ exception จาก device (code 1..4) แล้วรายงานเป็น error ไม่ใช่เอา payload มาเป็นค่าบิต |
| `New-WjssBitReadRequest` / `Resolve-WjssBitReadReply` | ประกอบ frame 12-byte และถอด reply แบบ pure function (ไม่แตะ socket) — `Test-WjssDriver` เทียบ byte ต่อ byte รวมทั้ง case exception/truncated/wrong-function |
| `Show-WjssStage` | พิมพ์โค้ดจริงของ stage (มี `-Copy` ที่ clipboard ใส่ `.` แทน `&` เพื่อให้วางรันตรง ๆ ได้) |
| `Export-WjssStageFiles` | เขียน stage `scriptล็อก` ออกเป็นไฟล์ `.ps1` 8 ไฟล์ + บรรทัด guard ที่บอกให้คุณกลับมา dot-source ถ้าเผลอรันก่อนโหลด |
| `Test-WjssDriver` | self-test ที่รันบน PowerShell เครื่องคุณ (ดูข้อ 0.3) |

กติกาที่ฝังในตัวขับ (ห้ามแก้เพื่อให้ผ่าน):
- `Invoke-WjssStage` เรียก `[string](Get-Content -LiteralPath $logFile -Raw)` = **0 หรือ 1 ไฟล์เท่านั้น** — ถ้า log หายคือ ERROR ไม่ใช่ CHECK
- `-ExpectExit` ตัดสินก่อน ถ้าไม่ตรงก็ยัง parse log ต่อ (ต้องรู้ *ว่า* trial ปฏิเสธด้วย reason อะไร) — เหมือนเดิม
- consent ถูก `SetEnvironmentVariable(name, $null)` ท้ายทุก stage และถ้า `Confirm-WjssState` ยังไม่ตอบตรง ตัวขับจะไม่เรียก exe เลย
- ตัวขับไม่ print consent, ไม่ print IP, ไม่เขียน IP ลงไฟล์ repo — ก้อนที่ส่งผม scrub แล้วเสมอ

## 3. Stage A — Trial A: อ่าน Pump-ready บนสัญญาณจริง (อ่านอย่างเดียว)

ปิด gate: `REAL WAGO ENDPOINT CONNECTIVITY` + `Pump-ready rule บนสัญญาณจริง (strict >)` ทั้งสองฝั่ง

**A1 — ปั๊มหยุด** (คาดว่า **exit 2** = typed refusal เพราะ 0.7 bar ≤ 15 bar →นี่คือ PASS)

```powershell
& "$script:Ev\stage-A1-off.ps1"
```
สิ่งที่บล็อกทำจริง (ตัวโค้ดคือ `stage-A1-off.ps1` ที่ export มาจากตัวขับ — ดูด้วย `Show-WjssStage -Name 'A1-off'`; ข้อความนี้ generate จากโค้ดจริงของตัวขับ จึงไม่เขียนค้าง):

- ต้องยืนยันกับ Owner ก่อน: พิมพ์ `STOPPED` ตามที่ prompt ทวงถาม ( prompt: A1: the pump must be STOPPED at the Panel (read-only trial) ) — ถ้าไม่ตอบตรงตัวบล็อกจะ**ไม่เรียก exe เลย**
- consent ที่บล็อกขอ: `PhysicalDiAiRead` (อ่านแบบซ่อน, ลบทิ้งหลังจบบล็อก)
- exit code ที่ยอมรับ: `2` เท่านั้น
- env ที่บล็อกตั้ง: `WJSS_PUMP_READY_READ_TRIAL=AI-003_ONLY`, `WJSS_PUMP_READY_SETPOINT_BAR=15`, `WJSS_PUMP_READY_FRESHNESS_MS=2000`
- key ที่**ต้องตรง**ตามนี้ (17 คู่): `trial_code=PUMP_READY_REFUSED_AT_OR_BELOW_SETPOINT`, `source_tag=AI-003+`, `evidence_kind=PHYSICAL`, `provenance=PHYSICAL_READ`, `quality=GOOD`, `fresh=TRUE`, `physical_ai003_verified=TRUE`, `pump_ready=FALSE`, `physical_pump_verified=FALSE`, `strict_comparison` ลงท้ายด้วย `=> FALSE`, `session_state=CLOSED`, `transport_released=TRUE`, `ai002_used_for_decision=FALSE`, `iv_command_executed=FALSE`, `motion_executed=FALSE`, `waterjet_command_executed=FALSE`, `write_authorization_created=FALSE`
- key ที่แนบมาให้ผมดูด้วย (ไม่ใช้ตัดสิน pass/fail): `raw_value`, `engineering_bar`, `engineering_unit`, `configured_setpoint_bar`, `session_generation`, `observation_diagnostic`

**A2 — ปั๊ม RUN จาก Panel (Owner เท่านั้น):** รอแรงดันนิ่งก่อน แล้วรันซ้ำ — คาด **exit 0** + `pump_ready=TRUE`

```powershell
& "$script:Ev\stage-A2-run.ps1"
```
สิ่งที่บล็อกทำจริง (ตัวโค้ดคือ `stage-A2-run.ps1` ที่ export มาจากตัวขับ — ดูด้วย `Show-WjssStage -Name 'A2-run'`; ข้อความนี้ generate จากโค้ดจริงของตัวขับ จึงไม่เขียนค้าง):

- ต้องยืนยันกับ Owner ก่อน: พิมพ์ `RUN` ตามที่ prompt ทวงถาม ( prompt: A2: the pump must be RUNNING at the Panel and the pressure must look settled ) — ถ้าไม่ตอบตรงตัวบล็อกจะ**ไม่เรียก exe เลย**
- consent ที่บล็อกขอ: `PhysicalDiAiRead` (อ่านแบบซ่อน, ลบทิ้งหลังจบบล็อก)
- exit code ที่ยอมรับ: `0` เท่านั้น
- env ที่บล็อกตั้ง: `WJSS_PUMP_READY_READ_TRIAL=AI-003_ONLY`, `WJSS_PUMP_READY_SETPOINT_BAR=15`, `WJSS_PUMP_READY_FRESHNESS_MS=2000`
- key ที่**ต้องตรง**ตามนี้ (14 คู่): `trial_code=PUMP_READY_ACCEPTED_PHYSICAL`, `evidence_kind=PHYSICAL`, `provenance=PHYSICAL_READ`, `quality=GOOD`, `fresh=TRUE`, `physical_ai003_verified=TRUE`, `pump_ready=TRUE`, `physical_pump_verified=TRUE`, `strict_comparison` ลงท้ายด้วย `=> TRUE`, `session_state=CLOSED`, `transport_released=TRUE`, `iv_command_executed=FALSE`, `motion_executed=FALSE`, `write_authorization_created=FALSE`
- key ที่แนบมาให้ผมดูด้วย (ไม่ใช้ตัดสิน pass/fail): `raw_value`, `engineering_bar`, `engineering_unit`, `configured_setpoint_bar`, `quality`, `fresh`, `session_generation`

- **A2 ได้ exit 2 + `engineering_bar` ≤ 15** → ปกติ (ปั๊มยังไม่ขึ้นแรงดัน) รอแล้วรันซ้ำได้
- **A2 ได้ exit 2 แต่ `engineering_bar` > 15** → นี่แหละผิดปกติจริง หยุดและส่ง log ให้ผม
- **A1 ได้ exit 0** → ผิดปกติ (gate ต้อง refuse ใต้ setpoint) หยุดและส่ง log
- exit ladder: `0` = Accepted เท่านั้น · `2` = refusal หลัง bind session · `3` = ReadTimeout · `4` = startup refusal (env/consent/profile)

## 4. Stage B — ขั้วสัญญาณ (polarity) DI‑004 / DI‑005 (อ่านอย่างเดียว)

ปิด gate: `WJSS_PUMP_RUNNING_POLARITY` / `WJSS_PUMP_FAULT_POLARITY` (ถ้ายังไม่commission ⇒ Stage D จะถูก refuse — นั่นคือ fail-closed ทำงาน)
ไม่มี tool ใดใน Product ที่พิมพ์สถานะ DI แบบต่อบิต — ใช้ `Read-WjssBits` ในตัวขับ (FC02/FC01 อ่านอย่างเดียว ไม่มี path สำหรับเขียน)

```powershell
& "$script:Ev\stage-B-off.ps1"
```
สิ่งที่บล็อกทำจริง (ตัวโค้ดคือ `stage-B-off.ps1` ที่ export มาจากตัวขับ — ดูด้วย `Show-WjssStage -Name 'B-off'`; ข้อความนี้ generate จากโค้ดจริงของตัวขับ จึงไม่เขียนค้าง):

- อ่านบิต: FC02 (read discrete inputs) ที่ start=0 count=8 ⇒ address 0..7
- ผลอ่านแต่ละช่วง print ผ่าน `Save-WjssSend` (scrub แล้ว) — ถ้า device ไม่รองรับ function code นั้น บรรทัดนั้นจะขึ้น `READ FAILED - protocol exception N` (N คือ code ที่ device ตอบ แบบทศนิยม) ซึ่งผมใช้ตัดสิน gate ได้ **ห้ามแก้ไฟล์ที่ export มาแล้ว**

สั่ง **ปั๊ม RUN** จาก Panel รอให้นิ่ง แล้วรันบล็อคเดิมซ้ำ (เปลี่ยนชื่อเป็น `B-on`):

```powershell
& "$script:Ev\stage-B-on.ps1"
```
สิ่งที่บล็อกทำจริง (ตัวโค้ดคือ `stage-B-on.ps1` ที่ export มาจากตัวขับ — ดูด้วย `Show-WjssStage -Name 'B-on'`; ข้อความนี้ generate จากโค้ดจริงของตัวขับ จึงไม่เขียนค้าง):

- อ่านบิต: FC02 (read discrete inputs) ที่ start=0 count=8 ⇒ address 0..7
- ผลอ่านแต่ละช่วง print ผ่าน `Save-WjssSend` (scrub แล้ว) — ถ้า device ไม่รองรับ function code นั้น บรรทัดนั้นจะขึ้น `READ FAILED - protocol exception N` (N คือ code ที่ device ตอบ แบบทศนิยม) ซึ่งผมใช้ตัดสิน gate ได้ **ห้ามแก้ไฟล์ที่ export มาแล้ว**

ตัดสินจากสองก้อนที่ส่งผม (คุณไม่ต้องแปลเอง ผมแปลให้): **DI‑004** ต้องเปลี่ยนระดับเมื่อ RUN, **DI‑005** ต้องอยู่นิ่งฝั่ง inactive
ค่าที่จะใช้ใน Stage D = `ACTIVE_HIGH` ถ้าระดับ "ทำงาน" คือ 1, `ACTIVE_LOW` ถ้าคือ 0

## 5. Stage C — Trial B: IV1 OPEN/CLOSE ครั้งเดียว (actuation ครั้งแรก)

ปิด gate: `physical IV command + FC01 read-back` · ต้องมี B ผ่านแล้ว และ **ปั๊มต้องหยุด** (trial ต้องการ attestation นี้)

```powershell
& "$script:Ev\stage-C-iv1.ps1"
```
สิ่งที่บล็อกทำจริง (ตัวโค้ดคือ `stage-C-iv1.ps1` ที่ export มาจากตัวขับ — ดูด้วย `Show-WjssStage -Name 'C-iv1'`; ข้อความนี้ generate จากโค้ดจริงของตัวขับ จึงไม่เขียนค้าง):

- ต้องยืนยันกับ Owner ก่อน: พิมพ์ `CLEAR` ตามที่ prompt ทวงถาม ( prompt: C: IV1 area clear, pump STOPPED, nobody at the lance ) — ถ้าไม่ตอบตรงตัวบล็อกจะ**ไม่เรียก exe เลย**
- consent ที่บล็อกขอ: `PhysicalDoSingle` (อ่านแบบซ่อน, ลบทิ้งหลังจบบล็อก)
- exit code ที่ยอมรับ: `0` เท่านั้น
- env ที่บล็อกตั้ง: `WJSS_DRY_IV_TRIAL_MODE=SOFTWARE_SIMULATED_AI003`, `WJSS_DRY_IV_TRIAL_VALVE=IV1`, `WJSS_DRY_IV_TRIAL_PUMP_STOPPED=PUMP_STOPPED_OWNER_ATTESTED`, `WJSS_DRY_IV_TRIAL_SIMULATED_AI003_BAR=0.7`, `WJSS_VALVE_SUPERVISION_MS=5000`
- key ที่**ต้องตรง**ตามนี้ (15 คู่): `trial_code=IV_TRIAL_ACCEPTED`, `accepted=True`, `evidence_kind=SOFTWARE_SIMULATED`, `trial_purpose=IV_OPEN_CLOSE_HARDWARE_TRIAL_ONLY`, `physical_pump_verified=FALSE`, `physical_ai003_verified=FALSE`, `physical_iv_command=TRUE`, `physical_upper_feedback=TRUE`, `physical_lower_feedback=TRUE`, `open_command` ขึ้นต้นด้วย `IV1`, `close_command` ขึ้นต้นด้วย `IV1`, `cleanup.complete=True`, `cleanup.refused=0`, `cleanup.transport_failures=0`, `cleanup.unknown=0`
- key ที่แนบมาให้ผมดูด้วย (ไม่ใช้ตัดสิน pass/fail): `valve`, `open_command`, `close_command`, `open_observation`, `closed_observation`, `cleanup`, `cleanup.attempted`, `cleanup.closed`, `cleanup.refused`, `operator_abort`, `PUMP_READY_EVIDENCE`, `PHYSICAL_PUMP_EVIDENCE`, `queue`, `surface`, `motion`

- stage นี้ **light โดยเจตนา** (ค่า terminal ขึ้นกับ feedback จริงหน้างาน) — ตัวเลขทั้งหมดในก้อน send ผมวิเคราะห์เอง
- ถ้า `exit=4` = startup refused (env/consent ไม่ครบหรือชนกัน) · `exit=2` = refuse หลัง bind เช่น `IV_TRIAL_REFUSED_*_NOT_FIELD_CONFIRMED` / `CONTRADICTORY` → **นี่คือ fail-closed ทำงาน ไม่ใช่บั๊ก** หยุด stage แล้วส่ง log

## 5b. ข้อเท็จจริงทางฮาร์ดแวร์ที่ Owner ยืนยันแล้ว (บันทึกไว้กันเข้าใจผิดซ้ำ)

- `DO-023` (WSB air solenoid ผ่าน Relay K160) เป็นแบบ **MAINTAINED** ไม่ใช่ pulse/momentary: เขียน `True` = เปิด, เขียน `False` = ปิด แล้วคงสถานะ
- ตาม sheet "Contract Summary" ของไฟล์ mapping: **การเขียน DO 0-31 ใช้ FC05/FC15 แต่การอ่านกลับด้วย FC01 ต้องอ่านที่ RW mirror ตำแหน่ง +512** → `DO-023` เขียนที่ address **22** แต่ต้องอ่านกลับที่ **534** (22+512); การอ่าน FC01 ที่ address 22 จะได้ `False` เสมอ ซึ่ง**ไม่ใช่ defect ของ Product**
- ความน่าเชื่อถือของสถานะวาล์วใน Product จึงอยู่ที่ **limit switches ของหัวงาน (DI-021 Upper + DI-029 Lower)** ซึ่งเป็นตัวที่ยืนยัน `IV_OPEN_FIELD_CONFIRMED` / `IV_CLOSED_FIELD_CONFIRMED` ใน Stage C แล้ว

- **เส้นทาง Modbus ใช้การเชื่อมต่อต่อเนื่องอยู่แล้ว — ไม่ต้องแก้ and อย่าถกซ้ำ**: `ProcessIoPhysicalTransport.GetOrCreateClient`
  (packages/application/ProcessIo/ProcessIoPhysicalTransport.cs:399-416) เก็บ `TcpClient` หนึ่งตัวต่อ `deviceKey` ใน `ConcurrentDictionary`,
  `TryGetValue` แล้วเช็ค `existing.Connected` ก่อนใช้, และสร้างใหม่เฉพาะหลัง client ที่เสียถูกถอดออกจาก dictionary เท่านั้น
  ไม่มี `new TcpClient()` ต่อคำสั่ง Modbus ใน repo (มีจุดเดียวคือบรรทัด 406 ข้างใน `GetOrCreateClient` นี้) และไม่มี `SendMbap`/`SendWriteSingle`
  ตามที่เคยเข้าใจ จึงไม่มี socket churn/TIME_WAIT ให้แก้; สิ่งที่โต๊ะนี้พลาดไม่ใช่เรื่อง socket (ดูบันทึกผลรันจริงที่ `6865f58` ใน §6 ข้อ 6.1.1)
- **จังหวะระหว่างคำสั่งใน Start() ไม่มี dwell ที่ Product เติมให้**: `ProcessIoPumpLifecycle.Start`
  (packages/application/ProcessIo/ProcessIoPumpLifecycle.cs:288-321) เขียน DO-001/DO-002 → DO-003 → DO-007 ติดกัน
  ระยะห่างจริงคือเวลาที่ Modbus round-trip ใช้เท่านั้น และ **AO จะเริ่มยกก็ต่อเมื่อ DI-004 ยืนยันว่ารันแล้ว**
  (`StepWaitingForRunning` ที่ :416-440 → `RAMPING_OUTPUT` ที่ :429; ถ้าหมดเวลาจะได้ `_primaryFailure = RUNNING_FEEDBACK_TIMEOUT`)
  ถ้า inverter ต้องการ dwell ระหว่าง enable กับ run หรือต้องการ AO floor ก่อนรอ running feedback **นั่นเป็นการตัดสินใจระดับ Product**
  เพราะบันไดชุดเดียวกันนี้เป็นตัวที่ปิด A/B/C/E มาแล้ว — ตอนนี้มีตัวเลข `dispatch_window_ms`/`gap_ms` ให้วัดจริงก่อนตัดสินใจ
- **รอบการ step ของ trial ตอนนี้เป็น 100 ms** (`PumpCommandDryTrial.DriveIntervalMilliseconds` เดิม 5 ms) — ละเอียดกว่า gate 10 เท่า
  เพราะสิ่งเดียวที่ loop นี้รอคือ wall clock: ramp ผูกกับ `PumpSpeedRampConfiguration.OwnerInitialIntervalMilliseconds = 1000`
  (`apps/runtime/CleaningPathDryComposition.cs:1227`, `ProcessIoPumpLifecycle.cs:444 if (now < _nextRampAt) return;`)
  และ `maximumSteps`/deadline คำนวณจากเวลาไม่ใช่จำนวนครั้ง → loop ช้าลงไม่ทำให้ ramp หรือการรอ DI-004 หิว,
  บันไดเดินทางเดียวและ PUMP_READY/terminal state ค้างอยู่เมื่อถึงแล้ว จึงไม่มี rung ไหนหลุดเพราะโพลล์ช้าขึ้น
  (ผลพลอยได้: จำนวน request บน coupler ลดลง ~20 เท่าในช่วง supervision 50 s เดิม)

- **กฎ "instant ที่ใช้ตัดสินต้องถูกจับหลังการอ่าน" (ต้นเหตุจริงที่ปิดไปแล้วบนโต๊ะรอบ `ee69196`)**: `ProcessIoPhysicalObservation.ObservedAt`
  ถูกประทับ *ข้างใน* `ProcessIoSession.ObserveDigitalInput`/`ObserveAnalogInput` (packages/application/ProcessIo/ProcessIoFoundation.cs:525, :560)
  ส่วน `ProcessIoEvidenceAcceptance.Accepts(...)` → `IsUsableAt` มีเงื่อนไข `ObservedAt <= now` (packages/contracts/ProcessIoContracts.cs:699-703)
  เดิม `ProcessIoPumpCommandBoundary` จับ `now` ไว้ *ก่อน* เรียกอ่าน (บรรทัด 447/383) ⇒ `ObservedAt > now` เสมอบนนาฬิกาจริง ⇒ ทุกการอ่านถูกทิ้ง
  = `Usable=false` ตลอด ⇒ `RUNNING_FEEDBACK_TIMEOUT` + `STOPPED_FEEDBACK_TIMEOUT` + `PHYSICAL_ABORT_REQUIRED` พร้อมกันทั้งที่ `transport_errors=0`
  ตอนนี้ boundary จับ `now` หลังการอ่านเหมือนที่ equipment boundary ทำ (`ProcessIoEquipmentBoundary.cs:449-450`) และมี test กันถอยหลัง
  ใช้นาฬิกาที่ก้าวจริงทุกการเรียก (`tests/runtime.tests/ProcessIo/ProcessIoPumpLifecycleTests.cs` — `TickingTime`)
  **บันทึกไว้ให้เห็นว่าทำไม unit test แบบนาฬิกาแช่แข็ง (`FakeTime.GetUtcNow() => _now`) จึงมองไม่เห็นแผลนี้**: สอง call คืนค่าเท่ากันเสมอ
- **สถานะแถวใน Catalogue ไม่เกี่ยวกับสาเหตุนี้**: เกตการอ่านคือ `row.SourceClassification == "USED"` (`ProcessIoFoundation.cs:147, :279, :583`)
  และ `ProcessIoCommissioningState` มีแค่ `EXCLUDED`/`READ_ONLY_CANDIDATE` (`packages/contracts/ProcessIoContracts.cs:91-95`) —
  `READ_ONLY_CANDIDATE` ไม่ได้บล็อกทั้งอ่านและเขียน (บล็อกเฉพาะ `EXCLUDED`: :203, :586) และแถว DI-004/DI-005 เป็น `USED` ใน CSV จริง

## 6. Stage D — Integrated cleaning-path cycle (มี 4 variant)

ปิด gate: วงจรทั้งรอบ (select pump → enable → run → AO ramp → supervision → cleanup → release) ควบคู่กับการเปิด/ปิดวาล์วและการเดิน Cleaning Path P1-P6

### ข้อ 6.1 — variant แรก: `D-pump` = บันไดคำสั่งปั๊มอย่างเดียว (ล็อกการเขียนวาล์วเชิงโครงสร้าง)

```powershell
& "$script:Ev\stage-D-pump.ps1"
```

- **ต้องการแค่ WAGO T8 เท่านั้น**: trial นี้ไม่อ่าน Cleaning Path store, ไม่อ่าน sensor CSV, ไม่ compose motion — จึงรันบนโต๊ะ
  ที่ต่อเฉพาะ Process-I/O ได้ทันที (และเพราะไม่มี motion จึงไม่มีแกนไหนขยับ)
- Product ที่รัน: `apps/runtime/PumpCommandDryTrial.cs` (เลือกด้วย `WJSS_PUMP_COMMAND_DRY_TRIAL=PUMP_ONLY_NO_VALVES`) — บล็อกนี้**ไม่ compose equipment boundary เลย** จึงไม่มีเส้นทางสั่งวาล์วอยู่ใน process นั้น (ไม่มี `RequestValve`, ไม่มี cleanup close ของวาล์ว) และไม่มี motion → วาล์วอยู่กับที่ตามที่ Stage C ทิ้งไว้, แกนไม่ขยับ
- บันไดที่ตรวจ (มาจาก `ProcessIoPumpLifecycle` ตัวเดิม ไม่มี state machine ชุดที่สอง): `selection_pair_written` (DO-001 TRUE / DO-002 FALSE) → `pump_enable_written` (DO-003 TRUE) → `pump_run_written` (DO-007 TRUE) → `running_feedback_confirmed` (DI-004) → `analog_ramp_executed` (AO-001) → `pump_ready_confirmed` (AI-003 > setpoint แบบ strictly-greater เดิม) → `analog_stop_sentinel_written` (AO = 0 mA sentinel) → `pump_run_cleared` (DO-007 FALSE) → `pump_enable_released` (DO-003 FALSE) → `stopped_confirmed` (DI-004 not-running) → `ownership_released` + `safe_to_report_complete`
- ต้องพิมพ์ยืนยันก่อนรัน: `PUMP LADDER ONLY` — ปั๊มทำงานจริงขณะที่วาล์วปิด เส้นสายจะ dead-head จึงต้องยืนยันว่าเส้นทาง relief/return เปิดอยู่และไม่มีใครอยู่ที่ lance
- บรรทัดหลักฐาน: `evidence_kind=PHYSICAL`, `physical_pump_verified=True`, `iv_command_executed=FALSE`, `iv_command_surface=NONE_NO_VALVE_BOUNDARY_COMPOSED`, `motion_composed=False`
- exit ที่ยอมรับ: `0` เท่านั้น (2 = ปฏิเสธ/ล้มกลางบันได, 3 = supervision หมดเวลา, 4 = ปฏิเสธก่อนแตะอุปกรณ์)
- มีอ่าน AI-003 เทียบอีกชั้นหนึ่งใน session เดียวกัน (ผ่าน read-only observer ตัวเดิม) (read-only เท่านั้น): `pump_outlet_pressure_bar`, `ai003_evidence_verified`, `ai003_pump_ready_rule`

### ข้อ 6.1.1 — หลัง `6865f58`: บันไดถูกตัดสินด้วย "state ที่เห็นจริง + dispatch log" ไม่ใช่ ordinal และทุก refusal ต้องพูดได้

สองรอบแรกที่โต๊ะ (start จาก DO-001=0 และจาก DO-001=1) พิสูจน์ว่าของเดิม "พูดมากเกินไป" และ "เงียบเกินไป" จึงแก้พร้อมกัน:

- **เลิกนับ ordinal ของ enum** — `PumpLifecycleStates.IsFailure` คือ ordinal `>= 20` ซึ่ง**อยู่เหนือ** progression states (0-13)
  ทำให้ `RUNNING_FEEDBACK_TIMEOUT` "ผ่าน" การเทียบกับ `RAMPING_OUTPUT` และพิมพ์ `running_feedback_confirmed=True` ทั้งที่บันไดไม่เคยถึงขั้นนั้น
  ตอนนี้ `Drive` เก็บ `HashSet<PumpLifecycleState> visited` (เก็บตอนเริ่ม, ทุกครั้งก่อน/หลัง `Step()`, และ TerminalState) แล้ว key ใด ๆ
  ที่เกี่ยวกับ rung ตอบจาก set นี้เท่านั้น
- **rung ที่เกิดใน `Start()` loop เห็นไม่ได้** (`selection_pair_written`, `pump_enable_written`, `pump_run_written`) จึงอ่านจาก
  `boundary.CommandLog` แทน: ต้องมี record ที่ `Succeeded` (admitted + dispatched + ไร้ transport fault) และ `Value` ตรงตามที่ contract สั่ง
- **ไม่เรียก `Stop` ซ้ำบน lifecycle ที่ terminal แล้ว** — เดิมได้ `WJSS_PUMP_COMMAND_REFUSED_ALREADY_TERMINAL`
  (`ProcessIoPumpLifecycle.cs:338` คืน `RefusedTerminal` เมื่อ `Result is not null`) และพิมพ์เรื่องเล่าชุดที่สองของ teardown เดียวกัน
  ตอนนี้บันทึก `stop_code=NOT_REQUESTED_LADDER_ALREADY_TERMINAL` แทน (ไม่ใช่ defect ของฮาร์ดแวร์ — เป็นแผลของ runner)
- **`complete` เข้มขึ้น: บันไดที่ timeout ต้องออก exit 2 ไม่ใช่ 0** — ต้องครบทุกข้อ: `TerminalState == STOPPED`, `StoppedConfirmed`,
  `CleanupState == COMPLETE`, `!OwnsPumpCommand`, `SafeToReportComplete`, เคยเห็น `PUMP_READY` จริง (`run_reached_ready=True`) และไม่เจอ Fault
  ⇒ ผลข้างเคียงที่ตั้งใจ: **รอบที่ปั๊มไม่ขึ้นจะมี exit=2 + RESULT CHECK** ถ้าเคยเห็น exit=0 ทั้งที่ `terminal_state=RUNNING_FEEDBACK_TIMEOUT`
  นั่นคือบั๊กเก่าที่ถูกปิดไปแล้ว
- **diagnostic ที่แนบมากับ send file (Show เท่านั้น ไม่ใช้ตัดสิน pass/fail)** — `dispatch_summary`, `dispatch` รายคำสั่ง
  (มี `gap_ms`), `dispatch_window_ms`, `dispatch_address_agreement`, `command_dispatches`, `command_dispatch_refusals`,
  `run_reached_ready`, `run_cleared_dispatch`, `enable_released_dispatch`, `stop_accepted`, `stop_code`,
  `running_commissioned`, `fault_commissioned`, `running_binding`, `fault_binding`,
  `last_running_usable`/`_raw_bit`/`_active`/`_code`/`_observation_raw`, `last_fault_usable`/`_raw_bit`/`_active`/`_code`/`_observation_raw`,
  `network_attempts`, `transport_errors`, `transport_error_summary`
- **แยกให้ออกระหว่างสองบรรทัดที่หน้าตาคล้ายกัน**: `last_running_raw_bit` คือบิต *ที่ตีเป็นหลักฐานแล้ว* (`usable && raw!=0` — ถ้าอ่านไม่ถูกยอมรับจะเป็น False เสมอ
  ไม่ใช่เพราะบิตต่ำ) ส่วน `last_running_observation_raw` คือบิต *ที่อุปกรณ์ตอบมาจริง* (`PumpFeedbackRead.ObservationRawBit`)
  ⇒ ถ้า `usable=False` แต่ `observation_raw=True` แปลว่า "อินพุต HIGH อยู่ แต่ระบบยังไม่ยอมรับ record นี้" ซึ่งคนละเรื่องกับ "อินพุต LOW"
- `last_running_code` / `last_fault_code` ตอนนี้เป็นการส่ง `observation.DiagnosticCode` จริงออกมา (`WJSS_SESSION_NOT_OPEN`,
  `WJSS_OBSERVATION_REFUSED_UNBOUND`, `WJSS_OBSERVATION_REFUSED_NOT_COMMISSIONED`, `WJSS_OBSERVATION_TRANSPORT_FAULT`,
  `WJSS_OBSERVATION_STALE`) และถ้าการอ่านสะอาดแต่ acceptance ไม่รับ จะได้ `WJSS_PUMP_FEEDBACK_NOT_ACCEPTED`
  — เดิมทุกกรณีถูกทับเป็น `WJSS_PUMP_READY_REFUSED_PRESSURE` ซึ่งเป็น code ของกฎแรงดัน ไม่เกี่ยวกับอินพุตดิจิทัล
- **`coil=` กับ `written=` คนละที่มา and ตอนนี้พิมพ์คู่กัน**: `coil=` มาจากตาราง contract (`PumpCommandContract.TryCoilAddress` → `ProcessIoBindings`)
  ส่วน `written=` คือ `row.Binding.Address` ที่ session ใช้เขียนจริงผ่าน FC05 (`ProcessIoFoundation.cs:221`) —
  `dispatch_address_agreement=MATCH_OVER_n` แปลว่าตรงทุกบันทึก, `CHECK(tag contract=x written=y)` แปลวาสองตารางไม่ตรงกัน (ต้องหยุดและส่ง log)
- **อ่าน diagnostic ยังไง (ไม่ต้องเดา):**
  | ที่เห็น | หมายความว่า |
  | --- | --- |
  | `last_running_code=WJSS_OBSERVATION_REFUSED_UNBOUND` + `running_binding=DI-004=UNBOUND ..` | แถว DI-004 ใน catalogue ไม่ถูก bind → แก้ที่ CSV mapping ไม่ใช่ที่โค้ด |
  | `last_running_code=WJSS_OBSERVATION_REFUSED_NOT_COMMISSIONED` + `running_commissioned=False` | ขั้วสัญญาณยังไม่ได้ commission (env `WJSS_PUMP_RUNNING_POLARITY`) |
  | `last_running_code=WJSS_SESSION_NOT_OPEN` | session ปิดไปก่อนแล้ว (ดู `session_state`) |
  | `last_running_code=WJSS_OBSERVATION_TRANSPORT_FAULT` + `transport_errors>0` | อ่านไม่ตอบเลย (network/timeout) — ไม่ใช่ "บิตต่ำ" |
  | `last_running_usable=True` + `last_running_raw_bit=False` + `transport_errors=0` | อุปกรณ์ตอบจริงและบิตต่ำจริง → ดูฝั่ง inverter/starter/สาย DI และเทียบ `running_binding=DI@<addr>` กับ mapping workbook (เบอร์หน้าตู้ = addr+1) |
  | `dispatch=DO-003 .. admission=REFUSED` | session ปฏิเสธการเขียน (consent/generation/lock) — ไม่ใช่ความผิดของ rack |
  | `dispatch=DO-007 .. admission=ADMITTED dispatch=NOT_DISPATCHED transport=FAULTED` | คำสั่งไปไม่ถึงอุปกรณ์ (socket ตาย — transport จะ reconnect ในครั้งถัดไปตาม §5b) |
  | `gap_ms` / `dispatch_window_ms` เล็กมากและ inverter ต้องมี dwell dwell ระหว่าง enable→run | เป็นเรื่องของ Product lifecycle ไม่ใช่ของ trial — ส่งค่านี้มาให้ผมตัดสินใจ |
- **load-time gate ใหม่**: ตัวขับจะปฏิเสธทุก stage ถ้า `Test-WjssDriver` ยังไม่ PASS ในหน้าต่างนั้น
  (`SELFCHECK: PASS (driver 2026-11-18.1, .. groups)` ต้องปรากฏก่อน) — เพราะที่ `6865f58` self-test แดงแต่ stage ยังรันได้
  และ fixture กับ assertion เคยหลุดกัน (บรรทัด `fresh=TRUE` หายไปจาก fixture) ซึ่งแก้แล้วทั้งคู่

### ข้อ 6.1.2 — variant สอง: `D-pump-mv` = บันไดปั๊มจริง + Motorized Main Valve (บล็อก 1.27)

```powershell
& "$script:Ev\stage-D-pump-mv.ps1"
```

- **ต่างจาก `D-pump` ตรงไหน:** เหมือนเดิมทุกประการ (trial ตัวเดิม `apps/runtime/PumpCommandDryTrial.cs`, session เดิม,
  ไม่ compose equipment boundary, ไม่มี motion, วาล์ว IV ทั้งสี่ยังปิดตามที่ Stage C ทิ้งไว้) ที่เพิ่มคือ selector
  `WJSS_PUMP_COMMAND_DRY_TRIAL=MAIN_VALVE_AND_PUMP` ซึ่ง compose `ProcessIoMainValveGate` ไว้หน้าบันไดปั๊ม:
  Product เปิด Main Valve เอง (WJSS เป็นผู้เขียนคนเดียว — ตู้หน้างานไม่เปิดให้อัตโนมัติ) แล้วจึงสั่ง Select Pump
  และปิดวาล์วกลับหลังหยุดปั๊มแบบยืนยันแล้วเท่านั้น
- **แอดเดรส/ฟังก์ชันที่ใช้ (derive จากตาราง binding ที่ adopt แล้วเท่านั้น ไม่มีเลขที่เขียนตายไว้ในโค้ด):** `DI-012` Over Torque
  = FC02 discrete 11, `DI-013` Valve is Opened = FC02 12, `DI-014` Valve is Closed = FC02 13, `DO-014` Open Valve
  = FC05 coil 13, `DO-015` Close Valve = FC05 coil 14 กับดักที่ห้ามสับสน: DI‑014 (discrete 13) กับ DO‑014 (coil 13)
  อยู่คนละ address space และการ read‑back ของ coil (FC01 525/526) เป็นเพียง process image ไม่ใช่หลักฐานว่าวาล์วขยับ
  ตำแหน่งจึงตัดสินจาก limit switch เท่านั้น (บันทึกจาก Owner: workbook ใส่เลข `K136` ที่ลิมิตทั้งสอง ซึ่งเทียบ step ของ
  trio พี่น้องแล้วน่าจะเป็น `K137` — ถือเป็นการแก้เอกสารเท่านั้น ไม่แตะ address แต่อย่างใด)
- **กฎที่บังคับ (Owner rulings 2026-11-19):** open = `DO-015` FALSE ก่อนแล้ว `DO-014` TRUE, close = `DO-014` FALSE ก่อนแล้ว
  `DO-015` TRUE, ห้ามเป็น TRUE พร้อมกันเด็ดขาด; เมื่อถึงลิมิตแล้วต้องปล่อยคอยล์ที่จ่ายอยู่ (ไม่ hold คอยล์);
  `WJSS_MAIN_VALVE_OPEN_TIMEOUT_MS` และ `WJSS_MAIN_VALVE_CLOSE_TIMEOUT_MS` = **45000** (ตัวขับเป็นคนตั้งให้ **ฝั่ง C# ไม่มีค่า default
  ใดๆ ทั้งสิ้น** — `TryReadRequiredTimeout` คืน false เมื่อ env หาย ⇒ `DRY_COMPOSITION_REFUSED_MAIN_VALVE_CONFIGURATION`
  ก่อนแตะ endpoint; `PumpCommandDryTrial` ก็เรียกตัวอ่านเดียวกัน (`CleaningPathDryComposition.cs:1475-1515`,
  `PumpCommandDryTrial.cs:355-358`) — Owner อนุมัติข้อ ก. ไว้ที่ 45000 ms แล้วไม่มีการแก้ constant ใน C# เพราะไม่มี constant
  ให้แก้ ดูหัวข้อ "เวลาเดินทางจริง" ด้านล่าง);
  `DI-012` เป็น TRUE ตอน pre‑flight ⇒ ปฏิเสธการเริ่มด้วย `WJSS_MAIN_VALVE_OVER_TORQUE` และถ้า trip ระหว่างเดินทางหรือ
  ระหว่างรัน ⇒ ปล่อยทั้งสองคอยล์แล้วเข้าเส้นทางหยุดทันที; ขา feedback อ่านไม่ได้ หรือได้ CONFLICT (ลิมิตสองตัวเป็น TRUE พร้อมกัน)
  ⇒ ปฏิเสธและไม่จ่ายปั๊ม; ปิดวาล์วเฉพาะหลังยืนยันว่าหยุดแล้ว และความผิดพลาดของวาล์วห้ามขัดขวางการหยุดปั๊ม
  (AO = 0 mA, `DO-007` FALSE, `DO-003` FALSE ไปต่อเสมอ)
- ต้องพิมพ์ยืนยันก่อนรัน: `MAIN VALVE 1.27 COMMAND AUTHORIZED`
- **วาล์วจะเริ่มจากสถานะไหนก็ได้ ทั้งสองกรณีต้องผ่าน:** ถ้า `DI-013` ยืนยันว่าเปิดอยู่แล้ว รับเป็น
  `main_valve_open_source=PRE_CONFIRMED_OPEN` (ไม่เขียนคอยล์เลย) ถ้าต้องสั่งเปิดเองเป็น `PRODUCT_DISPATCH` และเพราะกฎ
  "ปิดทุกจบรัน" การรันครั้งถัดไปจะเริ่มจาก CLOSED เสมอ จึงพิสูจน์ `PRODUCT_DISPATCH` ไปเองโดยไม่ต้องจัดฉาก
- key หลักใน out.txt: `main_valve_policy=ENFORCED`, `main_valve_command_executed`, `main_valve_position_at_start`,
  `main_valve_open_confirmed`, `main_valve_open_source`, `main_valve_open_refusal=NONE`, `main_valve_close_requested`,
  `main_valve_pump_stop_confirmed`, `main_valve_close_confirmed`, `main_valve_position_at_end=CLOSED`,
  `main_valve_coils_resting`, `main_valve_over_torque`, `main_valve_position_lost`, `main_valve_held_open`,
  `main_valve_supervision_fault`, `main_valve_refusal=NONE`, `main_valve_position_reads`, `main_valve_writes`
  และบรรทัด `main_valve_write=DO-014 coil=13 bound=13 value=True admission=ADMITTED dispatch=DISPATCHED code=OK`
  ทีละการเขียน (จำนวนเขียนไม่คงที่ตามสถานะเริ่มต้น จึงเป็น `-Show` ไม่ใช่ `-Expect`) ส่วน `physical_pump_verified`,
  `stop_accepted`, `valve_open_admitted_by_lifecycle` คงชื่อเดิมความหมายเดิมไว้ — `valve_open_admitted_by_lifecycle`
  คือ "lifecycle อนุญาตให้เปิดวาล์ว IV หรือยัง" ไม่ใช่หลักฐานว่า Main Valve เปิด จึงไม่เอามาแทนกัน
- exit ที่ยอมรับ: `0` เท่านั้น (2 = ปฏิเสธที่วาล์วหรือล้มกลางบันได, 3 = supervision หมดเวลา, 4 = ปฏิเสธก่อนแตะอุปกรณ์
  รวมถึง `DRY_COMPOSITION_REFUSED_MAIN_VALVE_CONFIGURATION` เมื่อขาด timeout สองตัว)
- **แยก 2 กับ 4 ให้ออก (ใช้ได้กับทุก variant):** ตัวเลขมาจากคนละชั้น — `4` คือ option/env ที่ trial อ่านเองไม่ผ่าน
  (`PumpCommandDryTrial.TryReadMode` etc.) ส่วน `2` คือ composition ล้มก่อนที่ trial จะเริ่ม ซึ่งเกิดได้กับ
  `D-sim` / `D-dry` เท่านั้น เพราะสอง stage นี้ compose ผ่าน `CleaningPathDryComposition` (กลุ่มนี้รวมถึง
  `DRY_COMPOSITION_REFUSED_SENSOR_PARAMETERS`, code ที่ importer คืนมาเอง เช่น `TOPO_TARGET_WALL_CONTRADICTION`,
  `_SENSOR_ID`, `_RESOLUTION`, `_CATALOGUE`, `_THRESHOLDS`, `_SUPERVISION_WINDOW`, `_MOTION`,
  `_MOTION_NOT_HARDWARE_CAPABLE` และ `_MOTION_CABINET_MISMATCH` ที่เพิ่มตาม ruling 2026-11-11)
  เห็น `exit=2` ให้หาบรรทัด `WJSS dry composition refused [CODE]: detail` ใน out.txt แล้วแก้ที่ input ของโต๊ะ
  (CSV / store / env) ไม่ใช่แก้ที่โค้ด และห้ามตีความ exit 2 ว่าเป็นความผิดของวาล์ว
- **เวลาเดินทางจริงของวาล์ว (วัดที่โต๊ะ 2026-11-11, HEAD `16f78bc`):** เปิด CLOSED→OPEN ใช้ `30399.395 ms`,
  ปิด OPEN→CLOSED ใช้ `29609.902 ms` ⇒ ค่า 30000 ที่คิดตอนออกแบบ **แคบกว่าของจริงประมาณ 0.4 วินาที**
  ถ้าค่านี้อยู่กับที่ stage จะ ABORT แบบ fail-closed ทั้งที่วาล์วทำงานปกติ ตัวขับจึงตั้งเป็น **45000** (เผื่อ 1.48 เท่า
  บนทิศทางที่ช้ากว่า และยังต่ำกว่าขอบเขตเชิงกลมาก) — ห้ามลดกลับต่ำกว่า 35000 โดยไม่มีเหตุ; `main_valve_position_reads=714`
  ครอบคลุมทั้งช่วงเปิดและช่วงปิด (30399.395 + 29609.902 = 60009 ms) ⇒ เฉลี่ย **84 ms ต่อการอ่าน** สอดคล้องกับจังหวะ
  supervision 100 ms ของ gate (`ProcessIoMainValveGate.SupervisionInterval`) บวกเวลาตอบของอุปกรณ์ — ไม่มีการข้ามจังหวะ
  และ `network_attempts=1550` / `transport_errors=0` ยืนยันว่า socket ไม่หลุดระหว่างรอลิมิต
- **บั๊กตัวขับที่โต๊ะเจอ (แก้แล้วที่ `16f78bc` → driver `2026-11-21.1`):** `Invoke-WjssStage` เลือกโปรไฟล์จากเส้น
  `if ($Name -eq 'C-iv1' -or ... -or $Name -eq 'D-sim') { $prof = 'TEST_HARDWARE_DRY' }` ซึ่งเขียนก่อนมี `D-pump-mv`
  ⇒ `D-pump-mv` ได้ `TEST_HARDWARE` และถูกปฏิเสธด้วย `PUMP_COMMAND_TRIAL_REFUSED_PROFILE_NOT_TEST_HARDWARE_DRY` (exit=4)
  **ก่อนแตะอุปกรณ์** ซึ่งเป็นพฤติกรรม fail-closed ที่ถูกต้องของ Product (trial ต้องเห็น `TEST_HARDWARE_DRY` เท่านั้น
  `apps/runtime/RuntimeHostOptions.cs:389`) แต่เป็นความผิดของตัวขับ ตอนนี้เพิ่มชื่อ stage เข้าเส้นนั้นแล้ว และ
  `Test-WjssDriver` มีกลุ่มตรวจใหม่ว่า `D-pump`/`D-pump-mv` ต้องเลือกโปรไฟล์นี้ ไม่อย่างนั้น self-test แดง
- **ทำไมไม่แตะ `D-dry` / `D-sim`:** สอง stage นั้นพิมพ์ `'profile' = 'TEST_HARDWARE_DRY'` ใน body เอง (ตรวจได้ด้วย self-test)
  และ **ไม่ต้องมี timeout สองตัว** เพราะ `TryReadMainValveConfiguration` ถูกข้ามเมื่อ `WJSS_DRY_SIMULATED_PUMP_READY`
  ถูกตั้ง (`CleaningPathDryComposition.cs:659-665` — เงื่อนไข `!simulatedPumpReady`) การใส่ env ที่ Product ไม่อ่าน
  จะทำให้ตัวตรวจ "ทุก WJSS_* ต้องมีใน source" สับสน จึงไม่ใส่
- **gate D ยังไม่ปิดด้วย stage นี้ตัวเดียว:** ถ้ารันนี้ผ่าน จะบันทึกว่า "วาล์วถูกสั่งและยืนยันโดย Product แล้ว" —
  การปิด gate ต้องอ่าน `main_valve_write` ทั้งก้อน (ลำดับต้องเป็น FALSE ของคอยล์ฝั่งตรงข้ามก่อนเสมอ) พร้อม
  `main_valve_coils_resting=True` และ `main_valve_refusal=NONE`

**บันทึกหลักฐานการรันที่นับเป็น gate (executed field evidence, Owner ran — not simulated):**
`RESULT D-pump-mv: PASS (exit=0)` เมื่อ 2026-11-11 01:33:22 ที่ `HEAD=16f78bcc` · driver `2026-11-20.1` +
patch โปรไฟล์ในหน่วยความจำ (ยังไม่อยู่ในไฟล์ตอนนั้น) · exe sha256 `492BCB9DE0CF` · ทั้งสอง timeout ถูก override
เป็น 45000 ms ผ่าน env ที่ชั้น caller เพราะค่า 30000 ในตัวขับตอนนั้นสั้นกว่าเวลาเดินทางจริง
(ตัวขับที่ `44820ef` แก้ที่ต้นเหตุแล้ว — รันรอบใหม่ไม่ต้อง override อะไร)
การรันนี้ **ไม่ได้** ใช้ `WJSS_UPDATE_FIXTURES=1`, `.err.txt` ว่าง, และทุก key ที่ `-Expect` ตั้งไว้ (40 คู่) ตรงตามค่า
สิ่งที่ gate นี้พิสูจน์จริง: Product เป็นผู้เปิดวาล์วเอง (`PRODUCT_DISPATCH`) → ยืนยันตำแหน่งด้วย FC02 → ค่อย Select Pump
→ รันบันไดครบ 9 ขั้น → หยุดยืนยันแล้ว → ปิดวาล์วจน `CLOSED` + คอยล์พักทั้งคู่ (`coils_resting=True`)
สิ่งที่ **ไม่ได้** พิสูจน์ (ห้ามอ้างเกิน): ไม่มีน้ำแรงดันสูงผ่าน nozzle, ไม่มี Cleaning Path/IV sequence, ไม่มีการขยับ axis
และไม่ใช่ FRR — ตาราง §11 ยังเหลือ `D-sim` ที่ Owner กำลังรันบนโต๊ะ (ขออนุมัติ 2026-11-11 ข้อ ข.), `D-dry` และ `F`
ถูก **waive** ไม่เป็น merge blocker (ข้อ ค.), ส่วน `E` ปิดเฉพาะเส้นทางอ่าน ยังไม่ทำ per-switch E ตามที่ตกลง

**คำตัดสินของ Owner ที่ HEAD `e3a731c` (2026-11-11) — บันทึกลากถึงเหตุผล ไม่ใช้แค่สรุปว่า "อนุมัติ":**
- **ข้อ ก. ค่า default timeout ของวาล์ว:** อนุมัติให้ใช้ **45000 ms** ให้ตรงกับตัวขับและเวลาเดินทางจริง
  (เปิด 30399.395 ms / ปิด 29609.902 ms) · **ผลตรวจในโค้ด: ไม่มี default ฝั่ง C# ให้แก้ตั้งแต่ต้น** — ความเห็นว่า
  "trial default ที่ 30000" ที่ผมเขียนไว้ในรายงานก่อนหน้า **ไม่ตรงกับ source** ผมจึงไม่สร้าง default ขึ้นใหม่
  เพราะจะเป็นการกลับกติกา fail‑closed ที่ Owner อนุมัติไว้เอง ("ขาด env = ปฏิเสธ") สิ่งที่แก้จริงจึงมีแค่
  doc comment ที่เขียนเลขค้างไว้ 30000 ใน `apps/runtime/CleaningPathDryComposition.cs:157-159`
  (เป็นข้อความล้วน ไม่มี test ไหน pin ตัวเลขนี้: `CleaningPathDryCompositionTests.cs:465-477` กับ
  `PumpCommandDryTrialTests.cs:470-481` ตรวจเฉพาะชื่อตัวแปรกับคำสั่ง refusal)
- **ข้อ ข. `D-sim`:** Owner ดำเนินการรันบนหน้างานจริง (ปั๊มจริงหยุด, `SOFTWARE_SIMULATED_AI003`) และจะส่ง
  `stage-D-sim.out.txt` + `.err.txt` มาให้บันทึกเพื่อปิด gate ⇒ แถว `D-sim` ยัง **OPEN** จนกว่าหลักฐานจะมาถึง
- **ข้อ ค. waive `D-dry` (โหมดเปียก) และ `F` (motion stop):** **ไม่ใช่ merge blocker ของ PR #45** — นโยบายคือแยก
  พิสูจน์ Physical Pump + Main Valve (`A1/A2` + `D-pump-mv`) ออกจาก Physical IV + Cleaning Path (`C-iv1` + `D-sim`)
  เพื่อไม่ให้น้ำแรงดันสูงออกหัวฉีดในขั้นตอน dry commissioning · ตัว stage ยังอยู่ในตัวขับ (ยัง export เป็น
  `stage-D-dry.ps1`) แต่ไม่มีการรัน · ถ้าจะรันเมื่อไหร่ `WJSS_DRY_WET_SEQUENCE_ACK` ยังบังคับอยู่ และ**การ waive
  นี้ไม่ใช่การลดกฎความปลอดภัย** เป็นแค่การตัดเงื่อนไขออกจาก PR นี้เท่านั้น (การกลับคำ waive ต้องเป็นคำสั่ง Owner)

### ข้อ 6.2 — variant สาม: `D-sim` = วาล์วจริง + Path จริงบนค่าแรงดันที่ Owner ประกาศ

```powershell
& "$script:Ev\stage-D-sim.ps1"
```

- Product: `CleaningPathDryComposition` + `CleaningPathSimulatedPumpReadyGate` (เลือกด้วย `WJSS_DRY_SIMULATED_PUMP_READY=SOFTWARE_SIMULATED_AI003` + `WJSS_DRY_SIMULATED_AI003_BAR` > setpoint + `WJSS_DRY_SIMULATED_PUMP_STOPPED=PUMP_STOPPED_OWNER_ATTESTED`)
- **ปั๊มต้องหยุดอยู่ที่ Panel ตลอดการรัน** และใน mode นี้ Product ไม่สั่งปั๊มเลย (ไม่ select, ไม่ enable, ไม่เขียน AO, ไม่อ่าน DI-004/005) — ตรวจได้จาก `cleanup.pump.attempted=False` + `cleanup.pump.terminal=IDLE`
- สิ่งที่ยังเป็นการสัมผัสจริงทั้งหมด: เปิด/ปิด paired isolation valve ผ่าน session ที่มีอำนาจเขียน, limit switches, การเดิน P1-P6, Mandatory Safe Return + cleanup close
- หลักฐานติดป้ายชัดเจน: `pump_evidence_kind=SOFTWARE_SIMULATED`, `physical_pump_verified=False`, `pump_gate=DECLARED_PUMP_PRECONDITION`, `simulated_pump_ready_rule=DECLARED_ABOVE_SETPOINT` + บรรทัด `DECLARED PUMP-READY EVIDENCE: ...` → นับเป็นหลักฐานของเส้นทางคำสั่ง/การควบคุมได้ **แต่ไม่นับเป็นหลักฐานว่า "สั่งงานพร้อมน้ำจริง"**
- ค่าที่ห้ามเดา (ถามบนหน้าจอ): sensor id, mA target (ยังถูก validate แต่ไม่ถูกใช้), polarity x2 และ **bar ที่ประกาศ (ต้อง > 15)**
- **สิ่งที่ D-sim ต้องมีมากกว่า D-pump (สำคัญ - อ่านก่อนรัน):** `CleaningPathDryComposition` compose ทั้ง Cleaning Path จึงต้องอ่าน
  ไฟล์ Owner-local อีก 2 ฉบับและฝั่ง motion ด้วย: `WJSS_CLEANING_PATH_STORE`, `WJSS_SENSOR_PARAMETER_CSV` และชุด motion
  (`WJSS_MANUAL_MOTION_PROFILE`, `WJSS_MOTION_CONTROLLER_01_ENDPOINT`, `WJSS_MOTION_LIB_DIR`, `WJSS_MOTION_LIB_GCLIB_SHA256`,
  `WJSS_MOTION_LIB_GCLIBO_SHA256`, `WJSS_MOTION_LIB_EXPECTED_VERSION`, `WJSS_MANUAL_MOTION_CONSENT`) — ตัวขับ**ถามค่าที่ขาด**
  ในตอนรัน stage `D-sim`/`D-dry` เท่านั้น (ค่าที่เคยตั้งไว้ในหน้าต่างก่อนโหลดตัวขับจะถูกเก็บไว้ไม่ให้ `Clear-WjssEnv` ลบ) และ
  **ถ้าค่าไหนไม่มี ตัวขับจะ STOP ก่อนเรียก exe** พร้อมชื่อตัวแปรให้ส่งกลับมา — ไม่ใช่ให้กรอกเดาสุ่ม
- เพราะฉะนั้น D-sim รันได้เมื่อโต๊ะต่อทั้ง WAGO T8 **และ** motion controller พร้อม homing no-load; ถ้าวันนี้มีแค่ T8
  ให้รัน `D-pump` (บันไดปั๊มจริง, ไม่ต้องมี store/CSV/motion เลย) + `C-iv1` (วาล์วจริงบนค่า AI-003 ที่ประกาศ) ซึ่งคู่นี้ปิด
  gate คนละครึ่งพอดี และการเดิน P1-P6 ค้างไว้จนกว่าจะต่อ motion ครบ (นี่คือขอบเขตจริงของ Product ไม่ใช่ข้อจำกัดของสคริปต์)
- refusal ที่เกี่ยวข้อง: `DRY_COMPOSITION_REFUSED_SIMULATED_PUMP_SELECTOR` / `_BAR` / `_ATTESTATION` — แก้ที่ input ไม่ใช่ที่โค้ด
- **บรรทัด refusal มีเหตุผลของ importer ต่อท้ายแล้ว (เพิ่มตาม ruling 2026-11-11):** ถ้า CSV ถูกปฏิเสธ out.txt จะขึ้นเป็น
  `WJSS dry composition refused [TOPO_TARGET_WALL_CONTRADICTION]: ... Importer detail: record 'SYN-REC-0xx'`
  ` (logical G5, REAR UPPER) maps to 'WJ5' whose target is REAR LOWER.` ข้อความหลัง `Importer detail:` คือ text ที่
  Product เขียนไว้เอง (ไม่ได้เดา ไม่ได้ read-back จากอุปกรณ์) ใช้ชี้แถวที่ผิดใน CSV ได้ทันทีโดยไม่ต้อง debug; ถ้า
  importer ไม่แนบ detail มาจะไม่มีประโยคนี้ต่อท้าย (ไม่มีการเติมข้อความปลอม) และ code ในวงเล็บเป็น code ของ
  importer เสมอ ตัวขับยังนับเคสนี้เป็น `exit=2` เหมือนเดิม
- **การตี tier ของ logical row เปลี่ยนตาม Owner ruling 2026-11-11 (โค้ด + fixture + example ตรงกันแล้ว):**
  แถว 1-3 (`G+2xx`, `G+1xx`, `G`) = UPPER และแถว 4-6 (`H`, `I`, `J`) = LOWER (เดิม 1-2 / 3-6) ตาราง WJ ไม่เปลี่ยน
  (WJ1-4 ฉีด UPPER, WJ5-8 ฉีด LOWER, จากฝั่งตรงข้ามของ tier เดิม) ดังนั้น `cannon` ของแถว `G` ใน CSV ต้องเป็น
  G1-G4 = 3, G5-G9 = 4, G10-G13 = 1, G14-G18 = 2 (IV ตาม WJ: IV3 / IV4 / IV1 / IV2) ส่วนแถว H/I/J คง WJ5-WJ8
  per-WJ count ตอนนี้คือ 12/15/12/15/15/15/12/12 (รวม 108, I7 ยังเป็น WJ5/IV5 และ I16 เป็น WJ6/IV6) ถ้ายังเจอ
  `TOPO_TARGET_WALL_CONTRADICTION` บน HEAD ที่มี ruling นี้แล้ว แปลว่าแถว `G` ใน CSV ยังใส่ cannon เก่า —
  แก้ที่ CSV ห้ามผ่อนปรนตัวตรวจในโค้ด
- **profile motion ต้องเป็นของ cabinet ที่ path นั้นอยู่จริง (refusal ใหม่ `DRY_COMPOSITION_REFUSED_MOTION_CABINET_MISMATCH`):**
  ไฟล์ที่ชี้ด้วย `WJSS_MANUAL_MOTION_PROFILE` ต้องเป็นของ WSB1 ซึ่งเป็น cabinet ของ WJ1/WJ2 บนโต๊ะนี้ Product อ่าน
  cabinet identity จาก profile ที่รับเข้ามาจริง แล้วเทียบกับ cabinet ที่ resolver ได้จาก canonical motion bindings
  ก่อนสร้าง transport / session / Pump lifecycle เลย ⇒ `exit=2` + `motionRoot.Shutdown()` แล้วจบ (ไม่ map axis ซ้ำ
  ไม่ยิงคำสั่งไป controller ตัวอื่น ไม่ทิ้ง session ให้ค้าง) และเพราะ A/B/C/D เป็นชื่อเดียวกันทุก cabinet การดูแค่
  ตัวอักษร axis จึงยืนยันอะไรไม่ได้ นี่คือเหตุผลที่มี gate นี้
- **บรรทัดที่ D-sim ต้องมีหลัง compose คือ `prepare=DRY_PREPARE_READY` (กติกาเพิ่มที่ `4cd8286` หลังรอบบนโต๊ะที่ `D:\wjss-artifact`):** หน้าต่างของ `CleaningPathDryComposition.Prepare` ถูก pace ด้วย `Thread.Sleep(PumpDriveIntervalMilliseconds)` ครบทั้ง 3 วง (เปิด port, อ่านสถานะก่อน Home, homing สองแกน) เพราะหนึ่ง step แตะ port ได้มากสุดหนึ่งคำสั่ง/หนึ่งการอ่าน — งบ `MaximumPrepareSteps = 24000` ที่ pace 5 ms จึงเท่ากับ 120 s ของเวลาจริง ซึ่งครอบคลุม `homeTimeoutMilliseconds` ของ profile บวกช่วง stop+settle ของทั้งสองแกน ถ้าไม่มี pace งบ 24000 หมดในเศษเสี้ยวของวินาที และ timeout ทุกตัวที่วัดด้วยนาฬิกาจะ "ยังไม่ครบ" เสมอ ⇒ บรรทัด refusal ไม่ใช่หลักฐานว่าฮาร์ดแวร์ช้า แต่เป็นหลักฐานว่าหน้าต่างสั้นเกิน
- **Home ถูกขอหลังอ่านสถานะเท่านั้น:** session รับ Home เมื่อพิสูจน์ได้ว่าแกนยืนนิ่งจาก statusword ที่ fresh (ถ้าขอ Home ก่อนอ่าน จะถูก refuse ด้วย `OBSERVATION_NOT_FRESH` และแกนจะไม่มีวัน move-ready) ดังนั้น prepare อ่านก่อนแล้วค่อยขอ Home และถ้าอ่านไม่ทันจะ refusal แบบใหม่ `DRY_PREPARE_REFUSED_NO_OBSERVATION` — **ห้ามแก้ด้วยการลบ `CheckStationary` หรือใส่ค่า observation ปลอม** นี่คือกันชนที่ห้าม homing แกนที่มองไม่เห็น
- **ทุก prepare refusal พูดได้เต็มปากแล้ว:** `DRY_PREPARE_REFUSED_PORT_NOT_READY`, `_MOTION_FAULTED`, `_NO_OBSERVATION`, `_NOT_MOVE_READY` ต่อท้ายด้วยข้อเท็จจริงที่ session เผยแพร่เอง: `Session state=`, `active operations=` และต่อแกน `home=`, `motion=`, `statusFresh=`, `tpFresh=`/`rpFresh=`, `active=Kind/Phase`, `last=<canonical code>`, `log=<code>` ⇒ เห็น `HOME_TIMEOUT` / `OBSERVATION_NOT_FRESH` / `home=HomedUnverified` จาก `out.txt`/`err.txt` ได้ทันทีโดยไม่ต้องแตะฮาร์ดแวร์เพิ่ม และไม่มี endpoint / path / consent phrase ปนอยู่ (อ่านจาก snapshot อย่างเดียว)
- ถ้า `last=HOME_TIMEOUT` หรือ `home=HomedUnverified` ปรากฏบนโต๊ะ:แก้ที่ input (profile: `homeSpeed`, `homeTimeoutMilliseconds`, `positionTolerance`, `brakeSettleMilliseconds`, `observationIntervalMilliseconds`, `observationFreshnessMilliseconds`) ห้ามผ่อนตัวตรวจในโค้ด และห้ามรันซ้ำโดยยังไม่อ่าน `err.txt` ทั้งก้อน
- **addendum (บันทึกเพิ่ม ไม่ทับของเดิม) — `S1` หน้าต่าง prepare จบเองได้เมื่อการ Home ตัดสินไปแล้ว:** เดิมที่วง homing ของ `CleaningPathDryComposition.Prepare` ออกได้ 2 ทางเท่านั้น (session faulted หรือสองแกน move-ready) ทำให้อีกแกนหนึ่ง Home จบเป็น `HomeFailed`/`HomedUnverified` หรือถูกปฏิเสธตั้งแต่ก่อนยิงคำสั่ง กระบวนการจะเงียบวนครบงบ `24000 × 5 ms` (~2 นาที) ทั้งที่ผลลัพธ์ปิดตายแล้ว — บนโต๊ะ Dell รอบที่ 2 ที่ `0bd5024` คือเคสนี้ (เจ้าของปิดหน้าต่างก่อนที่ `Prepare` จะ return จึงไม่เคยเห็นบรรทัด refusal เลย) ตอนนี้อ่าน log watermark ของ session ก่อนขอ Home แล้วตรวจว่า *ทั้งสอง* แกนจบการ Home แล้วจริง (มี Home-kind log entry หลัง watermark, ไม่มี operation ค้าง, ไม่ได้อยู่กลาง `Homing`) ถ้าจบทั้งคู่และฝ่ายใดฝ่ายหนึ่งไม่ถึง `Homed` จะออกจากวงทันที ⇒ ได้ `WJSS dry prepare refused [DRY_PREPARE_REFUSED_NOT_MOVE_READY]: ...` ภายในไม่ถึง 1 วินาทีหลังแกนหยุด **code เดิม, งบเดิม, ไม่มีคำสั่งใหม่ ไม่มี retry ไม่มี Stop เพิ่ม** (ถ้าอีกแกนยัง moving อยู่ วงจะรอให้แกนนั้นจบตัวเองก่อน เพื่อไม่คืนค่า false ขณะมีแกนค้างกลาง operation — `dry.Shutdown()` ใน `finally` ของ trial ยังเป็นเจ้าของการหยุดทุกกรณี)
- **addendum — `D2` diagnostic พิมพ์ตัวเลขที่การตัดสิน zero อ้างอิงจริง:** ต่อแกนมี `tp=<value>(fresh=<bool>)` และ `rp=<value>(fresh=<bool>)` (ค่า native ดิบ ตามที่ session เผยแพร่ ไม่มี scaling, ไม่มีหน่วย, ไม่มี label ที่ยังยืนยันไม่ได้) พร้อม `coord=<CoordinateState>` และ `homeInput=<Active|Inactive|Unknown>` ข้างหลังกุญแจเดิม `home=`, `motion=`, `statusFresh=`, `tpFresh=`, `rpFresh=`, `active=Kind/Phase`, `last=`, `log=` ⇒ อ่านจาก `err.txt` แล้วตัดสินได้เลยว่า Home แตะสวิตช์แล้ว zero ถูก verify หรือไม่ โดยไม่ต้องเปิด debugger
- **addendum — ตารางตัดสินจากค่าที่พิมพ์ (ใช้ตอนส่งหลักฐานกลับมาให้ผม):**
  | สิ่งที่เห็น | ความหมาย | จุดแก้ |
  | --- | --- | --- |
  | `home=HomeFailed last=HOME_ZERO_VERIFICATION_FAILED` + `tp=`≈0 แต่ `rp=` ไม่ ≈0 | `DP A=0`/`DP B=0` มีผลกับรีจิสเตอร์เดียว แต่ gate ของ contract ตรวจทั้งสองรีจิสเตอร์ | ห้ามแก้ด้วยการลบตัวตรวจ — ต้องขอรuling `D1`/`D3` จาก Owner พร้อมตัวเลขนี้ |
  | `home=HomedUnverified last=HOME_COMPLETED_ZERO_NOT_VERIFIED` | ไม่เกี่ยวกับ DP/DE เลย: `positionTolerance` หรือ `stopTimeoutMilliseconds` ใน profile ว่าง ⇒ `HasZeroVerification=false` | แก้ที่ profile แล้วรันใหม่ |
  | `home=HomeFailed last=HOME_TIMEOUT` | ไปไม่ถึงสวิตช์ใน `homeTimeoutMilliseconds` | `homeSpeed` / งบเวลา / สถานะฮาร์ดแวร์ |
  | `home=NotHomed last=HOME_INPUT_ALREADY_ACTIVE` (แกนนอนบนสวิตช์อยู่แล้ว) | เป็นพฤติกรรม *ตามแบบ* ของ H1 §3 (input ที่ active ไม่พิสูจน์ readiness และไม่ถอยแกนให้อัตโนมัติ) | จ็อกแกนออกจากสวิตช์ด้วยมือแล้วยิง Home ใหม่ |
- **addendum — บันทึก ruling R1 ของ Owner (Home กำหนดทั้งสองรีจิสเตอร์) และสิ่งที่แก้จริงที่ head นี้:** ในหนึ่ง Home ของทุก session ตอนนี้ Product จะส่ง `DP<axis>=0` แล้วตามด้วย `DE<axis>=0` ที่ตำแหน่งซึ่งสวิตช์ทำงาน โดยส่ง**หลังจากยืนยันว่าแกนหยุดจริงแล้ว** (ทั้งสองคำสั่งอยู่ท้ายเฟส `DefiningZero` เดิม แต่ **คนละ step** เพื่อคงกติกาหนึ่ง port call ต่อ step ของ `AdvanceActives` และงบหน้าต่าง prepare ไว้) จากนั้น `DoVerifyingZero` ตรวจ**ทั้งสองรีจิสเตอร์**ภายใต้ `positionTolerance`, เงื่อนไข `Sequence > ZeroSequence` และ freshness แบบเดิมทุกประการ โดยขยับ barrier ไปเป็น sequence ของคำสั่ง `DE` (คำสั่งสุดท้าย) เพื่อไม่ให้อ่านค่าเก่ากลายเป็นหลักฐานของรีจิสเตอร์ encoder ถ้า controller ปฏิเสธ `DE` จะได้ `home=HomeFailed last=HOME_ENCODER_ZERO_REFUSED` แล้วเดินเส้นทาง brake/stop เหมือนการปฏิเสธอื่น — **ไม่มีการผ่อน gate ใด และ code `HOME_INPUT_ALREADY_ACTIVE` ไม่ถูกแตะ**: ruling R2 (ยอมให้ Home ทั้งที่ input เป็น `Active`) ยังไม่ถูกอนุมัติเป็น code เพราะ block นั้นถูกบันทึกใน H1 §3 ว่าเป็น Fast H1 Repair (Owner-approved 2026-10-26) และมี 5 tests ยืนยันอยู่ ⇒ ที่โต๊ะยังต้องจ็อกแกนออกจากสวิตช์ก่อนยิง Home ตามเดิม และ ruling R3 (อัตราส่วน tp:rp = 2:1) เลื่อนไว้รอบถัดไป เพราะที่ตำแหน่ง 0 ตัวคูณไม่มีผล (`0 × 2 = 0`) จึงไม่ใช่วิธีแก้ `D-sim` และ branch นี้ไม่มีหลักฐาน vendor หรือการวัดจริงรองรับเลขนั้น
- **addendum — ค่า default ของคีย์ profile ตัวใหม่:** เขียน `autoResetEncoderOnHome` ลง profile หรือไม่เขียนเลยก็ได้ โดย**การไม่เขียนเท่ากับ true** ⇒ โปรไฟล์เดิมบนโต๊ะที่ `D:\wjss-artifact` ไม่ต้องแก้ไฟล์ แล้ว stage `D-sim` จะได้ `DP`+`DE` อัตโนมัติ (นี่คือเงื่อนไขที่ Owner สั่ง) ตั้งเป็น `false` เฉพาะเมื่อต้องการดู drift โดยไม่ให้ Home fail เพราะโหมดนั้นจะ verify เฉพาะรีจิสเตอร์ที่ Home กำหนดจริง ส่วนค่า `tp=` / `rp=` ที่พิมพ์ใน `err.txt` ยังเป็นตัวเลขดิบเดิม ไม่มีการ scaling ใด ๆ
- **addendum — แถวเพิ่มสำหรับอ่าน transcript รอบถัดไป:** `last=HOME_ENCODER_ZERO_REFUSED` หมายความว่า controller ปฏิเสธคำสั่ง `DE<axis>=0` ที่ Home ส่งไป ⇒ Home ไม่ถูกนับ และต้องแก้ฝั่ง controller/parameter ไม่ใช่ไปลบตัวตรวจ นอกจากนี้ **แถวแรกของตารางตัดสินด้านบน (กรณี `tp=` ≈ 0 แต่ `rp=` ไม่ ≈ 0) ถูกรับผิดชอบโดย ruling R1 แล้ว** — อาการนั้นคือสิ่งที่ R1 แก้ ถ้ายังเห็น `tp=` ไม่ ≈ 0 ทั้งที่ Home ส่ง `DE` สำเร็จ แปลว่ารีจิสเตอร์จริงที่ `DP`/`DE` เขียน สลับกับที่ model ใน branch นี้คาด ให้ส่งตัวเลข `tp=` / `rp=` พร้อม `last=` มา ผมจะประเมินจาก source เท่านั้น และ**ห้าม**แก้ด้วยการขยาย tolerance เด็ดขาด
- **addendum — บล็อกที่ Owner ใช้ที่โต๊ะ (รอบที่ 3) สำหรับ `stage-D-sim`:** (1) ตรวจ `git rev-parse HEAD` ให้เท่ากับ sha ที่ผมรายงาน และ `Get-FileHash` ของ exe ตรงกับที่ผมแจ้ง (2) ไม่ต้องแตะไฟล์ profile ใดทั้งสิ้น (3) เปิด `wsb1-hardware` จ็อก `-`/`+` ให้แกนนอนและแกนตั้งหลุดจาก limit switch ทั้งสองแกน แล้วกด Home หนึ่งครั้งต่อแกน โดยคงติ๊ก "Home defines both registers (DP, then DE)" ไว้ (4) ปิดหน้าต่างแล้วรัน `stage-D-sim` ใหม่ (5) ส่ง `out.txt` + `err.txt` ทั้งก้อนแบบบรรทัดต่อบรรทัด เป้าหมายคือบรรทัด `prepare=DRY_PREPARE_READY` และ `terminal_status=CompleteAwaitingSafeReturn`; ถ้ายัง `home=HomeFailed` ให้ดูว่า `last=` เป็น code ใดก่อน ผมจะประเมินจาก source เท่านั้นแล้วค่อยปล่อยรอบถัดไป
- **addendum — ดูค่าสดบนหน้าจอ commissioning ได้ (ใช้ตอนระหว่าง run):** app เดียวกับที่ใช้ jogging มี row `Position TP (raw)`, `Position RP (raw)`, `Home input bit`, `Coordinate (DP)`, `Dual zero (DE)`, `Last outcome` ของทุกแกน (`apps/motion-commissioning/ManualMotionAxisStateGrid.cs:81-89, 245-250` — โหมด `wsb1-hardware`, `apps/motion-commissioning/Program.cs:118`) และหน้า H1 พิมพ์บรรทัด `life <HomeState>/<CoordinateState>/<StopState> ... TP <value> FRESH | RP <value> FRESH ... last <code>` (`apps/motion-commissioning/H1DryCommissioningForm.cs:376-381` — โหมด `h1-wsb1-hardware`, `Program.cs:36`) การ launch ยังเป็น `dotnet run --project apps/motion-commissioning/Wjss.MotionCommissioning.csproj -c Release --no-build -- <mode>` ตาม `docs/MANUAL_MOTION_COMMISSIONING_RUNBOOK.md:262, 287` เท่านั้น และ **ไม่มีสคริปต์ชื่อ `Launch-H1-WSB1-Hardware.ps1` ใน repository นี้** (บันทึกไว้กันหาไม่เจอ — ชื่อที่แจ้งมาในการรีวิวรอบก่อนไม่ใช่ไฟล์จริงของ branch นี้)

### ข้อ 6.3 — variant สี่: `D-dry` = ปั๊มจริง + วาล์วจริง (โหมดเปียก ต้อง ack แยก)

Product บังคับ `WJSS_DRY_WET_SEQUENCE_ACK=WATER_AT_NOZZLE_AUTHORIZED` กับเส้นทางที่มีปั๊มจริง (ตัวขับตั้งให้หลังคุณพิมพ์ `DRY CYCLE AUTHORIZED` เท่านั้น และปฏิเสธก่อนเรียก exe ถ้าคุณไม่ตอบ) เพราะโหมดนี้เปิดวาล์วขณะปั๊มสร้างความดัน: **น้ำอาจพุ่งออกจาก nozzle ได้** ถ้า env นี้ขาด Product จะปฏิเสธด้วย `DRY_COMPOSITION_REFUSED_WET_SEQUENCE_NOT_ACKNOWLEDGED`

### ข้อ 6.4 — รายละเอียดของ `D-dry` (คงเดิม + key ที่เพิ่ม 2 คู่)

ปิด gate: วงจร dry ทั้งรอบ (select pump → enable → run → AO ramp → supervision → cleanup → release)
**เงื่อนไขก่อนรัน:** B ผ่าน (รู้ขั้วสัญญาณแล้ว) + C ผ่าน + เจ้าของตัดสินใจ `docs/ACCELERATED_CLEANING_DRY_TRIAL_PACKAGE.md` §6/§7 แล้ว + Motion ยังไม่มีแรง (no-load)

```powershell
& "$script:Ev\stage-D-dry.ps1"
```
สิ่งที่บล็อกทำจริง (ตัวโค้ดคือ `stage-D-dry.ps1` ที่ export มาจากตัวขับ — ดูด้วย `Show-WjssStage -Name 'D-dry'`; ข้อความนี้ generate จากโค้ดจริงของตัวขับ จึงไม่เขียนค้าง):

- ต้องยืนยันกับ Owner ก่อน: พิมพ์ `DRY CYCLE AUTHORIZED` ตามที่ prompt ทวงถาม ( prompt: D: Stage B and C passed, the ACCELERATED package owner decisions are recorded, motion unloaded ) — ถ้าไม่ตอบตรงตัวบล็อกจะ**ไม่เรียก exe เลย**
- consent ที่บล็อกขอ: `PhysicalDoSingle` (อ่านแบบซ่อน, ลบทิ้งหลังจบบล็อก)
- exit code ที่ยอมรับ: `0` เท่านั้น
- env ที่บล็อกตั้ง: `WJSS_DRY_COMMAND_MODE=TEST_HARDWARE_DRY`, `WJSS_DRY_WET_SEQUENCE_ACK=WATER_AT_NOZZLE_AUTHORIZED`, `WJSS_PUMP_SELECTION=1`, `WJSS_PUMP_FRESHNESS_MS=2000`, `WJSS_PUMP_RUNNING_CONFIRM_MS=10000`, `WJSS_PUMP_READY_TIMEOUT_MS=30000`, `WJSS_PUMP_STOPPED_CONFIRM_MS=15000`, `WJSS_PUMP_READY_SETPOINT_BAR=15`, `WJSS_PRESSURE_LOW_BAR=1`, `WJSS_PRESSURE_HIGH_BAR=15`, `WJSS_VALVE_SUPERVISION_MS=5000`
  และตั้งจากคำตอบของคุณบนหน้าจอ: `WJSS_CLEANING_SENSOR_ID`, `WJSS_PUMP_ANALOG_TARGET_MA`, `WJSS_PUMP_RUNNING_POLARITY`, `WJSS_PUMP_FAULT_POLARITY`
- key ที่**ต้องตรง**ตามนี้ (22 คู่): `profile=TEST_HARDWARE_DRY`, `mode=WJSS_DRY_COMMAND_MODE`, `prepare=DRY_PREPARE_READY`, `terminal_status=CompleteAwaitingSafeReturn`, `terminal_code=PATH_AND_VALVE_SEQUENCE_COMPLETE_AWAITING_SAFE_RETURN`, `primary_failure=NONE`, `cleanup.pump.stopped_confirmed=True`, `cleanup.pump.ownership_released=True`, `cleanup.pump.complete=True`, `cleanup.motion.complete=True`, `cleanup.valves.complete=True`, `cleanup.valves.refused=0`, `cleanup.valves.transport_failures=0`, `cleanup.valves.unknown=0`, `cleanup.complete=True`, `pump_ready_source=AI-003+`, `pump_feedback=COMMISSIONED`, `pump_gate=PUMP_PRECONDITION`, `pump_evidence_kind=PHYSICAL`, `physical_pump_verified=True`, `write_authority=PROCESS_IO_SESSION`, `coordinators=COMPOSITION+EXECUTION`
- key ที่แนบมาให้ผมดูด้วย (ไม่ใช้ตัดสิน pass/fail): `sensor`, `isolation_valve`, `water_jet`, `axes`, `catalogue_rows`, `endpoint`, `process_io_device`, `pump_selection`, `pump_owner`, `pump_readiness`, `start`, `steps`, `session_state`, `session_generation`, `cleanup.pump.attempted`, `cleanup.pump.terminal`, `cleanup.pump.state`, `cleanup.motion.session`, `cleanup.motion.close`, `cleanup.valves.attempted`, `cleanup.valves.closed`, `motion_port`, `motion_profile`

- 4 ค่าที่ผมห้ามเดาแทน (sensor id, mA target, polarity x2) ตอนนี้จะ**ถามบนหน้าจอ**ตอนรัน stage นั้น และตรวจรูปแบบให้ก่อน (ค่าว่างไม่ได้, ทศนิยมต้องใช้จุด ไม่ใช่จุลภาค)
- ถ้าพิมพ์ผิดรูป script จะถามซ้ำจนกว่าจะได้ค่าที่ถูกต้อง; ค่าที่ยังไงก็ไม่ถูกหลักจะถูก Product ปฏิเสธด้วย `exit=4` + refusal ที่บอกชื่อตัวแปร (ไม่ได้เดาแทนคุณ)
- **ห้าม re-run เพื่อหวังผล luck**: ถ้า `steps` หมด budget แล้วไม่มี terminal state ให้ส่ง log ทั้งก้อน
- cleanup ของ trial = ramp ลง → DO‑007 FALSE → ตรวจ → DO‑003 FALSE → ตรวจ → รอ not-running → ปล่อย ownership (script ตรวจ `cleanup=` จาก log ให้ผมดูอีกชั้น)

## 7. Stage E — WSB limit switches (Upper/Lower) + FC02/FC01 divergence probe

ปิด gate: `DI mirroring / limit-switch reclass` ตาม corrected CSV (DI‑021..028 = Upper, DI‑029..036 = Lower)
Legacy rev2 อ่าน bit กลุ่มนี้ผ่าน **FC01 (coils)** ส่วน Product อ่านผ่าน **FC02 (discrete inputs)** ที่ address เดียวกัน — บล็อคนี้วัดทั้งสองทางให้เห็นจริง (อ่านอย่างเดียว)

```powershell
& "$script:Ev\stage-E-idle.ps1"
```
สิ่งที่บล็อกทำจริง (ตัวโค้ดคือ `stage-E-idle.ps1` ที่ export มาจากตัวขับ — ดูด้วย `Show-WjssStage -Name 'E-idle'`; ข้อความนี้ generate จากโค้ดจริงของตัวขับ จึงไม่เขียนค้าง):

- อ่านบิต: FC02 (read discrete inputs) ที่ start=20 count=16 ⇒ address 20..35; FC01 (read coils) ที่ start=20 count=16 ⇒ address 20..35
- ผลอ่านแต่ละช่วง print ผ่าน `Save-WjssSend` (scrub แล้ว) — ถ้า device ไม่รองรับ function code นั้น บรรทัดนั้นจะขึ้น `READ FAILED - protocol exception N` (N คือ code ที่ device ตอบ แบบทศนิยม) ซึ่งผมใช้ตัดสิน gate ได้ **ห้ามแก้ไฟล์ที่ export มาแล้ว**

จากนั้นให้ Owner ขยับ WSB ทีละตัว (หรือให้ปั๊ม/วาล์วทำงานในจังหวะที่มี limit switch เปลี่ยน) แล้วรันซ้ำ:

```powershell
& "$script:Ev\stage-E-moved.ps1"
```
สิ่งที่บล็อกทำจริง (ตัวโค้ดคือ `stage-E-moved.ps1` ที่ export มาจากตัวขับ — ดูด้วย `Show-WjssStage -Name 'E-moved'`; ข้อความนี้ generate จากโค้ดจริงของตัวขับ จึงไม่เขียนค้าง):

- อ่านบิต: FC02 (read discrete inputs) ที่ start=20 count=16 ⇒ address 20..35; FC01 (read coils) ที่ start=20 count=16 ⇒ address 20..35
- ผลอ่านแต่ละช่วง print ผ่าน `Save-WjssSend` (scrub แล้ว) — ถ้า device ไม่รองรับ function code นั้น บรรทัดนั้นจะขึ้น `READ FAILED - protocol exception N` (N คือ code ที่ device ตอบ แบบทศนิยม) ซึ่งผมใช้ตัดสิน gate ได้ **ห้ามแก้ไฟล์ที่ export มาแล้ว**

- FC02 เปลี่ยนตามการขยับ + FC01 เปลี่ยนตรงกัน → binding ปัจจุบันถูกต้อง ✓
- FC02 error/ค้างคงที่ แต่ FC01 เปลี่ยน → device map DI ไว้ที่ coil space (ตาม legacy) → **นี่คือข้อมูลเพื่อสั่ง `WJSS_CONFIG` เปลี่ยน function code** ห้ามใส่ fallback อัตโนมัติในโค้ด ผมจะเป็นคนเขียนบันทึกให้หลังคุณส่ง log

## 8. Stage F (optional) — Motion WSB no-load stop

ใช้ `docs/MOTION_STOP_TRIAL_RUNBOOK.md` ตามเดิม (เป็นเอกสาร software-prep + ลำดับหน้างาน) — ไม่ทำก็ปิด gate หลักได้ครบ

## 9. ลำดับ stop / cleanup มาตรฐาน (ทุกกรณีที่ต้องหยุดกะทันหัน)

1. Panel: **Run (DO‑007) = FALSE**  2. รอ/ยืนยันว่าปั๊มไม่หมุนแล้ว  3. **Enable (DO‑003) = FALSE**  4. ถ้า Stage C/D เปิด IV ไว้ ให้ trial ทำ close เอง — **อย่าปิด manual ซ้ำ** ถ้า trial ยังถือ session อยู่ (มันจะทำ cleanup เอง แล้ว `session_state=CLOSED`)  5. ส่ง log ที่ค้างให้ผมก่อนแตะอะไรต่อ

## 10. refusal / exit map (ย่อ — เจอแล้วไม่ต้องเดา)

| ที่เห็น | ความหมาย | ทำอะไรต่อ |
| --- | --- | --- |
| `RESULT ..: PASS` | ตรง contract ทุก field | ไป stage ถัดไป |
| `CHECK - exit=4` | startup refused (env/consent/profile/selector) | ตรวจบล็อค stage นั้นว่าวางครบ/ค่าตรงตัวพิมพ์ใหญ่ |
| `CHECK - <key> missing` | trial ไม่พิมพ์ key นั้น (แปลว่าหยุดก่อนถึงขั้นนั้น) | ส่ง log ให้ผม |
| `PUMP_READY_REFUSED_AT_OR_BELOW_SETPOINT` ที่ A2 | แรงดันจริง ≤ 15 bar | รอปั๊มขึ้นแล้วซ้ำ (ไม่ผิด) |
| `*_REFUSED_READ_ONLY_CONSENT` / `_CONSENT_MISSING` | phrase ไม่ตรง exact | พิมพ์ใหม่จาก constant (อย่า copy จากแชท/doc) |
| `IV_TRIAL_REFUSED_*_NOT_FIELD_CONFIRMED` / `CONTRADICTORY` | สั่งงานแล้ว feedback ขัดกัน | หยุด — fail-closed ทำงาน |
| `exit=2` + `[TOPO_TARGET_WALL_CONTRADICTION]` | แถวใน CSV ระบุ tier/wall ขัดกับ WJ ที่อ้าง (ดู `Importer detail:` ว่าแถวไหน) | แก้ `cannon` ของแถว `G` ให้เป็น 3/4/1/2 ตาม ruling 2026-11-11 แล้วรันบล็อกเดิมซ้ำ — ห้ามแก้โค้ดเพื่อผ่าน |
| `exit=2` + `[DRY_COMPOSITION_REFUSED_MOTION_CABINET_MISMATCH]` | profile motion ที่โหลดเป็นคนละ cabinet กับที่ path ของ sensor นั้นอยู่ | เปลี่ยน `WJSS_MANUAL_MOTION_PROFILE` ไปไฟล์ของ WSB1 (โต๊ะนี้) แล้วส่ง log กลับ ถ้าคิดว่า binding ผิดให้หยุดส่งผมก่อน |
| poller `READ FAILED - connect timed out` | IP/ firewall / ไม่ได้เสียบสาย | ตรวจ network แล้วรันบล็อกอ่านบิตซ้ำ เช่น `& "$script:Ev\stage-B-off.ps1"` (อ่านอย่างเดียว ปลอดภัย) |

## 11. สรุปว่า stage ไหนปิด gate อะไร (ใช้ตอนผมอัปเดต register)

| Stage | Gate ที่ปิด | สถานะล่าสุด (แถว A/B/C/E อ้าง `c4898a0`, แถว D อ้าง `16f78bc`/`44820ef`) |
| --- | --- | --- |
| A1 + A2 | REAL WAGO ENDPOINT CONNECTIVITY · Pump-ready strict-greater ทั้งสองฝั่งบนสัญญาณจริง | **CLOSED** (A1 0.669 bar ⇒ FALSE, exit 2; A2 15.6047 bar ⇒ TRUE, exit 0, `physical_pump_verified=TRUE`) |
| B | Pump running/fault polarity commissioned (absent ⇒ NOT_CONFIGURED ⇒ D ถูก refuse) | **CLOSED** (DI-004 0→1 ⇒ `ACTIVE_HIGH`; DI-005 ไม่เปลี่ยน ⇒ `ACTIVE_HIGH` ตาม convention เท่านั้น) |
| C | IV physical actuation + FC01 read-back | **CLOSED** — field-confirmed ทั้งสองทางด้วย limit switches; `fc01=False` ที่ address 22 อธิบายด้วยกติกา RW mirror +512 (อ่านที่ 534) จึงไม่ใช่ defect |
| D-pump-mv | บันไดปั๊มจริง + Motorized Main Valve (บล็อก 1.27) ที่ Product สั่งเองทั้งเปิดและปิด | **CLOSED ที่ `16f78bc` (2026-11-11 01:33, driver 2026-11-20.1 + patch โปรไฟล์, exe sha256 `492BCB9DE0CF`)** — `RESULT D-pump-mv: PASS (exit=0)`: Product เปิดวาล์วเอง (`main_valve_open_source=PRODUCT_DISPATCH`, `position_at_start=CLOSED`) ยืนยันด้วย FC02 แล้วค่อย Select Pump, ปิดหลัง `pump_stop_confirmed=True` จนได้ `position_at_end=CLOSED` + `coils_resting=True`, `over_torque=False`, `position_lost=False`, `supervision_fault=False`, `refusal=NONE`; ลำดับ 6 การเขียนตรง invariant (FALSE ของคอยล์ฝั่งตรงข้ามก่อนเสมอ + ปล่อยคอยล์ที่ลิมิต) และ `dispatch_summary` สอดคล้อง; แรงดันจริง 15.4631 bar > 15 (raw 12667, `ai003_pump_ready_rule=ACCEPTED`), `transport_errors=0`, `network_attempts=1550`; เวลาเดินทาง 30399.395/29609.902 ms ⇒ ตัวขับตั้ง timeout เป็น 45000 (driver `2026-11-21.1`); หลักฐานเต็มอยู่ข้อ 6.1.2 — **ยังต้อง build+test เขียวที่ SHA ล่าสุดของ branch ก่อนนับเป็นรอบปิด gate** |
| D-pump | บันไดคำสั่งปั๊มอย่างเดียว โดยไม่มีเส้นทางวาล์วใน process เลย | **OPEN** — ที่ `ee69196` บันไดถูกตัดสินถูกต้องแล้ว (exit=2 + CHECK แทน exit=0 ปลอม) และเจอต้นเหตุจริงชั้น boundary: `now` ที่ใช้ acceptance ถูกจับก่อนการอ่าน ⇒ Usable=false เสมอบนของจริง (แก้พร้อมกันทั้ง ReadFeedback/ReadPumpOutletPressure + test นานาฬิกา ticking) — รันซ้ำที่ `1eb65a6` แล้ว: acceptance ที่แก้ทำงานจริงบนฮาร์ดแวร์ (`analog_ramp_executed=True`, `last_running_usable=True`, `transport_errors=0`) แต่รอบที่ exit 0 เกิดเพราะวาล์วถูกเปิดนอก Product ด้วยมือ จึงยังปิด gate ไม่ได้ — หลักฐานวาล์วต้องมาจาก `D-pump-mv`; ฝั่งโต๊ะ: `dispatch_window_ms`/`written=` ยังต้องดูจาก out.txt ⇒ **ปิด gate แล้วโดยอ้างหลักฐาน `D-pump-mv` ที่ `16f78bc` เป็นหลัก** (วาล์วเปิดโดย Product เอง, 7 dispatches `MATCH_OVER_7`, `written=` ตรง bound ทุกบรรทัด, `transport_errors=0`) ส่วนรอบ manual ของ `D-pump` ที่ `1eb65a6` ถูกบันทึกเป็นหลักฐานเสริมเท่านั้น ไม่ถูกนับเป็นหลักฐานวาล์ว |
| D-sim | Path จริง + วาล์วจริง บนค่าแรงดันที่ประกาศ (ติดป้าย SOFTWARE_SIMULATED) | **OPEN — Owner กำลังรันที่โต๊ะ (อนุมัติข้อ ข. 2026-11-11)** · ต้อง build ที่ SHA ใหม่ก่อน · ต้องเห็น `pump_evidence_kind=SOFTWARE_SIMULATED` + `simulated_pump_ready_rule=DECLARED_ABOVE_SETPOINT` (สองชื่อนี้คือสิ่งที่ Product พิมพ์จริง; `SOFTWARE_SIMULATED_AI003` เป็นค่าของตัวเลือก `WJSS_DRY_SIMULATED_PUMP_READY` ไม่ใช่ค่าที่พิมพ์ออก) + `prepare=DRY_PREPARE_READY` + `terminal_status=CompleteAwaitingSafeReturn` + `primary_failure=NONE` + IV เปิด/ปิดครบตาม `isolation_valve` · gate D จะปิดเมื่อ `.out.txt`/`.err.txt` ก้อนนี้ถูกบันทึก |
| D-dry | integrated dry cycle แบบเปียก (ปั๊มจริง + วาล์วจริง) no-load ครบลูปพร้อม cleanup | **WAIVED (Owner 2026-11-11 ข้อ ค.) — ไม่ใช่ merge blocker ของ PR #45** · เหตุผล: แยกพิสูจน์ปั๊ม/วาล์วหลักออกจาก path เพื่อไม่ให้น้ำออกหัวฉีดช่วง dry · stage ยัง export ได้และยังต้อง `WJSS_DRY_WET_SEQUENCE_ACK` ถ้าจะรัน |
| E | WSB Upper/Lower reclass + DI read path (FC02 vs FC01) | **CLOSED** สำหรับเส้นทางอ่าน (ยังไม่ทำ per-switch E ตามที่ตกลง) |
| F | (optional) motion stop behaviour | **WAIVED (Owner 2026-11-11 ข้อ ค.) — ไม่ใช่ merge blocker** · ใช้ `docs/MOTION_STOP_TRIAL_RUNBOOK.md` ตามเดิมเมื่อต้องการ |

**ส่งผมทุก stage:** ก้อนข้อความใต้ `--- copy everything below ---` (scrub แล้ว) + log ดิบเฉพาะก้อนที่มี `CHECK` — ห้าม commit ไฟล์ log/ผลลัพธ์เข้า repo เด็ดขาด
**สถานะ (อัปเดต 2026-11-11 หลัง `D-pump-mv` PASS):** A/B/C/E ปิดแล้วที่ `c4898a0` (ห้ามรันซ้ำ) · **D-pump กับ D-pump-mv ปิดแล้ว**
ที่ `16f78bc` — วาล์ว Main ถูก Product สั่งเปิด/ปิดพร้อมหลักฐาน FC02 ครบ (ดูข้อ 6.1.2) · D ยังเหลือ `D-sim` (ต้อง build ใหม่หลัง
เพิ่มโค้ด/tests) และถ้าจะเอา `D-dry` (wet) ด้วยต้องอ่านข้อ 6.2/6.3 ก่อน · ตัวขับเป็น `2026-11-21.1` (แก้เส้นเลือกโปรไฟล์ให้
`D-pump-mv` + ขยับ timeout วาล์วเป็น 45000 + self-test กลุ่มใหม่) · ข้อ ก. ของ Owner **ปิดแล้วโดยไม่แก้ C#**: ฝั่ง Product ไม่มี
ค่า default ให้ขยับ (env หาย = refuse) มีแค่ doc comment ที่เขียนเลขค้างไว้ซึ่งอัปเดตแล้ว · `D-dry` กับ `F` ถูก waive ตามข้อ ค.
⇒ เหลือ `D-sim` (ข้อ ข.) เป็นรายการเดียวที่ยังค้ำ gate D · PRODUCTION NOT AUTHORIZED ·
MERGE HOLD บน PR #45 จนกว่าผมจะสรุปผลครบทุก stage และคุณจะสั่ง merge เอง
