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
. Join-Path $Repo 'tools/hardware-runbook/wjss-field-driver.ps1'
Test-WjssDriver
Export-WjssStageFiles
```

`Test-WjssDriver` ไม่ใช่ formal check — มัน**รันโค้ดจริงบน PowerShell เครื่องคุณ** (ตัวเดียวกับที่จะรัน stage):

- parse ไฟล์ตัวขับและ parse ไฟล์ `stage-*.ps1` ทั้ง 8 ไฟล์ที่ export ออกมา (syntax ผิด = เจอตรงนั้นเลย)
- ตรวจว่า stage ครบ 8 ตัว ไม่มี consent literal/endpoint literal หลงอยู่ในโค้ดของ stage
- รัน `& <exe>` จริงใน mode startup-refusal (ไม่แตะ device) แล้วดูว่า exit code/ refusal มาจริง
- เทียบตัวอ่าน log, ตัวตัดสิน verdict, ตัว scrub IP กับ log สังเคราะห์ (บรรทัดที่มี `=` ซ้ำในค่า, junk line, key ซ้ำ, ค่าที่พิมพ์ตัวพิมพ์ใหญ่ต่างกันเลยต้องถูกจับ)
- ตรวจ frame ที่ใช้อ่านบิต: ความยาว 12 ไบต์, protocol id ต้องเป็น 0, length 6, start/quantity เป็น big-endian — พร้อมเคส reply ที่ device ตอบ exception / สั้นเกิน / function code ไม่ตรง
- เทียบ keys ที่ใช้ใน `-Expect`/`-Show` กับ keys ที่ trial นั้นพิมพ์จริง และ grep ชื่อ `WJSS_*` ทุกตัว + ค่าที่ผม assert ทุกตัวว่ามีอยู่ใน source ของ Product จริง

อะไรก็ตามที่ grep ไม่เจอ = `DRIVER SELF-TEST FAILED` แล้วตัวขับจะ**ไม่ปล่อยให้รัน stage**

ส่วนบรรทัด `Export-WjssStageFiles` คือตัวสร้าง `$env:USERPROFILE\wjss-evidence\<วันที่>\stage-<name>.ps1` ทั้ง 8 ไฟล์ ซึ่งเป็นสิ่งที่รันจริง (ห้ามแก้เอง)

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

รันตามลำดับนี้ **ทีละบรรทัด** (ไฟล์ทั้ง 8 เกิดจาก `Export-WjssStageFiles` ในข้อ 0.3 — ห้าม copy โค้ดจากเอกสารนี้ไปรันเอง และห้ามแก้ไฟล์ `stage-*.ps1`) — A1 รันตอนปั๊มหยุด, A2 รันตอนปั๊มรัน, B off/on เช่นกัน, ห้ามรันคู่ที่ติดกันแล้วลืมเปลี่ยนสถานะปั๊ม:

```powershell
& "$script:Ev\stage-A1-off.ps1"     # pump STOPPED, exit 2 expected - that is the PASS
& "$script:Ev\stage-A2-run.ps1"     # pump RUNNING, exit 0 + pump_ready=TRUE
& "$script:Ev\stage-B-off.ps1"      # read DI-001..008, pump stopped
& "$script:Ev\stage-B-on.ps1"       # read DI-001..008, pump running - I diff the two
& "$script:Ev\stage-C-iv1.ps1"      # IV1 open/close, one actuation
& "$script:Ev\stage-D-dry.ps1"      # full dry cycle, asks you for 4 values
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

## 6. Stage D — Integrated cleaning-path DRY cycle (actuation เต็มรูป)

ปิด gate: วงจร dry ทั้งรอบ (select pump → enable → run → AO ramp → supervision → cleanup → release)
**เงื่อนไขก่อนรัน:** B ผ่าน (รู้ขั้วสัญญาณแล้ว) + C ผ่าน + เจ้าของตัดสินใจ `docs/ACCELERATED_CLEANING_DRY_TRIAL_PACKAGE.md` §6/§7 แล้ว + Motion ยังไม่มีแรง (no-load)

```powershell
& "$script:Ev\stage-D-dry.ps1"
```
สิ่งที่บล็อกทำจริง (ตัวโค้ดคือ `stage-D-dry.ps1` ที่ export มาจากตัวขับ — ดูด้วย `Show-WjssStage -Name 'D-dry'`; ข้อความนี้ generate จากโค้ดจริงของตัวขับ จึงไม่เขียนค้าง):

- ต้องยืนยันกับ Owner ก่อน: พิมพ์ `DRY CYCLE AUTHORIZED` ตามที่ prompt ทวงถาม ( prompt: D: Stage B and C passed, the ACCELERATED package owner decisions are recorded, motion unloaded ) — ถ้าไม่ตอบตรงตัวบล็อกจะ**ไม่เรียก exe เลย**
- consent ที่บล็อกขอ: `PhysicalDoSingle` (อ่านแบบซ่อน, ลบทิ้งหลังจบบล็อก)
- exit code ที่ยอมรับ: `0` เท่านั้น
- env ที่บล็อกตั้ง: `WJSS_DRY_COMMAND_MODE=TEST_HARDWARE_DRY`, `WJSS_PUMP_SELECTION=1`, `WJSS_PUMP_FRESHNESS_MS=2000`, `WJSS_PUMP_RUNNING_CONFIRM_MS=10000`, `WJSS_PUMP_READY_TIMEOUT_MS=30000`, `WJSS_PUMP_STOPPED_CONFIRM_MS=15000`, `WJSS_PUMP_READY_SETPOINT_BAR=15`, `WJSS_PRESSURE_LOW_BAR=1`, `WJSS_PRESSURE_HIGH_BAR=15`, `WJSS_VALVE_SUPERVISION_MS=5000`
  และตั้งจากคำตอบของคุณบนหน้าจอ: `WJSS_CLEANING_SENSOR_ID`, `WJSS_PUMP_ANALOG_TARGET_MA`, `WJSS_PUMP_RUNNING_POLARITY`, `WJSS_PUMP_FAULT_POLARITY`
- key ที่**ต้องตรง**ตามนี้ (20 คู่): `profile=TEST_HARDWARE_DRY`, `mode=WJSS_DRY_COMMAND_MODE`, `prepare=DRY_PREPARE_READY`, `terminal_status=CompleteAwaitingSafeReturn`, `terminal_code=PATH_AND_VALVE_SEQUENCE_COMPLETE_AWAITING_SAFE_RETURN`, `primary_failure=NONE`, `cleanup.pump.stopped_confirmed=True`, `cleanup.pump.ownership_released=True`, `cleanup.pump.complete=True`, `cleanup.motion.complete=True`, `cleanup.valves.complete=True`, `cleanup.valves.refused=0`, `cleanup.valves.transport_failures=0`, `cleanup.valves.unknown=0`, `cleanup.complete=True`, `pump_ready_source=AI-003+`, `pump_feedback=COMMISSIONED`, `pump_gate=PUMP_PRECONDITION`, `write_authority=PROCESS_IO_SESSION`, `coordinators=COMPOSITION+EXECUTION`
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
| poller `READ FAILED - connect timed out` | IP/ firewall / ไม่ได้เสียบสาย | ตรวจ network แล้วรันบล็อกอ่านบิตซ้ำ เช่น `& "$script:Ev\stage-B-off.ps1"` (อ่านอย่างเดียว ปลอดภัย) |

## 11. สรุปว่า stage ไหนปิด gate อะไร (ใช้ตอนผมอัปเดต register)

| Stage | Gate ที่ปิด |
| --- | --- |
| A1 + A2 | REAL WAGO ENDPOINT CONNECTIVITY · Pump-ready strict-greater ทั้งสองฝั่งบนสัญญาณจริง |
| B | Pump running/fault polarity commissioned (absent ⇒ NOT_CONFIGURED ⇒ D ถูก refuse) |
| C | IV physical actuation + FC01 read-back |
| D | integrated dry cycle (no-load) ครบลูปพร้อม cleanup |
| E | WSB Upper/Lower reclass + DI read path (FC02 vs FC01) |
| F | (optional) motion stop behaviour |

**ส่งผมทุก stage:** ก้อนข้อความใต้ `--- copy everything below ---` (scrub แล้ว) + log ดิบเฉพาะก้อนที่มี `CHECK` — ห้าม commit ไฟล์ log/ผลลัพธ์เข้า repo เด็ดขาด
**สถานะ:** HARDWARE GATES ยัง OPEN · PRODUCTION NOT AUTHORIZED · MERGE HOLD บน PR #45 จนกว่าผมจะสรุปผลครบทุก stage และคุณจะสั่ง merge เอง
