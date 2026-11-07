# WJSS Physical Hardware Execution Runbook v2 — 2026-11-07

> เอกสารนี้แทนที่รุ่นแรก (รุ่นแรกเขียนงงและให้คนรันหาข้อมูลมากรอกเอง) — v2 ออกแบบให้ **copy → paste → รัน** เท่านั้น
> ผลลัพธ์ทุก stage มี script ตรวจให้เอง และก้อนข้อความสำหรับส่งให้ผมถูก scrub IP ให้อัตโนมัติ

## 0. ขั้นตอนที่ต้องรัน (copy ทั้งบล็อคไปวางได้เลย)

> บล็อค ``powershell`` ทุกบล็อกจาก §0 ถึง §8 เป็นคำสั่งจริงที่รันได้ทันที — ไม่ต้องแทนที่อะไรนอกจากค่าที่ผมไม่มี (IP, catalogue, ค่าที่มาจากผล Stage B)

### ข้อ 0.1 — ดึงโค้ด + build + รัน .NET matrix ทั้งหมด (ทำทุกครั้งที่ผมแจ้งว่า commit ใหม่แล้ว)

```powershell
$Repo = (Get-ChildItem -Path $HOME -Recurse -Depth 3 -Filter 'WaterJetSentinelSuite.sln' -ErrorAction SilentlyContinue | Select-Object -First 1).DirectoryName
if (-not $Repo) { $Repo = Read-Host 'Repo path (folder containing WaterJetSentinelSuite.sln)' }
Set-Location $Repo
$env:DOTNET_NOLOGO = '1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
git fetch origin
git rev-parse --short HEAD
# HEAD ต้องเป็น 214c0e0 หรือใหม่กว่า ถ้าไม่ตรง:
git pull --ff-only origin arena/b179a92e-waterjet-sentinel-suite
# ถ้า git pull บ่นว่าไม่ ff หรือมี local change -> หยุด แล้วส่งข้อความ error ให้ผม (ห้ามใช้ --force / reset)
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

วางบล็อค **§2 ทั้งบล็อค** (ใต้หัวข้อ §2 ในไฟล์นี้) แล้วตอบคำถาม 2 ข้อ: repo path + T8 IP — ตัวแปร `$script:Repo $script:Exe $script:Cat $script:Ip $script:Ev $script:Head` จะถูกตั้งครบ และ consent จะถูกถามเฉพาะ stage ที่ต้องใช้ (ซ่อนการพิมพ์, cache ในหน่วยความจำ,ถูกลบท้ายบล็อค)

ถ้าคุณใช้ catalogue สำเนาของคุณเอง (ตาม `docs/TRIAL_A_B_HARDWARE_EXECUTION_PACKAGE.md:148`) ให้วางต่อท้ายบล็อค §2:

```powershell
$script:Cat = 'C:\FULL\PATH\TO\YOUR\T8_CATALOGUE.csv'   # ค่าเริ่มต้น = docs\evidence\process-io-inventory-2026-10-22.csv ใน repo
```

### ข้อ 0.4 — รัน stage ตามลำดับ (ห้ามข้าม A1 ซึ่งคือ "ปั๊มหยุด")

```powershell
# ลำดับ: §3-A1 (ปั๊มหยุด) -> §3-A2 (ปั๊มรัน) -> §4-B (polarity) -> §5-C (IV1) -> §6-D (dry cycle) -> §7-E (WSB) -> [§8-F ถ้าถึง]
# แต่ละบล็อคพิมพ์บรรทัดสุดท้ายเป็น: RESULT <stage>: PASS  หรือ  RESULT <stage>: CHECK
```

### ข้อ 0.5 — สิ่งที่ต้องส่งกลับมาให้ผมหลังทุก stage

1. ก้อนที่อยู่ใต้ `--- copy everything below to the assistant ---` (script พิมพ์ให้ + เก็บในไฟล์ `$script:Ev\stage-<name>.send.txt` ซึ่ง scrub IP แล้ว)
2. ตัวเลข build/test จากข้อ 0.1
3. **เฉพาะ**เมื่อมี `CHECK`/`ERROR`: log ดิบทั้งไฟล์ `Get-Content $script:Ev\stage-A2.log -Raw` (ห้ามแก้ข้อความ ห้ามลบบรรทัด)

log ดิบทั้งหมดอยู่ใน `$env:USERPROFILE\wjss-evidence\<วันที่>\` — **ห้าม `git add` / ห้าม commit**

### ข้อ 0.6 — กติกาเวลาเจอ error

copy ข้อความ error ทั้งก้อน (บรรทัดแดง) ส่งผม แล้ว**ห้ามแก้ script เอง** และห้ามเปลี่ยนเกณฑ์ `PASS`/ค่าที่ script เทียบ — ผมจะแก้แล้ว commit ใหม่ให้เอง

**สถานะ scale ของ Pump pressure (แก้ที่ Product แล้ว):** ตัวแปลงตอนนี้คือ `bar = raw / 32767 × 40` (ช่อง AI‑003 ถูก config เป็น 4‑20 mA ⇒ `raw 0 = 4 mA` อยู่แล้ว) — ตัวอย่างจริงที่หน้างาน: `raw 580 = 4.283 mA = 0.708 bar` (ปั๊มหยุด, ปกติ), `raw 13631 = 10.656 mA = 16.6399 bar` (ปั๊มรัน) หลักฐานทั้งชุดอยู่ใน `docs/evidence/LEGACY_REV2_SCALING_CORRECTIONS_2026-11-07.md`

---

## 1. กฎความปลอดภัย (ห้ามข้าม)

- Stage **A, B, E = อ่านอย่างเดียว** ห้ามสั่งปั๊ม/วาล์วจาก Panel ระหว่างอ่าน ยกเว้นบรรทัดที่เขียนว่า "pump RUN"
- **C และ D** เป็นการสั่งงานจริง (actuation) — ห้ามรันถ้า **B ยังไม่ผ่าน** และ (สำหรับ D) ยังไม่ได้ตัดสินใจข้อ `docs/ACCELERATED_CLEANING_DRY_TRIAL_PACKAGE.md` §6/§7
- consent phrase พิมพ์จากค่าคงที่ใน `packages/contracts/ProcessIoContracts.cs` (`ProcessIoConsentPhrases.*`) เท่านั้น — ห้ามจดไว้ที่อื่น ห้ามถ่ายรูป และ script จะ `Remove-Item Env:` ท้ายทุกบล็อค
- IP ของ T8 ใช้ในหน่วยความจำของหน้าต่างนี้เท่านั้น — ไม่ถูกเขียนลงไฟล์ไหนใน repo และถูก scrub ออกจากก้อนที่ส่งผม
- ถ้าเกิดอะไรผิดปกติระหว่าง C/D: สั่งที่ Panel ตามลำดับ **Run (DO‑007) = FALSE ก่อน แล้ว Enable (DO‑003) = FALSE** (ห้ามกลับลำดับ) แล้วหยุดส่ง log

## 2. บล็อกร่วม — วางครั้งเดียวต่อบัญชี PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$script:Repo = (Resolve-Path (Read-Host 'Repo path (folder with WaterJetSentinelSuite.sln)')).Path
$script:Exe  = Join-Path $script:Repo 'apps\runtime\bin\Release\net10.0\Wjss.Runtime.exe'
$script:Cat  = Join-Path $script:Repo 'docs\evidence\process-io-inventory-2026-10-22.csv'
$script:Ip   = Read-Host 'T8 IP (used for this run only, never written to disk)'
$script:Ev   = Join-Path $env:USERPROFILE ('wjss-evidence\' + (Get-Date -Format yyyy-MM-dd))
New-Item -ItemType Directory -Force -Path $script:Ev | Out-Null
$script:Head = (& git -C $script:Repo rev-parse HEAD).Trim()
if (-not $script:Head) { throw 'STOP: not a git worktree.' }
$script:Consent = @{}

function Test-WjssBuild {
    if (-not (Test-Path -LiteralPath $script:Exe)) { throw 'STOP: Release exe missing. Run: dotnet build WaterJetSentinelSuite.sln -c Release' }
    $exe = Get-Item -LiteralPath $script:Exe
    $newest = Get-ChildItem -Path (Join-Path $script:Repo 'apps'), (Join-Path $script:Repo 'packages') -Recurse -Include *.cs -ErrorAction SilentlyContinue |
              Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($newest -and $newest.LastWriteTime -gt $exe.LastWriteTime) {
        throw ('STOP: source file {0} is newer than the exe. Rebuild Release, then re-paste this stage.' -f $newest.Name)
    }
    ('HEAD {0} | exe sha256 {1} | built {2}' -f $script:Head.Substring(0,8), (Get-FileHash -LiteralPath $script:Exe -Algorithm SHA256).Hash.Substring(0,12), $exe.LastWriteTime.ToString('yyyy-MM-dd HH:mm'))
}

function Read-WjssConsent {
    param([string]$Const)
    if (-not $script:Consent.ContainsKey($Const)) {
        Write-Host ('  copy the value of ProcessIoConsentPhrases.{0} from packages\contracts\ProcessIoContracts.cs' -f $Const) -ForegroundColor DarkGray
        $sec = Read-Host ('  paste consent for {0} (typing is hidden)' -f $Const) -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
        try { $script:Consent[$Const] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    }
    return $script:Consent[$Const]
}

function Save-WjssSend {
    param([string]$Name, $Lines)
    $txt = @('STAGE {0} | HEAD={1}' -f $Name, $script:Head.Substring(0,8)) + @($Lines)
    $txt = $txt -replace '\b\d{1,3}(\.\d{1,3}){3}\b', '<IP>'
    $p = Join-Path $script:Ev ('stage-' + $Name + '.send.txt')
    Set-Content -LiteralPath $p -Value ($txt -join [Environment]::NewLine) -Encoding UTF8
    Write-Host '--- copy everything below to the assistant (IP already scrubbed) ---' -ForegroundColor Cyan
    $txt
    Write-Host ('--- saved: {0} ---' -f $p) -ForegroundColor DarkGray
}

function Invoke-WjssStage {
    param([string]$Name, [hashtable]$Vars = @{}, [string]$ConsentConst = '', [IDictionary]$Expect = @{}, [int]$ExpectExit = 0, [string[]]$Show = @())
    Write-WjssClearEnv
    $build = Test-WjssBuild
    Write-Host ('  build check: ' + $build) -ForegroundColor DarkGray
    $log = Join-Path $script:Ev ('stage-' + $Name + '.log')
    $env:WJSS_DEVICE_PROFILE       = 'TEST_HARDWARE'
    $env:WJSS_PROCESS_IO_HOST      = $script:Ip
    $env:WJSS_PROCESS_IO_CATALOGUE = $script:Cat
    foreach ($k in $Vars.Keys) { Set-Item -Path ('env:' + $k) -Value ([string]$Vars[$k]) }
    if ($ConsentConst) { $env:WJSS_PROCESS_IO_CONSENT = Read-WjssConsent -Const $ConsentConst }
    Push-Location $script:Repo
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'   # native stderr must not abort us; the exit code decides
    try {
        & $script:Exe 2>&1 | Tee-Object -FilePath $log | Out-Null
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $prevEap
        Remove-Item -LiteralPath 'env:WJSS_PROCESS_IO_CONSENT' -ErrorAction SilentlyContinue
        Pop-Location
    }
    $map = [ordered]@{}
    foreach ($line in (Get-Content -LiteralPath $log)) {
        $t = $line.Trim(); $i = $t.IndexOf('=')
        if ($i -gt 0) { $k = $t.Substring(0, $i).Trim(); if (-not $map.Contains($k)) { $map[$k] = $t.Substring($i + 1).Trim() } }
    }
    $fail = @()
    if ($ExpectExit -ge 0 -and $code -ne $ExpectExit) { $fail += ('exit={0} (expected {1})' -f $code, $ExpectExit) }
    foreach ($k in $Expect.Keys) {
        $want = [string]$Expect[$k]
        if (-not $map.Contains($k)) { $fail += ($k + ' missing from report') }
        elseif ($want.StartsWith('~')) { if (-not $map[$k].EndsWith($want.Substring(1))) { $fail += ('{0}={1} (expected ...{2})' -f $k, $map[$k], $want.Substring(1)) } }
        elseif ($map[$k] -ne $want) { $fail += ('{0}={1} (expected {2})' -f $k, $map[$k], $want) }
    }
    $keys = (@($Expect.Keys) + @($Show) | Select-Object -Unique | Where-Object { $map.Contains($_) })
    $out = @('  exit={0}' -f $code)
    foreach ($k in $keys) { $out += ('  {0}={1}' -f $k, $map[$k]) }
    if ($fail.Count) { Write-Host ('RESULT {0}: CHECK' -f $Name) -ForegroundColor Yellow; foreach ($f in $fail) { Write-Host ('   - ' + $f) -ForegroundColor Yellow } }
    else { Write-Host ('RESULT {0}: PASS' -f $Name) -ForegroundColor Green }
    Save-WjssSend -Name $Name -Lines $out
    Write-Host ('raw log (keep local, never commit): {0}' -f $log) -ForegroundColor DarkGray
}

function Write-WjssClearEnv {
    Get-ChildItem Env: | Where-Object { $_.Name -like 'WJSS_*' } | ForEach-Object { Remove-Item -LiteralPath ('env:' + $_.Name) -ErrorAction SilentlyContinue }
}

# read-only Modbus TCP poller (FC02 = discrete inputs, FC01 = coils). FC05/06/15/16 are not implemented at all.
function Read-WjssBits {
    param([string]$Area = 'DI', [int]$Start = 0, [int]$Count = 16)
    $fc = 2
    if ($Area -eq 'COIL') { $fc = 1 }
    $tcp = New-Object System.Net.Sockets.TcpClient
    try {
        if (-not $tcp.ConnectAsync($script:Ip, 502).Wait(3000)) { throw 'connect timed out (check IP / firewall / device power)' }
        $s = $tcp.GetStream()
        $req = New-Object byte[] 12
        $req[0] = 0; $req[1] = 1; $req[2] = 0; $req[3] = 0; $req[4] = 0; $req[5] = 6
        $req[6] = 1; $req[7] = $fc
        $req[8] = [int][math]::Floor($Start / 256); $req[9] = ($Start % 256)
        $req[10] = [int][math]::Floor($Count / 256); $req[11] = ($Count % 256)
        $s.Write($req, 0, 12); $s.Flush()
        $hdr = New-Object byte[] 9
        $o = 0
        while ($o -lt 9) { $r = $s.Read($hdr, $o, 9 - $o); if ($r -le 0) { throw 'connection closed by device' }; $o += $r }
        if ([int]$hdr[7] -ne $fc) { throw ('device returned Modbus exception for FC{0:00} (check that this area is exposed on the device)' -f $fc) }
        $n = [int]$hdr[8]
        $dat = New-Object byte[] $n
        $o = 0
        while ($o -lt $n) { $r = $s.Read($dat, $o, $n - $o); if ($r -le 0) { throw 'connection closed mid-response' }; $o += $r }
        $bits = @()
        for ($i = 0; $i -lt $Count; $i++) { $byte = [int]$dat[[int][math]::Floor($i / 8)]; $bits += (($byte -shr ($i % 8)) -band 1) }
        return @{ ok = $true; fc = $fc; start = $Start; bits = $bits }
    }
    catch { return @{ ok = $false; error = $_.Exception.Message; bits = @() } }
    finally { $tcp.Close() }
}

function Format-WjssBits {
    param([string]$Prefix, [int]$Base, $Result)
    if (-not $Result.ok) { return ('  {0}: READ FAILED - {1}' -f $Prefix, $Result.error) }
    $line = '  ' + $Prefix + ':'
    for ($i = 0; $i -lt $Result.bits.Count; $i++) { $line += (' {0}-{1:D3}={2}' -f $Prefix, ($Base + $i), $Result.bits[$i]) }
    return $line
}
```

รัน `Test-WjssBuild` หนึ่งครั้งเพื่อดู HEAD/exe ที่ใช้ (พิมพ์ผลลัพธ์เก็บไว้ ผมใช้เทียบว่ารันบน build ไหน)

## 3. Stage A — Trial A: อ่าน Pump-ready บนสัญญาณจริง (อ่านอย่างเดียว)

ปิด gate: `REAL WAGO ENDPOINT CONNECTIVITY` + `Pump-ready rule บนสัญญาณจริง (strict >)` ทั้งสองฝั่ง

**A1 — ปั๊มหยุด** (คาดว่า **exit 2** = typed refusal เพราะ 0.7 bar ≤ 15 bar →นี่คือ PASS)

```powershell
Invoke-WjssStage -Name 'A1-off' -ConsentConst 'PhysicalDiAiRead' -ExpectExit 2 -Vars @{
  'WJSS_PUMP_READY_READ_TRIAL'   = 'AI-003_ONLY'
  'WJSS_PUMP_READY_SETPOINT_BAR' = '15'
  'WJSS_PUMP_READY_FRESHNESS_MS' = '2000'
} -Expect ([ordered]@{
  'trial_code'                = 'PUMP_READY_REFUSED_AT_OR_BELOW_SETPOINT'
  'source_tag'                = 'AI-003+'
  'evidence_kind'             = 'PHYSICAL'
  'provenance'                = 'PHYSICAL_READ'
  'quality'                   = 'GOOD'
  'fresh'                     = 'TRUE'
  'physical_ai003_verified'   = 'TRUE'
  'pump_ready'                = 'FALSE'
  'strict_comparison'         = '~=> FALSE'
  'session_state'             = 'CLOSED'
  'transport_released'        = 'TRUE'
  'ai002_used_for_decision'   = 'FALSE'
  'iv_command_executed'       = 'FALSE'
  'motion_executed'           = 'FALSE'
  'write_authorization_created' = 'FALSE'
}) -Show @('raw_value','engineering_bar','configured_setpoint_bar','session_generation')
```

**A2 — ปั๊ม RUN จาก Panel (Owner เท่านั้น):** รอแรงดันนิ่งก่อน แล้วรันซ้ำ — คาด **exit 0** + `pump_ready=TRUE`

```powershell
Invoke-WjssStage -Name 'A2-run' -ConsentConst 'PhysicalDiAiRead' -ExpectExit 0 -Vars @{
  'WJSS_PUMP_READY_READ_TRIAL'   = 'AI-003_ONLY'
  'WJSS_PUMP_READY_SETPOINT_BAR' = '15'
  'WJSS_PUMP_READY_FRESHNESS_MS' = '2000'
} -Expect ([ordered]@{
  'trial_code'              = 'PUMP_READY_ACCEPTED_PHYSICAL'
  'evidence_kind'           = 'PHYSICAL'
  'physical_ai003_verified' = 'TRUE'
  'pump_ready'              = 'TRUE'
  'physical_pump_verified'  = 'TRUE'
  'strict_comparison'       = '~=> TRUE'
  'session_state'           = 'CLOSED'
  'transport_released'      = 'TRUE'
  'iv_command_executed'     = 'FALSE'
  'motion_executed'         = 'FALSE'
  'write_authorization_created' = 'FALSE'
}) -Show @('raw_value','engineering_bar','configured_setpoint_bar','quality','fresh')
```

- **A2 ได้ exit 2 + `engineering_bar` ≤ 15** → ปกติ (ปั๊มยังไม่ขึ้นแรงดัน) รอแล้วรันซ้ำได้
- **A2 ได้ exit 2 แต่ `engineering_bar` > 15** → นี่แหละผิดปกติจริง หยุดและส่ง log ให้ผม
- **A1 ได้ exit 0** → ผิดปกติ (gate ต้อง refuse ใต้ setpoint) หยุดและส่ง log
- exit ladder: `0` = Accepted เท่านั้น · `2` = refusal หลัง bind session · `3` = ReadTimeout · `4` = startup refusal (env/consent/profile)

## 4. Stage B — ขั้วสัญญาณ (polarity) DI‑004 / DI‑005 (อ่านอย่างเดียว)

ปิด gate: `WJSS_PUMP_RUNNING_POLARITY` / `WJSS_PUMP_FAULT_POLARITY` (ถ้ายังไม่commission ⇒ Stage D จะถูก refuse — นั่นคือ fail-closed ทำงาน)
ไม่มี tool ใดใน Product ที่พิมพ์สถานะ DI แบบต่อบิต — ใช้ poller ใน §2 (อ่านอย่างเดียว, เขียนไม่ได้)

```powershell
$t = @()
$t += Format-WjssBits -Prefix 'DI' -Base 1 -Result (Read-WjssBits -Area 'DI' -Start 0 -Count 8)   # DI-001..DI-008
Save-WjssSend -Name 'B-off' -Lines $t
```

สั่ง **ปั๊ม RUN** จาก Panel รอให้นิ่ง แล้วรันบล็อคเดิมซ้ำ (เปลี่ยนชื่อเป็น `B-on`):

```powershell
$t = @()
$t += Format-WjssBits -Prefix 'DI' -Base 1 -Result (Read-WjssBits -Area 'DI' -Start 0 -Count 8)
Save-WjssSend -Name 'B-on' -Lines $t
```

ตัดสินจากสองก้อนที่ส่งผม (คุณไม่ต้องแปลเอง ผมแปลให้): **DI‑004** ต้องเปลี่ยนระดับเมื่อ RUN, **DI‑005** ต้องอยู่นิ่งฝั่ง inactive
ค่าที่จะใช้ใน Stage D = `ACTIVE_HIGH` ถ้าระดับ "ทำงาน" คือ 1, `ACTIVE_LOW` ถ้าคือ 0

## 5. Stage C — Trial B: IV1 OPEN/CLOSE ครั้งเดียว (actuation ครั้งแรก)

ปิด gate: `physical IV command + FC01 read-back` · ต้องมี B ผ่านแล้ว และ **ปั๊มต้องหยุด** (trial ต้องการ attestation นี้)

```powershell
Invoke-WjssStage -Name 'C-iv1' -ConsentConst 'PhysicalDoSingle' -ExpectExit 0 -Vars @{
  'WJSS_DRY_IV_TRIAL_MODE'               = 'SOFTWARE_SIMULATED_AI003'
  'WJSS_DRY_IV_TRIAL_VALVE'              = 'IV1'
  'WJSS_DRY_IV_TRIAL_PUMP_STOPPED'       = 'PUMP_STOPPED_OWNER_ATTESTED'
  'WJSS_DRY_IV_TRIAL_SIMULATED_AI003_BAR' = '0.7'
  'WJSS_VALVE_SUPERVISION_MS'            = '5000'
} -Show @('trial_code','accepted','evidence_kind','physical_iv_command','physical_upper_feedback','physical_lower_feedback','cleanup','session_state')
```

- stage นี้ **light โดยเจตนา** (ค่า terminal ขึ้นกับ feedback จริงหน้างาน) — ตัวเลขทั้งหมดในก้อน send ผมวิเคราะห์เอง
- ถ้า `exit=4` = startup refused (env/consent ไม่ครบหรือชนกัน) · `exit=2` = refuse หลัง bind เช่น `IV_TRIAL_REFUSED_*_NOT_FIELD_CONFIRMED` / `CONTRADICTORY` → **นี่คือ fail-closed ทำงาน ไม่ใช่บั๊ก** หยุด stage แล้วส่ง log

## 6. Stage D — Integrated cleaning-path DRY cycle (actuation เต็มรูป)

ปิด gate: วงจร dry ทั้งรอบ (select pump → enable → run → AO ramp → supervision → cleanup → release)
**เงื่อนไขก่อนรัน:** B ผ่าน (รู้ขั้วสัญญาณแล้ว) + C ผ่าน + เจ้าของตัดสินใจ `docs/ACCELERATED_CLEANING_DRY_TRIAL_PACKAGE.md` §6/§7 แล้ว + Motion ยังไม่มีแรง (no-load)

```powershell
Invoke-WjssStage -Name 'D-dry' -ConsentConst 'PhysicalDoSingle' -ExpectExit 0 -Vars @{
  'WJSS_DRY_COMMAND_MODE'          = 'TEST_HARDWARE_DRY'
  'WJSS_CLEANING_SENSOR_ID'        = '<Sensor Id จากตาราง §6 ของ ACCELERATED package>'
  'WJSS_PUMP_SELECTION'            = '1'
  'WJSS_PUMP_ANALOG_TARGET_MA'     = '<ค่า mA ที่เลือกไว้ §6>'
  'WJSS_PUMP_FRESHNESS_MS'         = '2000'
  'WJSS_PUMP_RUNNING_POLARITY'     = '<ACTIVE_HIGH หรือ ACTIVE_LOW จาก Stage B>'
  'WJSS_PUMP_FAULT_POLARITY'       = '<ACTIVE_HIGH หรือ ACTIVE_LOW จาก Stage B>'
  'WJSS_PUMP_RUNNING_CONFIRM_MS'   = '10000'
  'WJSS_PUMP_READY_TIMEOUT_MS'     = '30000'
  'WJSS_PUMP_STOPPED_CONFIRM_MS'   = '15000'
  'WJSS_PUMP_READY_SETPOINT_BAR'   = '15'
  'WJSS_PRESSURE_LOW_BAR'          = '1'
  'WJSS_PRESSURE_HIGH_BAR'         = '15'
  'WJSS_VALVE_SUPERVISION_MS'      = '5000'
} -Show @('profile','mode','terminal_status','terminal_code','primary_failure','steps','pump_readiness','session_state','session_generation','write_authority')
```

- 4 ค่าใน `<...>` คือค่าที่ **ผมห้ามเดาแทน** (sensor id, mA target, polarity สองตัว) — ขาดไม่ได้: script จะส่ง `exit=4` พร้อม refusal ที่บอกชื่อตัวแปรที่ขาด
- **ห้าม re-run เพื่อหวังผล luck**: ถ้า `steps` หมด budget แล้วไม่มี terminal state ให้ส่ง log ทั้งก้อน
- cleanup ของ trial = ramp ลง → DO‑007 FALSE → ตรวจ → DO‑003 FALSE → ตรวจ → รอ not-running → ปล่อย ownership (script ตรวจ `cleanup=` จาก log ให้ผมดูอีกชั้น)

## 7. Stage E — WSB limit switches (Upper/Lower) + FC02/FC01 divergence probe

ปิด gate: `DI mirroring / limit-switch reclass` ตาม corrected CSV (DI‑021..028 = Upper, DI‑029..036 = Lower)
Legacy rev2 อ่าน bit กลุ่มนี้ผ่าน **FC01 (coils)** ส่วน Product อ่านผ่าน **FC02 (discrete inputs)** ที่ address เดียวกัน — บล็อคนี้วัดทั้งสองทางให้เห็นจริง (อ่านอย่างเดียว)

```powershell
$t = @()
$t += Format-WjssBits -Prefix 'DI' -Base 21 -Result (Read-WjssBits -Area 'DI'   -Start 20 -Count 16)   # FC02, addr 20..35
$t += Format-WjssBits -Prefix 'CO' -Base 21 -Result (Read-WjssBits -Area 'COIL' -Start 20 -Count 16)   # FC01, same addresses
$t += '  all-switches-at-rest-should-be-one-side (ตาม wiring จริง)'
Save-WjssSend -Name 'E-idle' -Lines $t
```

จากนั้นให้ Owner ขยับ WSB ทีละตัว (หรือให้ปั๊ม/วาล์วทำงานในจังหวะที่มี limit switch เปลี่ยน) แล้วรันซ้ำ:

```powershell
$t = @()
$t += Format-WjssBits -Prefix 'DI' -Base 21 -Result (Read-WjssBits -Area 'DI'   -Start 20 -Count 16)
$t += Format-WjssBits -Prefix 'CO' -Base 21 -Result (Read-WjssBits -Area 'COIL' -Start 20 -Count 16)
Save-WjssSend -Name 'E-moved' -Lines $t
```

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
| poller `READ FAILED - connect timed out` | IP/ firewall / ไม่ได้เสียบสาย | ตรวจ network แล้วรันบล็อค poller ซ้ำ (อ่านอย่างเดียว ปลอดภัย) |

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
