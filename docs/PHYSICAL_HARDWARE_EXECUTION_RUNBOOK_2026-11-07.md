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

วางบล็อค **§2 ทั้งบล็อค** (ใต้หัวข้อ §2 ในไฟล์นี้) แล้วตอบคำถาม 2 ข้อ: repo path + T8 IP — ตัวแปร `$script:Repo $script:Exe $script:Cat $script:Ip $script:Ev $script:Head` จะถูกตั้งครบ และ consent จะถูกถามเฉพาะ stage ที่ต้องใช้ (ซ่อนการพิมพ์, cache ในหน่วยความจำ,ถูกลบท้ายบล็อค)

ทางลัด — ไม่ต้อง copy บล็อค §2 เอง (โหลดจากไฟล์นี้แล้ว dot-source เข้าหน้าต่างปัจจุบัน):

```powershell
$b  = [char]0x60 * 3
$md = Get-Content "$Repo\docs\PHYSICAL_HARDWARE_EXECUTION_RUNBOOK_2026-11-07.md" -Raw
$m  = [regex]::Match($md, '(?m)^## 2\. ')
if (-not $m.Success) { throw 'STOP: no section 2 heading in that file.' }
$code = [regex]::Match($md.Substring($m.Index), ('(?s)' + $b + 'powershell' + [char]13 + '?' + [char]10 + '(.*?)' + $b)).Groups[1].Value
if ($code.Length -lt 200) { throw 'STOP: the shared block was not found. Open the runbook and paste the block under heading 2 manually.' }
. ([scriptblock]::Create($code))
Test-WjssBuild
Export-WjssStageFiles
```

หมายเหตุ: ตัวแปร `$Repo` มาจากข้อ 0.1 (รันในหน้าต่างเดียวกัน) — ถ้าเปิดหน้าต่างใหม่ให้วางสามบรรทัดแรกของข้อ 0.1 ก่อน

ค่าควบคุมที่มีประโยชน์ (ใส่ต่อท้ายบล็อค §2 ได้เลย):

```powershell
$script:Port = 502       # Modbus TCP port used by the read-only poller AND given to the trial
$script:UnitId = 1       # unit id, same for both
# if a stage must run on an exe you built outside this repo: Invoke-WjssStage ... -SkipBuildCheck
```

ถ้าคุณใช้ catalogue สำเนาของคุณเอง (ตาม `docs/TRIAL_A_B_HARDWARE_EXECUTION_PACKAGE.md:148`) ให้วางต่อท้ายบล็อค §2:

```powershell
$script:Cat = 'C:\FULL\PATH\TO\YOUR\T8_CATALOGUE.csv'   # default is docs\evidence\process-io-inventory-2026-10-22.csv in the repo
```

### ข้อ 0.4 — รัน stage ตามลำดับ (ห้ามข้าม A1 ซึ่งคือ "ปั๊มหยุด")

รันตามลำดับนี้ **ทีละบรรทัด** (ไฟล์พวกนี้เกิดจาก `Export-WjssStageFiles` ใน §2) — A1 รันตอนปั๊มหยุด, A2 รันตอนปั๊มรัน, B off/on เช่นกัน, ห้ามรันคู่ที่ติดกันแล้วลืมเปลี่ยนสถานะปั๊ม:

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

function Read-WjssLine {
    # asks once, retries up to three times, never invents a value
    param([string]$Prompt, [string]$Kind = 'text')
    for ($i = 0; $i -lt 3; $i++) {
        $v = (Read-Host $Prompt).Trim()
        $v = $v.Trim([char]34).Trim([char]39)
        if ($v.Length -eq 0) { Write-Host '  empty answer - type the value' -ForegroundColor Yellow; continue }
        if ($Kind -eq 'repo') {
            if (-not (Test-Path -LiteralPath (Join-Path $v 'WaterJetSentinelSuite.sln'))) {
                Write-Host '  no WaterJetSentinelSuite.sln in that folder - type the repo root' -ForegroundColor Yellow; continue
            }
            return (Resolve-Path -LiteralPath $v).Path
        }
        if ($Kind -eq 'ip') {
            if ($v -notmatch '^[0-9]{1,3}(\.[0-9]{1,3}){3}$') {
                Write-Host '  not an IPv4 address - four dotted numbers, the value on the T8 label' -ForegroundColor Yellow; continue
            }
            return $v
        }
        return $v
    }
    throw 'STOP: no valid answer after 3 tries. Re-paste this block.'
}

$script:Repo = Read-WjssLine -Kind 'repo' -Prompt 'Repo path (folder that contains WaterJetSentinelSuite.sln)'
$script:Ip   = Read-WjssLine -Kind 'ip'   -Prompt 'T8 Modbus TCP IP (this window only - never written to any file)'
$script:Exe  = Join-Path $script:Repo 'apps\runtime\bin\Release\net10.0\Wjss.Runtime.exe'
$script:Cat  = Join-Path $script:Repo 'docs\evidence\process-io-inventory-2026-10-22.csv'
$script:Ev   = Join-Path $env:USERPROFILE ('wjss-evidence\' + (Get-Date -Format 'yyyy-MM-dd'))
$script:Port    = 502      # same default as ProcessIoTransportShape.Port in the Product
$script:UnitId  = 1        # same default as ProcessIoTransportShape.UnitId
New-Item -ItemType Directory -Force -Path $script:Ev | Out-Null
$script:Head = [string](& git -C $script:Repo rev-parse HEAD)
if ($script:Head.Length -lt 40) { throw 'STOP: git did not return a HEAD sha in that folder.' }
$script:Head = $script:Head.Trim()
$script:Consent = @{}

function Test-WjssBuild {
    # refuses to test a stale binary: this is what protects the corrected scaling from being judged on an old exe
    if (-not (Test-Path -LiteralPath $script:Exe)) {
        throw 'STOP: Wjss.Runtime.exe not found. Run: dotnet build WaterJetSentinelSuite.sln -c Release'
    }
    $exe = Get-Item -LiteralPath $script:Exe
    $newest = $null
    $roots = @((Join-Path $script:Repo 'apps'), (Join-Path $script:Repo 'packages'))
    foreach ($r in $roots) {
        $files = @(Get-ChildItem -Path $r -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
        foreach ($f in $files) {
            if ($null -eq $newest) { $newest = $f }
            elseif ($f.LastWriteTime -gt $newest.LastWriteTime) { $newest = $f }
        }
    }
    if ($null -ne $newest -and $newest.LastWriteTime -gt $exe.LastWriteTime) {
        throw ('STOP: ' + $newest.Name + ' is newer than the exe. Rebuild Release, then run this stage again (or add -SkipBuildCheck to this stage only if you know why).')
    }
    $hash = 'unavailable'
    try { $hash = (Get-FileHash -LiteralPath $script:Exe -Algorithm SHA256).Hash.Substring(0, 12) } catch { }
    return ('HEAD ' + $script:Head.Substring(0, 8) + ' | exe sha256 ' + $hash + ' | built ' + $exe.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))
}

function Read-WjssConsent {
    param([string]$Const)
    if (-not $script:Consent.ContainsKey($Const)) {
        Write-Host ('  read the value of ProcessIoConsentPhrases.' + $Const + ' from packages\contracts\ProcessIoContracts.cs, then paste it below (hidden)') -ForegroundColor DarkGray
        $sec = Read-Host '  consent phrase' -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
        try { $script:Consent[$Const] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    }
    return $script:Consent[$Const]
}

function Write-WjssClearEnv {
    $names = @([Environment]::GetEnvironmentVariables().Keys)
    foreach ($n in $names) {
        if ($n -like 'WJSS_*') { [Environment]::SetEnvironmentVariable($n, $null) }
    }
}

function Save-WjssSend {
    param([string]$Name, $Lines)
    $txt = @()
    $txt += 'STAGE ' + $Name + ' | HEAD=' + $script:Head.Substring(0, 8) + ' | ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    foreach ($l in @($Lines)) { $txt += [string]$l }
    $scrubbed = @()
    foreach ($l in $txt) { $scrubbed += ($l -replace '\b[0-9]{1,3}(\.[0-9]{1,3}){3}\b', '<IP>') }
    $p = Join-Path $script:Ev ('stage-' + $Name + '.send.txt')
    Set-Content -LiteralPath $p -Value ($scrubbed -join [Environment]::NewLine) -Encoding utf8
    Write-Host '--- copy everything below to the assistant (IP already scrubbed) ---' -ForegroundColor Cyan
    foreach ($l in $scrubbed) { Write-Host $l }
    Write-Host ('--- saved: ' + $p + ' ---') -ForegroundColor DarkGray
}

function Invoke-WjssStage {
    param(
        [string]$Name,
        [hashtable]$Vars = @{},
        [string]$ConsentConst = '',
        $Expect = $null,
        $Show = @(),
        [int]$ExpectExit = 0,
        [switch]$SkipBuildCheck
    )
    Write-WjssClearEnv
    if ($SkipBuildCheck) { Write-Host '  build check SKIPPED by request' -ForegroundColor Yellow }
    else { Write-Host ('  build: ' + (Test-WjssBuild)) -ForegroundColor DarkGray }

    $outFile = Join-Path $script:Ev ('stage-' + $Name + '.out.txt')
    $errFile = Join-Path $script:Ev ('stage-' + $Name + '.err.txt')
    if (-not (Test-Path -LiteralPath $script:Exe)) { throw ('STOP: exe not found: ' + $script:Exe) }

    [Environment]::SetEnvironmentVariable('WJSS_DEVICE_PROFILE', 'TEST_HARDWARE')
    [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_HOST', $script:Ip)
    [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_CATALOGUE', $script:Cat)
    # the poller and the Product must address the same endpoint, so the stage inherits both values
    [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_PORT', [string]$script:Port)
    [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_UNIT_ID', [string]$script:UnitId)
    foreach ($k in @($Vars.Keys)) { [Environment]::SetEnvironmentVariable($k, [string]$Vars[$k]) }
    if ($ConsentConst.Length -gt 0) {
        [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_CONSENT', (Read-WjssConsent -Const $ConsentConst))
    }

    $code = $null
    try {
        $proc = Start-Process -FilePath $script:Exe -WorkingDirectory $script:Repo -NoNewWindow -Wait -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile
        if ($null -ne $proc) {
            if (-not $proc.HasExited) { $proc.WaitForExit() }
            try { $code = [int]$proc.ExitCode } catch { $code = $null }
            try { $proc.Dispose() } catch { }
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_CONSENT', $null)
    }

    $map = @{}
    if (Test-Path -LiteralPath $outFile) {
        foreach ($line in @(Get-Content -LiteralPath $outFile)) {
            $t = [string]$line
            $i = $t.IndexOf('=')
            if ($i -gt 0) {
                $k = $t.Substring(0, $i).Trim()
                if ($k.Length -gt 0 -and -not $map.ContainsKey($k)) { $map[$k] = $t.Substring($i + 1).Trim() }
            }
        }
    }
    $errLines = @()
    if (Test-Path -LiteralPath $errFile) { $errLines = @(Get-Content -LiteralPath $errFile | Where-Object { $_.Length -gt 0 }) }

    $fail = @()
    if ($null -eq $code) { $fail += 'exit code could not be read from the process object' }
    elseif ($code -ne $ExpectExit) { $fail += ('exit=' + [string]$code + ' but this stage expects ' + [string]$ExpectExit) }
    if ($null -ne $Expect) {
        foreach ($k in @($Expect.Keys)) {
            $exp = [string]$Expect[$k]
            if (-not $map.ContainsKey($k)) { $fail += ('key ' + $k + ' was not printed by the trial (expected ' + $exp + ')'); continue }
            $got = [string]$map[$k]
            if ($exp.Length -gt 1 -and $exp.Substring(0, 1) -eq '~') {
                if (-not $got.EndsWith($exp.Substring(1))) { $fail += ($k + '=' + $got + ' does not end with ' + $exp.Substring(1)) }
            }
            elseif ($got -ne $exp) { $fail += ($k + '=' + $got + ' but expected ' + $exp) }
        }
    }

    $rep = @()
    $rep += '  exit=' + [string]$code
    foreach ($k in @($Show)) {
        if ($map.ContainsKey($k)) { $rep += ('  ' + $k + '=' + [string]$map[$k]) }
        else { $rep += ('  ' + $k + '=<not printed>') }
    }
    if ($errLines.Count -gt 0) {
        $rep += ('  stderr_lines=' + [string]$errLines.Count)
        $take = 5
        if ($errLines.Count -lt $take) { $take = $errLines.Count }
        for ($j = 0; $j -lt $take; $j++) { $rep += ('  stderr| ' + [string]$errLines[$j]) }
    }
    if ($fail.Count -gt 0) {
        $rep += '  --- last 40 output lines (auto-included because this stage did not match) ---'
        $all = @(Get-Content -LiteralPath $outFile)
        $from = $all.Count - 40
        if ($from -lt 0) { $from = 0 }
        for ($j = $from; $j -lt $all.Count; $j++) { $rep += ('  | ' + [string]$all[$j]) }
    }
    $rep += '  raw stdout: ' + $outFile

    if ($fail.Count -eq 0) {
        Write-Host ('RESULT ' + $Name + ': PASS (exit=' + [string]$code + ')') -ForegroundColor Green
        Save-WjssSend -Name $Name -Lines $rep
    }
    else {
        Write-Host ('RESULT ' + $Name + ': CHECK') -ForegroundColor Red
        foreach ($m in $fail) { Write-Host ('   MISMATCH ' + $m) -ForegroundColor Red }
        $withFails = @()
        foreach ($m in $fail) { $withFails += ('  MISMATCH ' + $m) }
        Save-WjssSend -Name $Name -Lines ($withFails + $rep)
    }
}

function Read-WjssExact {
    param($Stream, [byte[]]$Buffer, [int]$Offset, [int]$Count)
    $got = 0
    while ($got -lt $Count) {
        $n = $Stream.Read($Buffer, ($Offset + $got), ($Count - $got))
        if ($n -le 0) { throw 'the device closed the connection mid-response' }
        $got = $got + $n
    }
}

function Read-WjssBits {
    # READ ONLY Modbus TCP poller: FC02 discrete inputs, FC01 coils. Nothing here can write to the device.
    param([string]$Area = 'DI', [int]$Start = 0, [int]$Count = 16)
    $fc = 2
    if ($Area -eq 'COIL') { $fc = 1 }
    $client = $null
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $client.ReceiveTimeout = 4000
        $client.SendTimeout = 4000
        $task = $client.ConnectAsync($script:Ip, $script:Port)
        if (-not $task.Wait(4000)) { return @{ ok = $false; error = 'connect timed out after 4000 ms'; bits = @() } }
        $stream = $client.GetStream()

        $req = New-Object byte[] 12
        $req[0] = 0; $req[1] = 1; $req[2] = 0; $req[3] = 1; $req[4] = 0; $req[5] = 6
        $req[6] = [byte]$script:UnitId
        $req[7] = [byte]$fc
        $req[8] = [byte](($Start -shr 8) -band 255); $req[9] = [byte]($Start -band 255)
        $req[10] = [byte](($Count -shr 8) -band 255); $req[11] = [byte]($Count -band 255)
        $stream.Write($req, 0, 12)
        $stream.Flush()

        $hdr = New-Object byte[] 9
        Read-WjssExact -Stream $stream -Buffer $hdr -Offset 0 -Count 9 | Out-Null
        if ([int]$hdr[7] -ne $fc) {
            $ex = New-Object byte[] 1
            Read-WjssExact -Stream $stream -Buffer $ex -Offset 0 -Count 1 | Out-Null
            return @{ ok = $false; error = ('device returned Modbus exception ' + [string]([int]$ex[0]) + ' for FC' + [string]$fc + ' (that area may not be exposed on this device)'); bits = @() }
        }
        $byteCount = [int]$hdr[8]
        $data = New-Object byte[] $byteCount
        if ($byteCount -gt 0) { Read-WjssExact -Stream $stream -Buffer $data -Offset 0 -Count $byteCount | Out-Null }
        $bits = @()
        for ($i = 0; $i -lt $Count; $i++) {
            $b = [int]$data[($i -shr 3)]
            $bits += (($b -shr ($i -band 7)) -band 1)
        }
        return @{ ok = $true; fc = $fc; start = $Start; bits = $bits }
    }
    catch {
        return @{ ok = $false; error = $_.Exception.Message; bits = @() }
    }
    finally {
        if ($null -ne $client) { $client.Close() }
    }
}

function Format-WjssBits {
    param([string]$Prefix = 'DI', [int]$Base = 1, $Result)
    if ($null -eq $Result.ok -or -not $Result.ok) {
        return ('  ' + $Prefix + ': READ FAILED - ' + [string]$Result.error)
    }
    $parts = @()
    for ($i = 0; $i -lt $Result.bits.Count; $i++) {
        $parts += ($Prefix + ('-{0:D3}=' -f ($Base + $i)) + [string]$Result.bits[$i])
    }
    return ('  ' + ($parts -join ' '))
}

function Read-WjssField {
    # one Owner-only value; empty is never accepted and nothing is guessed for you
    param([string]$Label, [string]$Pattern = '', [switch]$Number)
    while ($true) {
        $v = (Read-Host ('  ' + $Label)).Trim()
        if ($v.Length -eq 0) { Write-Host '  empty is not allowed (the trial refuses with exit=4 rather than defaulting)' -ForegroundColor Yellow; continue }
        if ($Number) {
            if ($v -match '^-?[0-9]{1,3}(\.[0-9]{1,3})?$') { return $v }
            Write-Host '  must be a plain number with a DOT decimal (a comma is rejected by the invariant-culture parser)' -ForegroundColor Yellow; continue
        }
        if ($Pattern.Length -gt 0 -and $v -notmatch $Pattern) {
            Write-Host ('  must match: ' + $Pattern) -ForegroundColor Yellow; continue
        }
        return $v
    }
}

function Confirm-WjssState {
    # stops a stage BEFORE anything is set if the physical pre-state is not confirmed
    param([string]$Token, [string]$Prompt)
    $a = Read-Host ($Prompt + ' | type ' + $Token + ' to continue')
    if ($a -ne $Token) { throw ('STOP: pre-state not confirmed (typed "' + $a + '"), nothing was run.') }
}

function Export-WjssStageFiles {
    # rebuilds every labelled stage fence from the runbook; the .ps1 files are never edited by hand
    param([switch]$KeepExisting)
    $md = Join-Path $script:Repo 'docs\PHYSICAL_HARDWARE_EXECUTION_RUNBOOK_2026-11-07.md'
    if (-not (Test-Path -LiteralPath $md)) { throw 'STOP: this runbook file is not in the repo path you gave.' }
    $raw = Get-Content -LiteralPath $md -Raw
    $fence = [string]([char]0x60 * 3)
    $pat = '(?s)' + $fence + 'powershell stage:([A-Za-z0-9\-]+)\r?\n(.*?)' + $fence
    $ms = [regex]::Matches($raw, $pat)
    if ($ms.Count -ne 8) {
        throw ('STOP: expected 8 labelled stage blocks, found ' + [string]$ms.Count + '. git pull this branch, then paste this block again.')
    }
    foreach ($m in $ms) {
        $n = [string]$m.Groups[1].Value
        $b = [string]$m.Groups[2].Value
        $f = Join-Path $script:Ev ('stage-' + $n + '.ps1')
        if ($KeepExisting -and (Test-Path -LiteralPath $f)) {
            Write-Host ('  kept: stage-' + $n + '.ps1') -ForegroundColor DarkGray; continue
        }
        $head = '# generated from runbook HEAD ' + $script:Head.Substring(0, 8) + ' - do not edit this file; edit the runbook and re-run Export-WjssStageFiles'
        $need = 'if (-not (Test-Path function:\Invoke-WjssStage)) { throw "paste the shared block (section 2) of the runbook into THIS window first" }'
        Set-Content -LiteralPath $f -Value ($head + [Environment]::NewLine + $need + [Environment]::NewLine + $b) -Encoding utf8
        Write-Host ('  stage-{0,-8}  run with:  & "$script:Ev\stage-{0}.ps1"' -f $n)
    }
    Write-Host ('  files: ' + $script:Ev) -ForegroundColor DarkGray
}
```

หลังวางบล็อคร่วมเสร็จ ให้วางตามนี้ทีละบล็อค (บล็อคที่ 2 และ 3 คือสิ่งที่คุณใช้ทั้งวัน):

```powershell
Test-WjssBuild
```
พิมพ์ HEAD / sha256 ของ exe / เวลา build — เก็บบรรทัดนี้ไว้ให้ผมเทียบว่ารันบน build ไหน

```powershell
Export-WjssStageFiles
```
แตกบล็อค stage ทั้ง 8 ของเอกสารนี้ออกเป็นไฟล์ `.ps1` ในโฟลเดอร์หลักฐาน (เขียนทับไฟล์เดิมทุกครั้งเพื่อให้ตรงกับ runbook; ใช้ `Export-WjssStageFiles -KeepExisting` ถ้าไม่อยากให้ทับ) แล้วพิมพ์รายการคำสั่งให้คัดลอก

```powershell
& "$script:Ev\stage-A1-off.ps1"        # one line per stage - whichever stage is next
& "$script:Ev\stage-A2-run.ps1"        # do not run these two back to back: the pump state must change between them
```

> วิธีดูว่า stage ไหนชื่ออะไร: `Get-ChildItem $script:Ev -Filter 'stage-*.ps1' | Select-Object -ExpandProperty Name`

## 3. Stage A — Trial A: อ่าน Pump-ready บนสัญญาณจริง (อ่านอย่างเดียว)

ปิด gate: `REAL WAGO ENDPOINT CONNECTIVITY` + `Pump-ready rule บนสัญญาณจริง (strict >)` ทั้งสองฝั่ง

**A1 — ปั๊มหยุด** (คาดว่า **exit 2** = typed refusal เพราะ 0.7 bar ≤ 15 bar →นี่คือ PASS)

```powershell stage:A1-off
Confirm-WjssState -Token 'STOPPED' -Prompt 'A1: the pump must be STOPPED at the Panel (read-only trial)'
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

```powershell stage:A2-run
Confirm-WjssState -Token 'RUN' -Prompt 'A2: the pump must be RUNNING at the Panel and the pressure must look settled'
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

```powershell stage:B-off
$t = @()
$t += Format-WjssBits -Prefix 'DI' -Base 1 -Result (Read-WjssBits -Area 'DI' -Start 0 -Count 8)   # DI-001..DI-008
Save-WjssSend -Name 'B-off' -Lines $t
```

สั่ง **ปั๊ม RUN** จาก Panel รอให้นิ่ง แล้วรันบล็อคเดิมซ้ำ (เปลี่ยนชื่อเป็น `B-on`):

```powershell stage:B-on
$t = @()
$t += Format-WjssBits -Prefix 'DI' -Base 1 -Result (Read-WjssBits -Area 'DI' -Start 0 -Count 8)
Save-WjssSend -Name 'B-on' -Lines $t
```

ตัดสินจากสองก้อนที่ส่งผม (คุณไม่ต้องแปลเอง ผมแปลให้): **DI‑004** ต้องเปลี่ยนระดับเมื่อ RUN, **DI‑005** ต้องอยู่นิ่งฝั่ง inactive
ค่าที่จะใช้ใน Stage D = `ACTIVE_HIGH` ถ้าระดับ "ทำงาน" คือ 1, `ACTIVE_LOW` ถ้าคือ 0

## 5. Stage C — Trial B: IV1 OPEN/CLOSE ครั้งเดียว (actuation ครั้งแรก)

ปิด gate: `physical IV command + FC01 read-back` · ต้องมี B ผ่านแล้ว และ **ปั๊มต้องหยุด** (trial ต้องการ attestation นี้)

```powershell stage:C-iv1
Confirm-WjssState -Token 'CLEAR' -Prompt 'C: IV1 area clear, pump STOPPED, nobody at the lance'
Invoke-WjssStage -Name 'C-iv1' -ConsentConst 'PhysicalDoSingle' -ExpectExit 0 -Vars @{
  'WJSS_DRY_IV_TRIAL_MODE'               = 'SOFTWARE_SIMULATED_AI003'
  'WJSS_DRY_IV_TRIAL_VALVE'              = 'IV1'
  'WJSS_DRY_IV_TRIAL_PUMP_STOPPED'       = 'PUMP_STOPPED_OWNER_ATTESTED'
  'WJSS_DRY_IV_TRIAL_SIMULATED_AI003_BAR' = '0.7'
  'WJSS_VALVE_SUPERVISION_MS'            = '5000'
} -Show @('trial_code','accepted','evidence_kind','valve','physical_iv_command','open_command','close_command','physical_upper_feedback','physical_lower_feedback','cleanup','operator_abort')
```

- stage นี้ **light โดยเจตนา** (ค่า terminal ขึ้นกับ feedback จริงหน้างาน) — ตัวเลขทั้งหมดในก้อน send ผมวิเคราะห์เอง
- ถ้า `exit=4` = startup refused (env/consent ไม่ครบหรือชนกัน) · `exit=2` = refuse หลัง bind เช่น `IV_TRIAL_REFUSED_*_NOT_FIELD_CONFIRMED` / `CONTRADICTORY` → **นี่คือ fail-closed ทำงาน ไม่ใช่บั๊ก** หยุด stage แล้วส่ง log

## 6. Stage D — Integrated cleaning-path DRY cycle (actuation เต็มรูป)

ปิด gate: วงจร dry ทั้งรอบ (select pump → enable → run → AO ramp → supervision → cleanup → release)
**เงื่อนไขก่อนรัน:** B ผ่าน (รู้ขั้วสัญญาณแล้ว) + C ผ่าน + เจ้าของตัดสินใจ `docs/ACCELERATED_CLEANING_DRY_TRIAL_PACKAGE.md` §6/§7 แล้ว + Motion ยังไม่มีแรง (no-load)

```powershell stage:D-dry
Confirm-WjssState -Token 'DRY CYCLE AUTHORIZED' -Prompt 'D: Stage B and C passed, ACCELERATED package section 6/7 decided, motion unloaded'
$sid    = Read-WjssField -Label 'Canonical Sensor Id exactly as it appears in your catalogue CSV'
$ma     = Read-WjssField -Label 'Pump AO target in mA (the value you decided in the ACCELERATED package)' -Number
$runPol = Read-WjssField -Label 'Pump RUNNING polarity, from the Stage B result' -Pattern '^(ACTIVE_HIGH|ACTIVE_LOW)$'
$fltPol = Read-WjssField -Label 'Pump FAULT polarity, from the Stage B result' -Pattern '^(ACTIVE_HIGH|ACTIVE_LOW)$'
Invoke-WjssStage -Name 'D-dry' -ConsentConst 'PhysicalDoSingle' -ExpectExit 0 -Vars @{
  'WJSS_DRY_COMMAND_MODE'          = 'TEST_HARDWARE_DRY'
  'WJSS_CLEANING_SENSOR_ID'        = $sid
  'WJSS_PUMP_SELECTION'            = '1'
  'WJSS_PUMP_ANALOG_TARGET_MA'     = $ma
  'WJSS_PUMP_FRESHNESS_MS'         = '2000'
  'WJSS_PUMP_RUNNING_POLARITY'     = $runPol
  'WJSS_PUMP_FAULT_POLARITY'       = $fltPol
  'WJSS_PUMP_RUNNING_CONFIRM_MS'   = '10000'
  'WJSS_PUMP_READY_TIMEOUT_MS'     = '30000'
  'WJSS_PUMP_STOPPED_CONFIRM_MS'   = '15000'
  'WJSS_PUMP_READY_SETPOINT_BAR'   = '15'
  'WJSS_PRESSURE_LOW_BAR'          = '1'
  'WJSS_PRESSURE_HIGH_BAR'         = '15'
  'WJSS_VALVE_SUPERVISION_MS'      = '5000'
} -Show @('profile','mode','sensor','isolation_valve','water_jet','pump_selection','pump_owner','pump_gate','pump_feedback','pump_readiness','prepare','start','steps','terminal_status','terminal_code','primary_failure','write_authority','session_state','session_generation')
```

- 4 ค่าที่ผมห้ามเดาแทน (sensor id, mA target, polarity x2) ตอนนี้จะ**ถามบนหน้าจอ**ตอนรัน stage นั้น และตรวจรูปแบบให้ก่อน (ค่าว่างไม่ได้, ทศนิยมต้องใช้จุด ไม่ใช่จุลภาค)
- ถ้าพิมพ์ผิดรูป script จะถามซ้ำจนกว่าจะได้ค่าที่ถูกต้อง; ค่าที่ยังไงก็ไม่ถูกหลักจะถูก Product ปฏิเสธด้วย `exit=4` + refusal ที่บอกชื่อตัวแปร (ไม่ได้เดาแทนคุณ)
- **ห้าม re-run เพื่อหวังผล luck**: ถ้า `steps` หมด budget แล้วไม่มี terminal state ให้ส่ง log ทั้งก้อน
- cleanup ของ trial = ramp ลง → DO‑007 FALSE → ตรวจ → DO‑003 FALSE → ตรวจ → รอ not-running → ปล่อย ownership (script ตรวจ `cleanup=` จาก log ให้ผมดูอีกชั้น)

## 7. Stage E — WSB limit switches (Upper/Lower) + FC02/FC01 divergence probe

ปิด gate: `DI mirroring / limit-switch reclass` ตาม corrected CSV (DI‑021..028 = Upper, DI‑029..036 = Lower)
Legacy rev2 อ่าน bit กลุ่มนี้ผ่าน **FC01 (coils)** ส่วน Product อ่านผ่าน **FC02 (discrete inputs)** ที่ address เดียวกัน — บล็อคนี้วัดทั้งสองทางให้เห็นจริง (อ่านอย่างเดียว)

```powershell stage:E-idle
$t = @()
$t += Format-WjssBits -Prefix 'DI' -Base 21 -Result (Read-WjssBits -Area 'DI'   -Start 20 -Count 16)   # FC02, addr 20..35
$t += Format-WjssBits -Prefix 'CO' -Base 21 -Result (Read-WjssBits -Area 'COIL' -Start 20 -Count 16)   # FC01, same addresses
$t += '  note: at rest every switch should sit on one side, per the real wiring'
Save-WjssSend -Name 'E-idle' -Lines $t
```

จากนั้นให้ Owner ขยับ WSB ทีละตัว (หรือให้ปั๊ม/วาล์วทำงานในจังหวะที่มี limit switch เปลี่ยน) แล้วรันซ้ำ:

```powershell stage:E-moved
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
