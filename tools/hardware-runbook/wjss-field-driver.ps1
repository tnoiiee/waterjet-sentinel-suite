# WJSS field driver for the physical hardware execution runbook (docs/PHYSICAL_HARDWARE_EXECUTION_RUNBOOK_2026-11-07.md).
#
# HOW TO USE (one line, in a plain PowerShell window, on the machine wired to the T8 rack):
#     . "$Repo\tools\hardware-runbook\wjss-field-driver.ps1"      # dot-source, never run with &
#     Test-WjssDriver                                             # runs the built-in self-tests, no hardware needed
#     Export-WjssStageFiles                                       # writes the 10 stage scripts into the evidence folder
#     & "$script:Ev\stage-A1-off.ps1"                             # then one line per stage, in runbook order
#
# Contract of this file:
#   - loads and runs on Windows PowerShell 5.1 and PowerShell 7 (no type accelerators that do not exist,
#     no char arithmetic, no regex that parses markdown, no non-ASCII text anywhere);
#   - it never writes a consent phrase or the device IP into any file; the send block it prints is scrubbed;
#   - it never defaults a value the Owner must supply: an empty answer is re-asked, and the Product itself
#     refuses with exit code 4 rather than guessing;
#   - the read-only poller speaks FC01/FC02 only, so no stage in here can write to the device;
#   - Test-WjssDriver exercises the parser, the verdict comparer, the scrubber, the exporter and the
#     stage definitions inside the SAME PowerShell that will run the stages.
#
# SOURCE-AUTHORED IN ARENA. NOT EXECUTED IN ARENA: the Arena sandbox has no PowerShell interpreter, so the
# gates here are (a) the load-time parser of PowerShell itself, (b) Test-WjssDriver on the Owner's machine.

$script:WjssDriverVersion = '2026-11-19.1'
$ErrorActionPreference = 'Stop'
# A stage never runs on a driver whose own self-test has not passed in THIS window. Test-WjssDriver is
# the only thing that proves the parser, the verdict matcher and the expectations still agree with each
# other, and at 6865f58 that agreement broke silently: the fixture and its expectation had drifted apart,
# so a red self-test still let a stage execute. Loading sets this false; only a PASS sets it true.
$script:SelfTestPassed = $false

function Read-WjssAnswer {
    # one prompt, up to three attempts, never invents an answer
    param([string]$Prompt, [string]$Kind = 'text')
    for ($i = 0; $i -lt 3; $i = $i + 1) {
        $v = (Read-Host $Prompt).Trim()
        while ($v.Length -ge 2) {
            $first = $v.Substring(0, 1)
            $last = $v.Substring($v.Length - 1, 1)
            if (($first -eq '"' -and $last -eq '"') -or ($first -eq "'" -and $last -eq "'")) { $v = $v.Substring(1, $v.Length - 2).Trim() }
            else { break }
        }
        if ($v.Length -eq 0) { Write-Host '  empty answer is not accepted - type the value' -ForegroundColor Yellow; continue }
        if ($Kind -eq 'repo') {
            $probe = Join-Path $v 'WaterJetSentinelSuite.sln'
            if (-not (Test-Path -LiteralPath $probe)) { Write-Host '  no WaterJetSentinelSuite.sln in that folder' -ForegroundColor Yellow; continue }
            return (Resolve-Path -LiteralPath $v).Path
        }
        if ($Kind -eq 'ip') {
            if ($v -notmatch '^[0-9]{1,3}(\.[0-9]{1,3}){3}$') { Write-Host '  needs four dotted numbers' -ForegroundColor Yellow; continue }
            return $v
        }
        if ($Kind -eq 'number') {
            if ($v -match '^[0-9]{1,5}$') { return $v }
            Write-Host '  needs digits only' -ForegroundColor Yellow; continue
        }
        return $v
    }
    throw 'STOP: three unusable answers. Re-run the dot-source line.'
}

function Set-WjssDevice {
    # asked once per window; the IP lives in memory only
    $script:Repo   = Read-WjssAnswer -Kind 'repo' -Prompt 'Repo path (folder containing WaterJetSentinelSuite.sln)'
    $script:Ip     = Read-WjssAnswer -Kind 'ip'   -Prompt 'T8 device IP (this window only - never written to a file)'
    Write-Host '  The TCP port and unit id default to the same values the Product uses (ProcessIoTransportShape: 502 / 1).' -ForegroundColor DarkGray
    Write-Host '  Press Enter to keep the default, or type the value the T8 is configured with.' -ForegroundColor DarkGray
    $port = (Read-Host '  device TCP port').Trim()
    if ($port.Length -eq 0) { $script:Port = 502 }
    elseif ($port -match '^[0-9]{1,5}$') { $script:Port = [int]$port }
    else { throw 'STOP: the TCP port must be digits only.' }
    $unit = (Read-Host '  unit id').Trim()
    if ($unit.Length -eq 0) { $script:UnitId = 1 }
    elseif ($unit -match '^[0-9]{1,3}$') { $script:UnitId = [int]$unit }
    else { throw 'STOP: the unit id must be digits only (1..247).' }
    if ($script:Port -lt 1 -or $script:Port -gt 65535) { throw 'STOP: port out of range (1..65535).' }
    if ($script:UnitId -lt 1 -or $script:UnitId -gt 247) { throw 'STOP: unit id out of range (1..247).' }
    $script:Exe    = Join-Path $script:Repo 'apps\runtime\bin\Release\net10.0\Wjss.Runtime.exe'
    $script:Cat    = Join-Path $script:Repo 'docs\evidence\process-io-inventory-2026-10-22.csv'
    $script:Ev     = Join-Path $env:USERPROFILE ('wjss-evidence\' + (Get-Date -Format 'yyyy-MM-dd'))
    New-Item -ItemType Directory -Force -Path $script:Ev | Out-Null
    $headLine      = [string](& git -C $script:Repo rev-parse HEAD)
    $script:Head   = $headLine.Trim()
    if ($script:Head.Length -lt 40) { throw 'STOP: git did not return a commit sha in that folder.' }
    $script:Consent = @{}
    Write-Host ('  device set: repo ' + $script:Repo) -ForegroundColor DarkGray
    Write-Host ('  device set: port ' + [string]$script:Port + ', unit id ' + [string]$script:UnitId + ', evidence ' + $script:Ev) -ForegroundColor DarkGray
}

function Ensure-WjssDevice {
    # Set-WjssDevice is the only place any of these are set, and it sets them together. A guard that looked only
    # at $script:Repo therefore let a session that already held a repo path typed at the prompt (a dot-sourced
    # script shares the caller's scope) skip the whole setup, leaving $script:Ev, $script:Exe and $script:Head
    # null - and the failure arrived dozens of lines later as a bare InvokeMethodOnNull inside Save-WjssSend,
    # with no hint that the device was never configured. The guard now checks every derived value the stages
    # depend on, and re-runs the complete prompt set if any one of them is missing. Plain $script: references
    # only, exactly as the rest of this file uses them.
    foreach ($value in @($script:Ip, $script:Port, $script:UnitId, $script:Exe, $script:Cat, $script:Ev, $script:Head, $script:Consent)) {
        if ($null -eq $value) { Set-WjssDevice; return }
    }
    if (-not $script:Repo -or ([string]$script:Repo).Length -eq 0) { Set-WjssDevice; return }
    if (([string]$script:Head).Length -lt 40) { Set-WjssDevice }
}
function Test-WjssBuild {
    # refuses to test a stale binary: that is what protects the corrected scaling from being judged on an old exe
    Ensure-WjssDevice
    if (-not (Test-Path -LiteralPath $script:Exe)) {
        throw 'STOP: Wjss.Runtime.exe not found. Run: dotnet build WaterJetSentinelSuite.sln -c Release'
    }
    $exe = Get-Item -LiteralPath $script:Exe
    $newest = $null
    $roots = @((Join-Path $script:Repo 'apps'), (Join-Path $script:Repo 'packages'), (Join-Path $script:Repo 'adapters'))
    foreach ($r in $roots) {
        $files = @(Get-ChildItem -Path $r -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
        foreach ($f in $files) {
            if ($null -eq $newest) { $newest = $f }
            elseif ($f.LastWriteTime -gt $newest.LastWriteTime) { $newest = $f }
        }
    }
    if ($null -ne $newest -and $newest.LastWriteTime -gt $exe.LastWriteTime) {
        throw ('STOP: ' + $newest.Name + ' is newer than the exe. Rebuild Release, then run this stage again.')
    }
    $hash = 'unavailable'
    try { $hash = (Get-FileHash -LiteralPath $script:Exe -Algorithm SHA256).Hash.Substring(0, 12) } catch { }
    return ('HEAD ' + $script:Head.Substring(0, 8) + ' | driver ' + $script:WjssDriverVersion + ' | exe sha256 ' + $hash + ' | built ' + $exe.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))
}

function Read-WjssConsent {
    # the phrase is never printed and never stored in a file; it lives in this hashtable for the window
    param([string]$Const)
    Ensure-WjssDevice
    if (-not $script:Consent.ContainsKey($Const)) {
        Write-Host ('  open packages\contracts\ProcessIoContracts.cs and read the value of ProcessIoConsentPhrases.' + $Const) -ForegroundColor DarkGray
        $sec = Read-Host '  paste that phrase (input hidden)' -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
        try { $script:Consent[$Const] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    }
    return $script:Consent[$Const]
}

function Clear-WjssEnv {
    $names = @([Environment]::GetEnvironmentVariables().Keys)
    foreach ($n in $names) {
        if ($n -like 'WJSS_*') { [Environment]::SetEnvironmentVariable($n, $null) }
    }
}

function Get-WjssKeyValuePairs {
    # the one parser for every trial report: first key wins, value keeps any later '=' text
    param([string]$Path)
    $map = @{}
    if (-not (Test-Path -LiteralPath $Path)) { return $map }
    foreach ($line in @(Get-Content -LiteralPath $Path)) {
        $t = [string]$line
        $i = $t.IndexOf('=')
        if ($i -gt 0) {
            $k = $t.Substring(0, $i).Trim()
            if ($k.Length -gt 0 -and -not $map.ContainsKey($k)) { $map[$k] = $t.Substring($i + 1).Trim() }
        }
    }
    return $map
}

function Resolve-WjssVerdict {
    # compares the report against the expectation table. A leading '~' means "must end with", a leading '^'
    # means "must start with" (for report lines that pack several pairs after one key). Every other value is
    # compared exactly, and case-sensitively: the capitalisation in these reports is part of the contract
    # (Flag() prints TRUE/FALSE, bool.ToString() prints True/False), so a case change is a real change.
    param($Map, $Expect, [int]$Code, [int]$ExpectExit)
    $fail = @()
    if ($Code -ne $ExpectExit) { $fail += ('exit=' + [string]$Code + ' but this stage expects ' + [string]$ExpectExit) }
    if ($null -ne $Expect) {
        foreach ($k in @($Expect.Keys)) {
            $exp = [string]$Expect[$k]
            if (-not $Map.ContainsKey($k)) { $fail += ('key ' + $k + ' was not printed by the trial (expected ' + $exp + ')'); continue }
            $got = [string]$Map[$k]
            $mode = ''
            if ($exp.Length -gt 1) {
                $head = $exp.Substring(0, 1)
                if ($head -eq '~' -or $head -eq '^') { $mode = $head }
            }
            if ($mode -eq '~') {
                if (-not $got.EndsWith($exp.Substring(1))) { $fail += ($k + '=' + $got + ' does not end with ' + $exp.Substring(1)) }
            }
            elseif ($mode -eq '^') {
                if (-not $got.StartsWith($exp.Substring(1))) { $fail += ($k + '=' + $got + ' does not start with ' + $exp.Substring(1)) }
            }
            elseif ($got -cne $exp) { $fail += ($k + '=' + $got + ' but expected ' + $exp) }
        }
    }
    return $fail
}

function Save-WjssSend {
    param([string]$Name, $Lines, [string]$To = '')
    Ensure-WjssDevice
    $dir = $script:Ev
    if ($To.Length -gt 0) { $dir = $To }
    $txt = @()
    $txt += 'STAGE ' + $Name + ' | HEAD=' + $script:Head.Substring(0, 8) + ' | driver ' + $script:WjssDriverVersion + ' | ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    foreach ($l in @($Lines)) { $txt += [string]$l }
    $scrubbed = @()
    foreach ($l in $txt) { $scrubbed += ($l -replace '\b[0-9]{1,3}(\.[0-9]{1,3}){3}\b', '<IP>') }
    $p = Join-Path $dir ('stage-' + $Name + '.send.txt')
    Set-Content -LiteralPath $p -Value ($scrubbed -join [Environment]::NewLine) -Encoding utf8
    Write-Host '--- copy everything below to the assistant (IP already scrubbed) ---' -ForegroundColor Cyan
    foreach ($l in $scrubbed) { Write-Host $l }
    Write-Host ('--- saved: ' + $p + ' ---') -ForegroundColor DarkGray
    return $p
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
    # Checked in both scopes on purpose. The driver is dot-sourced at the prompt, so its script scope IS the
    # session scope and $script: is the right name here; $global: is read as well so the gate means the same
    # thing under any host that hands this function a different current script. It is not a way to get around
    # the self-test: both names are unset until Test-WjssDriver passes in this window.
    if ($script:SelfTestPassed -ne $true -and $global:SelfTestPassed -ne $true) {
        throw ('STOP: Test-WjssDriver has not passed in this window, so no stage runs. Run: Test-WjssDriver')
    }
    Ensure-WjssDevice
    Clear-WjssEnv
    if ($SkipBuildCheck) { Write-Host '  build check skipped by request' -ForegroundColor Yellow }
    else { Write-Host ('  build: ' + (Test-WjssBuild)) -ForegroundColor DarkGray }

    $outFile = Join-Path $script:Ev ('stage-' + $Name + '.out.txt')
    $errFile = Join-Path $script:Ev ('stage-' + $Name + '.err.txt')
    if (-not (Test-Path -LiteralPath $script:Exe)) { throw ('STOP: exe not found: ' + $script:Exe) }

    # The device profile each trial composes for, taken from the source at this commit and not from a
    # guess: DryIvTrial refuses any profile but TEST_HARDWARE_DRY (apps/runtime/DryIvTrial.cs:278), the
    # dry command composition refuses it too (apps/runtime/RuntimeHostOptions.cs:389), and the read-only
    # Pump-ready trial runs under TEST_HARDWARE. A wrong profile is refused before anything is contacted.
    $prof = 'TEST_HARDWARE'
    if ($Name -eq 'C-iv1' -or $Name -eq 'D-dry' -or $Name -eq 'D-pump' -or $Name -eq 'D-sim') { $prof = 'TEST_HARDWARE_DRY' }
    [Environment]::SetEnvironmentVariable('WJSS_DEVICE_PROFILE', $prof)
    [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_HOST', $script:Ip)
    [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_CATALOGUE', $script:Cat)
    # the poller and the trial must address the same socket
    [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_PORT', [string]$script:Port)
    [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_UNIT_ID', [string]$script:UnitId)
    foreach ($k in @($Vars.Keys)) { [Environment]::SetEnvironmentVariable($k, [string]$Vars[$k]) }
    if ($ConsentConst.Length -gt 0) {
        [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_CONSENT', (Read-WjssConsent -Const $ConsentConst))
    }

    $code = -1
    try {
        $proc = Start-Process -FilePath $script:Exe -WorkingDirectory $script:Repo -NoNewWindow -Wait -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile
        if ($null -ne $proc) {
            if (-not $proc.HasExited) { $proc.WaitForExit() }
            try { $code = [int]$proc.ExitCode } catch { Write-Host '  WARN: exit code not readable from the process object' -ForegroundColor Yellow }
            try { $proc.Dispose() } catch { }
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable('WJSS_PROCESS_IO_CONSENT', $null)
    }

    $map = Get-WjssKeyValuePairs -Path $outFile
    $errLines = @()
    if (Test-Path -LiteralPath $errFile) { $errLines = @(Get-Content -LiteralPath $errFile | Where-Object { ([string]$_).Length -gt 0 }) }

    $fail = Resolve-WjssVerdict -Map $map -Expect $Expect -Code $code -ExpectExit $ExpectExit

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
        for ($j = 0; $j -lt $take; $j = $j + 1) { $rep += ('  stderr| ' + [string]$errLines[$j]) }
    }
    if ($fail.Count -gt 0) {
        $rep += '  --- last 40 output lines (attached because this stage did not match) ---'
        $all = @(Get-Content -LiteralPath $outFile)
        $from = $all.Count - 40
        if ($from -lt 0) { $from = 0 }
        for ($j = $from; $j -lt $all.Count; $j = $j + 1) { $rep += ('  | ' + [string]$all[$j]) }
    }
    $rep += '  raw stdout: ' + $outFile

    if ($fail.Count -eq 0) {
        Write-Host ('RESULT ' + $Name + ': PASS (exit=' + [string]$code + ')') -ForegroundColor Green
        [void](Save-WjssSend -Name $Name -Lines $rep)
    }
    else {
        Write-Host ('RESULT ' + $Name + ': CHECK') -ForegroundColor Red
        $withFails = @()
        foreach ($m in $fail) { Write-Host ('   MISMATCH ' + $m) -ForegroundColor Red; $withFails += ('  MISMATCH ' + $m) }
        [void](Save-WjssSend -Name $Name -Lines ($withFails + $rep))
    }
}

function Read-WjssField {
    # one Owner-only value; nothing is guessed on your behalf
    param([string]$Label, [string]$Pattern = '', [switch]$Number, [switch]$Hash)
    while ($true) {
        $v = (Read-Host ('  ' + $Label)).Trim()
        if ($v.Length -eq 0) { Write-Host '  empty is not allowed (the trial refuses with exit=4 rather than defaulting)' -ForegroundColor Yellow; continue }
        if ($Number) {
            if ($v -match '^-?[0-9]{1,3}(\.[0-9]{1,3})?$') { return $v }
            Write-Host '  needs a plain number with a DOT decimal (a comma is rejected by the invariant-culture parser)' -ForegroundColor Yellow; continue
        }
        if ($Hash) {
            if ($v -match '^[0-9a-fA-F]{64}$') { return $v }
            Write-Host '  needs the 64 hex characters of the SHA-256 that Gate A expects' -ForegroundColor Yellow
            continue
        }
        if ($Pattern.Length -gt 0 -and $v -notmatch $Pattern) {
            Write-Host ('  must match: ' + $Pattern) -ForegroundColor Yellow; continue
        }
        return $v
    }
}

$script:MotionVarNames = @(
    'WJSS_MANUAL_MOTION_CONSENT',
    'WJSS_MOTION_CONTROLLER_01_ENDPOINT',
    'WJSS_MOTION_LIB_DIR',
    'WJSS_MOTION_LIB_GCLIB_SHA256',
    'WJSS_MOTION_LIB_GCLIBO_SHA256',
    'WJSS_MOTION_LIB_EXPECTED_VERSION',
    'WJSS_MANUAL_MOTION_PROFILE'
)

function Save-WjssMotionEnv {
    # The Cleaning Path composition also composes the motion side, so those names must survive the
    # per-stage Clear-WjssEnv. They are taken from THIS window at load time; nothing is invented and
    # nothing is read from a file, so a value that was never supplied stays missing.
    if ($null -eq $script:Motion) { $script:Motion = @{} }
    $all = [Environment]::GetEnvironmentVariables()
    foreach ($n in $script:MotionVarNames) {
        $v = [string]$all[$n]
        if ($v.Length -gt 0) { $script:Motion[$n] = $v }
    }
    return $script:Motion.Count
}

function Get-WjssMotionVars {
    # one prompt per missing motion value, and a refusal rather than a guess when nothing is given
    [void](Save-WjssMotionEnv)
    if ($null -eq $script:Motion) { $script:Motion = @{} }
    foreach ($k in $script:MotionVarNames) {
        $v = ''
        if ($script:Motion.ContainsKey($k)) { $v = [string]$script:Motion[$k] }
        if ($v.Length -eq 0) {
            if ($k -eq 'WJSS_MANUAL_MOTION_CONSENT') {
                $v = Read-WjssMotionConsent
            } elseif ($k -eq 'WJSS_MANUAL_MOTION_PROFILE') {
                $v = Read-WjssOwnerFile -Label 'Owner-local manual motion profile file (the same file your motion commissioning run was given)'
            } elseif ($k -eq 'WJSS_MOTION_CONTROLLER_01_ENDPOINT') {
                $v = Read-WjssField -Label 'Motion controller endpoint, exactly as in your motion commissioning run'
            } elseif ($k -eq 'WJSS_MOTION_LIB_DIR') {
                $v = Read-WjssOwnerFile -Label 'Directory that holds the motion native library'
            } else {
                $v = Read-WjssField -Label ('Gate A value for ' + $k + ' (the same value your motion commissioning window used)') -Hash
            }
        }
        if ($v.Length -eq 0) {
            throw ('STOP: ' + $k + ' is required by the Cleaning Path composition and nothing was supplied. Nothing was run.')
        }
        $script:Motion[$k] = $v
    }
    return $script:Motion
}

function Read-WjssOwnerFile {
    # an Owner-local file the Product refuses to default; asked for by path, checked on disk
    param([string]$Label)
    while ($true) {
        $v = (Read-Host ('  ' + $Label)).Trim()
        if ($v.Length -eq 0) {
            Write-Host '  empty is not allowed - the composition refuses with exit=2 rather than substituting a file' -ForegroundColor Yellow
            continue
        }
        if (-not (Test-Path -LiteralPath $v)) {
            Write-Host ('  no such file or folder: ' + $v) -ForegroundColor Yellow
            continue
        }
        return $v
    }
}

function Read-WjssMotionConsent {
    # the motion session confirmation: never printed, never written to a file, held for this window only
    Ensure-WjssDevice
    if (-not $script:Consent.ContainsKey('ManualMotionSession')) {
        Write-Host '  open adapters\motion-library\ManualMotionControllerOutcome.cs and read the value of ManualMotionControllerCodes.ExactConsentValue' -ForegroundColor DarkGray
        $sec = Read-Host '  paste that phrase (input hidden)' -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
        try { $script:Consent['ManualMotionSession'] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    }
    return $script:Consent['ManualMotionSession']
}

function Confirm-WjssState {
    # stops a stage before anything is set if the physical pre-state is not confirmed
    param([string]$Token, [string]$Prompt)
    $a = Read-Host ($Prompt + ' | type ' + $Token + ' to continue')
    if ($a -ne $Token) { throw ('STOP: pre-state not confirmed (typed "' + $a + '"), nothing was run.') }
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

function New-WjssBitReadRequest {
    # builds one 12-byte bit-read request for the bench transport (function codes 01/02). Pure, so
    # Test-WjssDriver can verify the frame
    # byte by byte: tid 0x0001, protocol id 0x0000 (any other value and the gateway drops the frame),
    # length 0x0006, then unit, function, start and quantity, each 16-bit field big-endian.
    param([int]$UnitId = 1, [int]$Fc = 2, [int]$Start = 0, [int]$Count = 16)
    $req = New-Object byte[] 12
    $req[0] = 0;  $req[1]  = 1
    $req[2] = 0;  $req[3]  = 0
    $req[4] = 0;  $req[5]  = 6
    $req[6] = [byte]$UnitId
    $req[7] = [byte]$Fc
    $req[8]  = [byte](($Start -shr 8) -band 255); $req[9]  = [byte]($Start -band 255)
    $req[10] = [byte](($Count -shr 8) -band 255); $req[11] = [byte]($Count -band 255)
    return $req
}

function Resolve-WjssBitReadReply {
    # decodes a bit-read reply without touching the network. Header is the first 9 bytes on the wire
    # (transaction 2, protocol 2, length 2, unit 1, function 1, byte count 1); Body is what followed it.
    # For an exception reply the last header byte IS the exception code and there is no body, so the
    # caller must not try to read one.
    param($Header, $Body, [int]$Count = 8, [int]$ExpectFc = 0)
    if ($null -eq $Header -or @($Header).Count -lt 9) { return @{ ok = $false; error = 'reply shorter than the 9-byte header'; bits = @() } }
    $fc = [int]$Header[7]
    $tail = [int]$Header[8]
    if ($fc -band 128) {
        return @{ ok = $false; error = ('protocol exception ' + [string]$tail + ' (the function code or the address range is not exposed by this device)'); bits = @() }
    }
    if ($ExpectFc -gt 0 -and $fc -ne $ExpectFc) {
        return @{ ok = $false; error = ('reply carried function code ' + [string]$fc + ' but ' + [string]$ExpectFc + ' was asked for'); bits = @() }
    }
    $need = (($Count + 7) -shr 3)
    if ($tail -lt $need -or @($Body).Count -lt $need) {
        return @{ ok = $false; error = ('short reply: byte count ' + [string]$tail + ' cannot carry ' + [string]$Count + ' bits'); bits = @() }
    }
    $bits = @()
    for ($i = 0; $i -lt $Count; $i = $i + 1) {
        $byte = [int]$Body[($i -shr 3)]
        $bits += (($byte -shr ($i -band 7)) -band 1)
    }
    return @{ ok = $true; fc = $fc; byte_count = $tail; bits = $bits }
}

function Read-WjssBits {
    # READ ONLY bit poller over the bench transport: FC02 discrete inputs, FC01 coils.
    # Nothing in here can write to the device.
    param([string]$Area = 'DI', [int]$Start = 0, [int]$Count = 16)
    $fc = 2
    if ($Area -eq 'COIL') { $fc = 1 }
    Ensure-WjssDevice
    $client = $null
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $client.ReceiveTimeout = 4000
        $client.SendTimeout = 4000
        $task = $client.ConnectAsync($script:Ip, $script:Port)
        if (-not $task.Wait(4000)) { return @{ ok = $false; error = 'connect timed out after 4000 ms'; bits = @() } }
        $stream = $client.GetStream()

        $req = New-WjssBitReadRequest -UnitId $script:UnitId -Fc $fc -Start $Start -Count $Count
        $stream.Write($req, 0, $req.Length)
        $stream.Flush()

        $hdr = New-Object byte[] 9
        Read-WjssExact -Stream $stream -Buffer $hdr -Offset 0 -Count 9
        $data = New-Object byte[] 0
        if (([int]$hdr[7] -band 128) -eq 0) {
            $byteCount = [int]$hdr[8]
            if ($byteCount -gt 0) {
                $data = New-Object byte[] $byteCount
                Read-WjssExact -Stream $stream -Buffer $data -Offset 0 -Count $byteCount
            }
        }
        $r = Resolve-WjssBitReadReply -Header $hdr -Body $data -Count $Count -ExpectFc $fc
        if ($r.ok -eq $true) { $r['start'] = $Start }
        return $r
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
    if ($null -eq $Result -or $Result.ok -ne $true) {
        $why = 'no result'
        if ($null -ne $Result) { $why = [string]$Result.error }
        return ('  ' + $Prefix + ': READ FAILED - ' + $why)
    }
    $parts = @()
    for ($i = 0; $i -lt $Result.bits.Count; $i = $i + 1) {
        $parts += ($Prefix + ('-{0:D3}=' -f ($Base + $i)) + [string]$Result.bits[$i])
    }
    return ('  ' + ($parts -join ' '))
}

# ------------------------------------------------------------------ the stage definitions (this is the authority, not the markdown)

$script:StageNames = @('A1-off', 'A2-run', 'B-off', 'B-on', 'C-iv1', 'D-pump', 'D-sim', 'D-dry', 'E-idle', 'E-moved')

$script:Stages = @{
    'A1-off' = {
        Confirm-WjssState -Token 'STOPPED' -Prompt 'A1: the pump must be STOPPED at the Panel (read-only trial)'
        Invoke-WjssStage -Name 'A1-off' -ConsentConst 'PhysicalDiAiRead' -ExpectExit 2 -Vars @{
            'WJSS_PUMP_READY_READ_TRIAL'   = 'AI-003_ONLY'
            'WJSS_PUMP_READY_SETPOINT_BAR' = '15'
            'WJSS_PUMP_READY_FRESHNESS_MS' = '2000'
        } -Expect @{
            'trial_code'                  = 'PUMP_READY_REFUSED_AT_OR_BELOW_SETPOINT'
            'source_tag'                  = 'AI-003+'
            'evidence_kind'               = 'PHYSICAL'
            'provenance'                  = 'PHYSICAL_READ'
            'quality'                     = 'GOOD'
            'fresh'                       = 'TRUE'
            'physical_ai003_verified'     = 'TRUE'
            'pump_ready'                  = 'FALSE'
            'physical_pump_verified'      = 'FALSE'
            'strict_comparison'           = '~=> FALSE'
            'session_state'               = 'CLOSED'
            'transport_released'          = 'TRUE'
            'ai002_used_for_decision'     = 'FALSE'
            'iv_command_executed'         = 'FALSE'
            'motion_executed'             = 'FALSE'
            'waterjet_command_executed'   = 'FALSE'
            'write_authorization_created' = 'FALSE'
        } -Show @('raw_value', 'engineering_bar', 'engineering_unit', 'configured_setpoint_bar', 'session_generation', 'observation_diagnostic')
    }
    'A2-run' = {
        Confirm-WjssState -Token 'RUN' -Prompt 'A2: the pump must be RUNNING at the Panel and the pressure must look settled'
        Invoke-WjssStage -Name 'A2-run' -ConsentConst 'PhysicalDiAiRead' -ExpectExit 0 -Vars @{
            'WJSS_PUMP_READY_READ_TRIAL'   = 'AI-003_ONLY'
            'WJSS_PUMP_READY_SETPOINT_BAR' = '15'
            'WJSS_PUMP_READY_FRESHNESS_MS' = '2000'
        } -Expect @{
            'trial_code'              = 'PUMP_READY_ACCEPTED_PHYSICAL'
            'evidence_kind'           = 'PHYSICAL'
            'provenance'              = 'PHYSICAL_READ'
            'quality'                 = 'GOOD'
            'fresh'                   = 'TRUE'
            'physical_ai003_verified' = 'TRUE'
            'pump_ready'              = 'TRUE'
            'physical_pump_verified'  = 'TRUE'
            'strict_comparison'       = '~=> TRUE'
            'session_state'           = 'CLOSED'
            'transport_released'      = 'TRUE'
            'iv_command_executed'     = 'FALSE'
            'motion_executed'         = 'FALSE'
            'write_authorization_created' = 'FALSE'
        } -Show @('raw_value', 'engineering_bar', 'engineering_unit', 'configured_setpoint_bar', 'quality', 'fresh', 'session_generation')
    }
    'B-off' = {
        $t = @()
        $t += Format-WjssBits -Prefix 'DI' -Base 1 -Result (Read-WjssBits -Area 'DI' -Start 0 -Count 8)
        $t += '  pump state at the Panel: STOPPED (ask the operator, do not infer it)'
        [void](Save-WjssSend -Name 'B-off' -Lines $t)
    }
    'B-on' = {
        $t = @()
        $t += Format-WjssBits -Prefix 'DI' -Base 1 -Result (Read-WjssBits -Area 'DI' -Start 0 -Count 8)
        $t += '  pump state at the Panel: RUNNING (DI-004 must have changed, DI-005 must stay inactive)'
        [void](Save-WjssSend -Name 'B-on' -Lines $t)
    }
    'C-iv1' = {
        Confirm-WjssState -Token 'CLEAR' -Prompt 'C: IV1 area clear, pump STOPPED, nobody at the lance'
        Invoke-WjssStage -Name 'C-iv1' -ConsentConst 'PhysicalDoSingle' -ExpectExit 0 -Vars @{
            'WJSS_DRY_IV_TRIAL_MODE'                = 'SOFTWARE_SIMULATED_AI003'
            'WJSS_DRY_IV_TRIAL_VALVE'               = 'IV1'
            'WJSS_DRY_IV_TRIAL_PUMP_STOPPED'        = 'PUMP_STOPPED_OWNER_ATTESTED'
            # must be strictly above WJSS_PUMP_READY_SETPOINT_BAR or the trial refuses with
            # IV_TRIAL_REFUSED_SIMULATED_PRESSURE_AT_OR_BELOW_SETPOINT (DryIvTrial.cs:150)
            'WJSS_DRY_IV_TRIAL_SIMULATED_AI003_BAR' = '16'
            'WJSS_VALVE_SUPERVISION_MS'             = '5000'
            # the IV trial reads all three and refuses with IV_TRIAL_REFUSED_THRESHOLDS without them;
            # they are never defaulted (DryIvTrial.cs:845-866)
            'WJSS_PUMP_READY_SETPOINT_BAR'          = '15'
            'WJSS_PRESSURE_LOW_BAR'                 = '1'
            'WJSS_PRESSURE_HIGH_BAR'                = '15'
        } -Expect ([ordered]@{
            'trial_code'                = 'IV_TRIAL_ACCEPTED'
            'accepted'                  = 'True'
            'evidence_kind'             = 'SOFTWARE_SIMULATED'
            'trial_purpose'             = 'IV_OPEN_CLOSE_HARDWARE_TRIAL_ONLY'
            'physical_pump_verified'    = 'FALSE'
            'physical_ai003_verified'   = 'FALSE'
            'physical_iv_command'       = 'TRUE'
            'physical_upper_feedback'   = 'TRUE'
            'physical_lower_feedback'   = 'TRUE'
            # the Product prints the hardware tag rather than the catalogue alias, e.g.
            # open_command=DO-023 coil=22 value=True admission=OK dispatch=DISPATCHED. The tail is what
            # the gate is about, so it is asserted that way: a different bench wiring still fails loudly
            # instead of passing silently on a hard-coded DO number.
            'open_command'              = '~value=True admission=OK dispatch=DISPATCHED'
            'close_command'             = '~value=False admission=OK dispatch=DISPATCHED'
            'cleanup.complete'          = 'True'
            'cleanup.refused'           = '0'
            'cleanup.transport_failures' = '0'
            'cleanup.unknown'           = '0'
        }) -Show @('valve', 'open_command', 'close_command', 'open_observation', 'closed_observation', 'cleanup', 'cleanup.attempted', 'cleanup.closed', 'cleanup.refused', 'operator_abort', 'PUMP_READY_EVIDENCE', 'PHYSICAL_PUMP_EVIDENCE', 'queue', 'surface', 'motion')
    }
    'D-pump' = {
        # Stage D, first variant: the Pump command ladder ALONE. The Product composes no equipment
        # boundary in this run at all (apps/runtime/PumpCommandDryTrial.cs), so no Isolation Valve
        # command exists to be sent; the valve stays exactly where Stage C left it.
        Confirm-WjssState -Token 'PUMP LADDER ONLY' -Prompt 'D-pump: the pump runs for real while the isolation valve stays closed, so the line dead-heads - confirm the relief or return path is open, the nozzle is clear of people, and that you do NOT expect any valve motion in this run'
        $ma     = Read-WjssField -Label 'Pump AO target in mA (the value you decided in the ACCELERATED package)' -Number
        $runPol = Read-WjssField -Label 'Pump RUNNING polarity, from the Stage B result' -Pattern '^(ACTIVE_HIGH|ACTIVE_LOW)$'
        $fltPol = Read-WjssField -Label 'Pump FAULT polarity, from the Stage B result' -Pattern '^(ACTIVE_HIGH|ACTIVE_LOW)$'
        Invoke-WjssStage -Name 'D-pump' -ConsentConst 'PhysicalDoSingle' -ExpectExit 0 -Vars @{
            'WJSS_PUMP_COMMAND_DRY_TRIAL'   = 'PUMP_ONLY_NO_VALVES'
            'WJSS_DRY_COMMAND_MODE'         = 'TEST_HARDWARE_DRY'
            'WJSS_PUMP_SELECTION'           = '1'
            'WJSS_PUMP_ANALOG_TARGET_MA'    = $ma
            'WJSS_PUMP_FRESHNESS_MS'        = '2000'
            'WJSS_PUMP_RUNNING_POLARITY'    = $runPol
            'WJSS_PUMP_FAULT_POLARITY'      = $fltPol
            'WJSS_PUMP_RUNNING_CONFIRM_MS'  = '10000'
            'WJSS_PUMP_READY_TIMEOUT_MS'    = '30000'
            'WJSS_PUMP_STOPPED_CONFIRM_MS'  = '15000'
            'WJSS_PUMP_READY_SETPOINT_BAR'  = '15'
            'WJSS_PRESSURE_LOW_BAR'         = '1'
            'WJSS_PRESSURE_HIGH_BAR'        = '15'
            # nothing else: this trial composes no store, no sensor parameters and no motion, so the WAGO
            # T8 alone is the whole device surface it touches
        } -Expect ([ordered]@{
            'trial_identity'                = 'PHYSICAL_PUMP_COMMAND_ONLY_NO_VALVES'
            'evidence_kind'                 = 'PHYSICAL'
            'physical_pump_verified'        = 'True'
            'start_accepted'                = 'True'
            'selection_pair_written'        = 'True'
            'pump_enable_written'           = 'True'
            'pump_run_written'              = 'True'
            'running_feedback_confirmed'    = 'True'
            'analog_ramp_executed'          = 'True'
            'pump_ready_confirmed'          = 'True'
            'fault_feedback_never_active'   = 'True'
            'analog_stop_sentinel_written'  = 'True'
            'pump_run_cleared'              = 'True'
            'pump_enable_released'          = 'True'
            'terminal_state'                = 'STOPPED'
            'stopped_confirmed'             = 'True'
            'cleanup_state'                 = 'COMPLETE'
            'ownership_released'            = 'True'
            'safe_to_report_complete'       = 'True'
            'primary_failure'               = 'NONE'
            'iv_command_executed'           = 'FALSE'
            'iv_command_surface'            = 'NONE_NO_VALVE_BOUNDARY_COMPOSED'
            'motion_composed'               = 'False'
            'trial_code'                    = 'PUMP_COMMAND_TRIAL_ACCEPTED'
            'session_state'                 = 'CLOSED'
            'transport_released'            = 'True'
        }) -Show @('pump_selection', 'pump_analog_target_ma', 'pump_ready_setpoint_bar', 'ladder', 'ladder_complete', 'run_reached_ready', 'dispatch_summary', 'dispatch_window_ms', 'dispatch_address_agreement', 'command_dispatches', 'command_dispatch_refusals', 'run_cleared_dispatch', 'enable_released_dispatch', 'stop_accepted', 'stop_code', 'running_commissioned', 'fault_commissioned', 'running_binding', 'fault_binding', 'last_running_usable', 'last_running_raw_bit', 'last_running_active', 'last_running_code', 'last_running_observation_raw', 'last_fault_usable', 'last_fault_raw_bit', 'last_fault_active', 'last_fault_code', 'last_fault_observation_raw', 'network_attempts', 'transport_errors', 'transport_error_summary', 'pump_outlet_pressure_bar', 'pump_outlet_pressure_raw', 'pump_outlet_pressure_fresh', 'ai003_evidence_verified', 'ai003_pump_ready_rule', 'valve_open_admitted_by_lifecycle', 'terminal_state', 'cleanup_state', 'primary_failure', 'session_state')
    }
    'D-sim' = {
        # Stage D, second variant: the Cleaning Path for real, on a DECLARED Pump Outlet Pressure. The
        # pump must stay stopped at the panel: the Product never selects it, never enables it and never
        # writes the analog reference (apps/runtime/CleaningPathDryComposition.cs, step 7b), while the
        # paired isolation valve, the limit switches and the whole P1-P6 path stay physical. The evidence
        # is labelled SOFTWARE_SIMULATED and physical_pump_verified=False, so it can never be read as a
        # verified pump start.
        Confirm-WjssState -Token 'PUMP STOPPED' -Prompt 'D-sim: the pump must be STOPPED and left stopped at the Panel for the whole run - this stage opens the paired isolation valve and walks the path on a declared pump-outlet pressure, with no pump command issued'
        $sid    = Read-WjssField -Label 'Canonical Sensor Id exactly as it appears in your catalogue CSV'
        $ma     = Read-WjssField -Label 'Pump AO target in mA (still asked: the composition validates its pump configuration, then never uses it)' -Number
        $runPol = Read-WjssField -Label 'Pump RUNNING polarity, from the Stage B result' -Pattern '^(ACTIVE_HIGH|ACTIVE_LOW)$'
        $fltPol = Read-WjssField -Label 'Pump FAULT polarity, from the Stage B result' -Pattern '^(ACTIVE_HIGH|ACTIVE_LOW)$'
        $bar    = Read-WjssField -Label 'Declared Pump Outlet Pressure in bar, must be strictly above 15' -Number
        # This variant composes the WHOLE Cleaning Path, so the composition also reads the Owner-local
        # topology and the motion side. Those names are asked for here because every stage starts from an
        # environment that Clear-WjssEnv emptied on purpose - nothing is inherited and nothing is invented.
        $v = @{
            'WJSS_DRY_COMMAND_MODE'           = 'TEST_HARDWARE_DRY'
            'WJSS_DRY_SIMULATED_PUMP_READY'   = 'SOFTWARE_SIMULATED_AI003'
            'WJSS_DRY_SIMULATED_PUMP_STOPPED' = 'PUMP_STOPPED_OWNER_ATTESTED'
            'WJSS_DRY_SIMULATED_AI003_BAR'    = $bar
            'WJSS_CLEANING_SENSOR_ID'         = $sid
            'WJSS_PUMP_SELECTION'             = '1'
            'WJSS_PUMP_ANALOG_TARGET_MA'      = $ma
            'WJSS_PUMP_FRESHNESS_MS'          = '2000'
            'WJSS_PUMP_RUNNING_POLARITY'      = $runPol
            'WJSS_PUMP_FAULT_POLARITY'        = $fltPol
            'WJSS_PUMP_RUNNING_CONFIRM_MS'   = '10000'
            'WJSS_PUMP_READY_TIMEOUT_MS'     = '30000'
            'WJSS_PUMP_STOPPED_CONFIRM_MS'   = '15000'
            'WJSS_PUMP_READY_SETPOINT_BAR'   = '15'
            'WJSS_PRESSURE_LOW_BAR'          = '1'
            'WJSS_PRESSURE_HIGH_BAR'         = '15'
            'WJSS_VALVE_SUPERVISION_MS'      = '5000'
            'WJSS_CLEANING_PATH_STORE'         = Read-WjssOwnerFile -Label 'Owner-local Cleaning Path store file (the canonical topology the resolver reads)'
            'WJSS_SENSOR_PARAMETER_CSV'        = Read-WjssOwnerFile -Label 'Owner-local sensor parameter CSV (the Sensor-owned P1-P6 parameters)'
        }
        $mv = Get-WjssMotionVars
        foreach ($k in @($mv.Keys)) { $v[$k] = $mv[$k] }
        Invoke-WjssStage -Name 'D-sim' -ConsentConst 'PhysicalDoSingle' -ExpectExit 0 -Vars $v -Expect ([ordered]@{
            'profile'                           = 'TEST_HARDWARE_DRY'
            'mode'                              = 'WJSS_DRY_COMMAND_MODE'
            'prepare'                           = 'DRY_PREPARE_READY'
            'terminal_status'                   = 'CompleteAwaitingSafeReturn'
            'terminal_code'                     = 'PATH_AND_VALVE_SEQUENCE_COMPLETE_AWAITING_SAFE_RETURN'
            'primary_failure'                   = 'NONE'
            'pump_gate'                          = 'DECLARED_PUMP_PRECONDITION'
            'pump_evidence_kind'                 = 'SOFTWARE_SIMULATED'
            'physical_pump_verified'             = 'False'
            'simulated_pump_ready_rule'           = 'DECLARED_ABOVE_SETPOINT'
            'cleanup.pump.attempted'             = 'False'
            'cleanup.pump.terminal'              = 'IDLE'
            'cleanup.pump.state'                 = 'COMPLETE'
            'cleanup.pump.stopped_confirmed'     = 'False'
            'cleanup.pump.ownership_released'     = 'True'
            'cleanup.pump.complete'              = 'True'
            'cleanup.motion.complete'            = 'True'
            'cleanup.valves.complete'            = 'True'
            'cleanup.valves.refused'             = '0'
            'cleanup.valves.transport_failures'  = '0'
            'cleanup.valves.unknown'             = '0'
            'cleanup.complete'                   = 'True'
            'write_authority'                    = 'PROCESS_IO_SESSION'
            'coordinators'                       = 'COMPOSITION+EXECUTION'
            'session_state'                      = 'CLOSED'
        }) -Show @('pump_evidence_kind', 'simulated_ai003_bar', 'simulated_pump_ready_rule', 'pump_gate', 'pump_selection', 'isolation_valve', 'start', 'terminal_status', 'terminal_code', 'primary_failure', 'steps', 'cleanup.pump.attempted', 'cleanup.valves.attempted', 'cleanup.valves.closed', 'cleanup.complete', 'session_state')
    }
    'D-dry' = {
        Confirm-WjssState -Token 'DRY CYCLE AUTHORIZED' -Prompt 'D: Stage B and C passed, the ACCELERATED package owner decisions are recorded, motion unloaded. This composition ramps the pump itself and opens the paired isolation valve inside the cycle, so with water in the loop the lance is pressurised: the software cannot see whether the water supply is open, only you can'
        $sid    = Read-WjssField -Label 'Canonical Sensor Id exactly as it appears in your catalogue CSV'
        $ma     = Read-WjssField -Label 'Pump AO target in mA (the value you decided in the ACCELERATED package)' -Number
        $runPol = Read-WjssField -Label 'Pump RUNNING polarity, from the Stage B result' -Pattern '^(ACTIVE_HIGH|ACTIVE_LOW)$'
        $fltPol = Read-WjssField -Label 'Pump FAULT polarity, from the Stage B result' -Pattern '^(ACTIVE_HIGH|ACTIVE_LOW)$'
        # the wet variant composes the whole path as well, so it needs the same Owner-local topology and
        # motion values as D-sim (asked for here, after the environment was deliberately cleared)
        $v = @{
            'WJSS_DRY_COMMAND_MODE'        = 'TEST_HARDWARE_DRY'
            'WJSS_CLEANING_SENSOR_ID'      = $sid
            'WJSS_PUMP_SELECTION'           = '1'
            'WJSS_PUMP_ANALOG_TARGET_MA'    = $ma
            'WJSS_PUMP_FRESHNESS_MS'        = '2000'
            'WJSS_PUMP_RUNNING_POLARITY'    = $runPol
            'WJSS_PUMP_FAULT_POLARITY'      = $fltPol
            'WJSS_PUMP_RUNNING_CONFIRM_MS'  = '10000'
            'WJSS_PUMP_READY_TIMEOUT_MS'    = '30000'
            'WJSS_PUMP_STOPPED_CONFIRM_MS'  = '15000'
            'WJSS_PUMP_READY_SETPOINT_BAR'  = '15'
            'WJSS_PRESSURE_LOW_BAR'         = '1'
            'WJSS_PRESSURE_HIGH_BAR'        = '15'
            'WJSS_VALVE_SUPERVISION_MS'     = '5000'
            'WJSS_DRY_WET_SEQUENCE_ACK'      = 'WATER_AT_NOZZLE_AUTHORIZED'
            'WJSS_CLEANING_PATH_STORE'       = Read-WjssOwnerFile -Label 'Owner-local Cleaning Path store file (the canonical topology the resolver reads)'
            'WJSS_SENSOR_PARAMETER_CSV'      = Read-WjssOwnerFile -Label 'Owner-local sensor parameter CSV (the Sensor-owned P1-P6 parameters)'
        }
        $mv = Get-WjssMotionVars
        foreach ($k in @($mv.Keys)) { $v[$k] = $mv[$k] }
        Invoke-WjssStage -Name 'D-dry' -ConsentConst 'PhysicalDoSingle' -ExpectExit 0 -Vars $v -Expect ([ordered]@{
            'profile'                           = 'TEST_HARDWARE_DRY'
            'mode'                              = 'WJSS_DRY_COMMAND_MODE'
            'prepare'                           = 'DRY_PREPARE_READY'
            'terminal_status'                   = 'CompleteAwaitingSafeReturn'
            'terminal_code'                     = 'PATH_AND_VALVE_SEQUENCE_COMPLETE_AWAITING_SAFE_RETURN'
            'primary_failure'                   = 'NONE'
            'pump_evidence_kind'                = 'PHYSICAL'
            'cleanup.pump.stopped_confirmed'    = 'True'
            'cleanup.pump.ownership_released'   = 'True'
            'cleanup.pump.complete'              = 'True'
            'cleanup.motion.complete'            = 'True'
            'cleanup.valves.complete'            = 'True'
            'cleanup.valves.refused'             = '0'
            'cleanup.valves.transport_failures'  = '0'
            'cleanup.valves.unknown'             = '0'
            'cleanup.complete'                   = 'True'
            'pump_ready_source'                  = 'AI-003+'
            'pump_feedback'                     = 'COMMISSIONED'
            'pump_gate'                          = 'PUMP_PRECONDITION'
            'physical_pump_verified'             = 'True'
            'write_authority'                    = 'PROCESS_IO_SESSION'
            'coordinators'                      = 'COMPOSITION+EXECUTION'
        }) -Show @('sensor', 'isolation_valve', 'water_jet', 'axes', 'catalogue_rows', 'endpoint', 'process_io_device', 'pump_selection', 'pump_owner', 'pump_readiness', 'start', 'steps', 'session_state', 'session_generation', 'cleanup.pump.attempted', 'cleanup.pump.terminal', 'cleanup.pump.state', 'cleanup.motion.session', 'cleanup.motion.close', 'cleanup.valves.attempted', 'cleanup.valves.closed', 'motion_port', 'motion_profile')
    }
    'E-idle' = {
        $t = @()
        $t += Format-WjssBits -Prefix 'DI' -Base 21 -Result (Read-WjssBits -Area 'DI' -Start 20 -Count 16)
        $t += Format-WjssBits -Prefix 'CO' -Base 21 -Result (Read-WjssBits -Area 'COIL' -Start 20 -Count 16)
        $t += '  note: at rest every WSB switch should sit on one side, per the real wiring'
        [void](Save-WjssSend -Name 'E-idle' -Lines $t)
    }
    'E-moved' = {
        $t = @()
        $t += Format-WjssBits -Prefix 'DI' -Base 21 -Result (Read-WjssBits -Area 'DI' -Start 20 -Count 16)
        $t += Format-WjssBits -Prefix 'CO' -Base 21 -Result (Read-WjssBits -Area 'COIL' -Start 20 -Count 16)
        $t += '  run this after moving each WSB switch by hand, one at a time'
        [void](Save-WjssSend -Name 'E-moved' -Lines $t)
    }
}

function Show-WjssStage {
    # prints the real code of a stage, so the runbook can never drift from what executes
    param([string]$Name)
    if (-not $script:Stages.ContainsKey($Name)) { throw ('STOP: no stage named ' + $Name + '. Known: ' + ($script:StageNames -join ', ')) }
    Write-Host ('--- stage-' + $Name + ' ---') -ForegroundColor Cyan
    Write-Host ([string]$script:Stages[$Name])
}

function Export-WjssStageFiles {
    param([switch]$KeepExisting, [string]$To = '')
    Ensure-WjssDevice
    $dir = $script:Ev
    if ($To.Length -gt 0) { $dir = $To; New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $guard = 'if (-not (Test-Path function:\Invoke-WjssStage)) { throw "dot-source tools\hardware-runbook\wjss-field-driver.ps1 in this window first" }'
    foreach ($n in $script:StageNames) {
        if (-not $script:Stages.ContainsKey($n)) { throw ('STOP: stage ' + $n + ' is listed but not defined.') }
        $f = Join-Path $dir ('stage-' + $n + '.ps1')
        if ($KeepExisting -and (Test-Path -LiteralPath $f)) { Write-Host ('  kept: stage-' + $n + '.ps1') -ForegroundColor DarkGray; continue }
        $head = '# generated from driver ' + $script:WjssDriverVersion + ' at HEAD ' + $script:Head.Substring(0, 8) + ' - do not edit this file; edit the driver and re-run Export-WjssStageFiles'
        $body = $head + [Environment]::NewLine + $guard + [Environment]::NewLine + [string]$script:Stages[$n]
        Set-Content -LiteralPath $f -Value $body -Encoding utf8
        Write-Host ('  stage-{0,-8}  run with:  & "{1}\stage-{0}.ps1"' -f $n, $dir)
    }
    Write-Host ('  ' + [string]$script:StageNames.Count + ' files in ' + $dir) -ForegroundColor DarkGray
}

function Test-WjssDriver {
    # self-tests of everything that does NOT need hardware, executed by the same PowerShell that will run the stages
    Ensure-WjssDevice
    $bad = @()
    $done = @()

    if ($script:StageNames.Count -ne 10) { $bad += ('expected 10 stage names, got ' + [string]$script:StageNames.Count) }
    # assembled at run time so that this file never contains the consent wording itself
    $needle = 'I ' + 'AUTH' + 'ORIZE'
    foreach ($n in $script:StageNames) {
        if (-not $script:Stages.ContainsKey($n)) { $bad += ('stage ' + $n + ' missing'); continue }
        $text = [string]$script:Stages[$n]
        if ($text.Length -lt 40) { $bad += ('stage ' + $n + ' is empty') }
        if ($text -notmatch ([regex]::Escape("'" + $n + "'"))) { $bad += ('stage ' + $n + ' does not name itself') }
        if ($text.Contains($needle)) { $bad += ('stage ' + $n + ' contains a consent literal') }
        if ($text -match '\b[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\b') { $bad += ('stage ' + $n + ' contains an IP literal') }
    }
    $done += '10 stage definitions present, no consent literal, no endpoint literal'

    $self = [string]$MyInvocation.PSCommandPath
    if ($self.Length -eq 0 -or -not (Test-Path -LiteralPath $self)) {
        $self = Join-Path $script:Repo 'tools\hardware-runbook\wjss-field-driver.ps1'
    }
    $haveSelf = Test-Path -LiteralPath $self
    if ($haveSelf) {
        $own = [string](Get-Content -LiteralPath $self -Raw)
        if ($own.Contains($needle)) { $bad += 'the driver itself carries a consent literal' }
        if ($own -match '[^\x00-\x7F]') { $bad += 'the driver file is not pure ASCII (paste hazard)' }
        $done += 'driver file is ASCII-only and consent-free'
    }
    else { $done += 'driver file not found (pasted manually) - file-level checks skipped' }

    # parser test on a synthetic report (first key wins, '=' inside a value, junk lines ignored)
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('wjss-selftest-' + (Get-Date -Format 'HHmmss'))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    $log = @(
        'WJSS pump-ready read trial report',
        '  trial_code=PUMP_READY_ACCEPTED_PHYSICAL',
        '  strict_comparison=16.6399 > 15 => TRUE',
        '  open_command=IV1 coil=6 value=True admission=ADMITTED dispatch=DISPATCHED',
        '  fresh=TRUE',
        '  raw_value=13631',
        '  raw_value=99999',
        '  note = has spaces around the sign',
        ''
    )
    Set-Content -LiteralPath (Join-Path $tmp 'fake.out.txt') -Value $log -Encoding utf8
    $map = Get-WjssKeyValuePairs -Path (Join-Path $tmp 'fake.out.txt')
    if ([string]$map['trial_code'] -ne 'PUMP_READY_ACCEPTED_PHYSICAL') { $bad += 'parser: trial_code not read' }
    if ([string]$map['raw_value'] -ne '13631') { $bad += 'parser: first key did not win' }
    if ([string]$map['strict_comparison'] -notlike '*=> TRUE') { $bad += 'parser: value truncated at a later =' }
    if ([string]$map['open_command'] -notlike 'IV1 coil=6*') { $bad += 'parser: the packed open_command line was not kept whole' }
    $done += 'key=value parser (7 lines, duplicate keys, trailing =, junk line, packed pairs)'

    # verdict test: match, wrong value, ends-with, missing key, wrong exit
    $e1 = @{ trial_code = 'PUMP_READY_ACCEPTED_PHYSICAL'; strict_comparison = '~=> TRUE'; fresh = 'TRUE' }
    if ((Resolve-WjssVerdict -Map $map -Expect $e1 -Code 0 -ExpectExit 0).Count -ne 0) { $bad += 'verdict: a correct run was reported as CHECK' }
    $e2 = @{ fresh = 'FALSE' }
    if ((Resolve-WjssVerdict -Map $map -Expect $e2 -Code 0 -ExpectExit 0).Count -ne 1) { $bad += 'verdict: a wrong value was not caught' }
    if ((Resolve-WjssVerdict -Map $map -Expect @{ nope = 'x' } -Code 0 -ExpectExit 0).Count -ne 1) { $bad += 'verdict: a missing key was not caught' }
    if ((Resolve-WjssVerdict -Map $map -Expect $null -Code 2 -ExpectExit 0).Count -ne 1) { $bad += 'verdict: a wrong exit code was not caught' }
    if ((Resolve-WjssVerdict -Map $map -Expect @{ strict_comparison = '~=> FALSE' } -Code 0 -ExpectExit 0).Count -ne 1) { $bad += 'verdict: the ends-with sentinel did not fire' }
    if ((Resolve-WjssVerdict -Map $map -Expect @{ open_command = '^IV1' } -Code 0 -ExpectExit 0).Count -ne 0) { $bad += 'verdict: the starts-with sentinel rejected a correct line' }
    if ((Resolve-WjssVerdict -Map $map -Expect @{ open_command = '^IV2' } -Code 0 -ExpectExit 0).Count -ne 1) { $bad += 'verdict: the starts-with sentinel did not fire' }
    if ((Resolve-WjssVerdict -Map $map -Expect @{ fresh = 'true' } -Code 0 -ExpectExit 0).Count -ne 1) { $bad += 'verdict: comparison is not case-sensitive (TRUE vs true would be hidden)' }
    $done += 'verdict comparer (pass, wrong value, missing key, exit mismatch, ends-with, starts-with, case sensitivity)'

    # bit-read framing test: the request bytes and the reply decoder are verified here, before any socket is opened
    $req = New-WjssBitReadRequest -UnitId 1 -Fc 2 -Start 300 -Count 16
    if (@($req).Count -ne 12) { $bad += 'bitread: request is not 12 bytes' }
    if ([int]$req[2] -ne 0 -or [int]$req[3] -ne 0) { $bad += 'bitread: protocol id must be 0x0000' }
    if ([int]$req[4] -ne 0 -or [int]$req[5] -ne 6) { $bad += 'bitread: length field must be 6' }
    if ([int]$req[6] -ne 1 -or [int]$req[7] -ne 2) { $bad += 'bitread: unit/function bytes wrong' }
    if (([int]$req[8] * 256 + [int]$req[9]) -ne 300) { $bad += 'bitread: start address not big-endian' }
    if (([int]$req[10] * 256 + [int]$req[11]) -ne 16) { $bad += 'bitread: quantity not big-endian' }
    $h9 = New-Object byte[] 9
    $h9[0] = 0; $h9[1] = 1; $h9[2] = 0; $h9[3] = 0; $h9[4] = 0; $h9[5] = 3; $h9[6] = 1; $h9[7] = 2; $h9[8] = 1
    $b1 = New-Object byte[] 1
    $b1[0] = 1
    $dec = Resolve-WjssBitReadReply -Header $h9 -Body $b1 -Count 8 -ExpectFc 2
    if ($dec.ok -ne $true) { $bad += 'bitread: a valid FC02 reply was rejected' }
    if (@($dec.bits).Count -ne 8) { $bad += 'bitread: bit count wrong after decode' }
    if ([int]$dec.bits[0] -ne 1 -or [int]$dec.bits[1] -ne 0) { $bad += 'bitread: bits are not read least-significant-first' }
    $h9e = New-Object byte[] 9
    $h9e[0] = 0; $h9e[1] = 1; $h9e[2] = 0; $h9e[3] = 0; $h9e[4] = 0; $h9e[5] = 3; $h9e[6] = 1; $h9e[7] = 130; $h9e[8] = 2
    $de = Resolve-WjssBitReadReply -Header $h9e -Body (New-Object byte[] 0) -Count 8 -ExpectFc 1
    if ($de.ok -eq $true) { $bad += 'bitread: an exception reply was decoded as data' }
    if ($de.error -notmatch 'exception 2') { $bad += 'bitread: exception code not reported' }
    $db = Resolve-WjssBitReadReply -Header $h9 -Body (New-Object byte[] 0) -Count 16 -ExpectFc 2
    if ($db.ok -eq $true) { $bad += 'bitread: a truncated reply was accepted' }
    $h9w = New-Object byte[] 9
    $h9w[0] = 0; $h9w[1] = 1; $h9w[2] = 0; $h9w[3] = 0; $h9w[4] = 0; $h9w[5] = 3; $h9w[6] = 1; $h9w[7] = 3; $h9w[8] = 1
    $dw = Resolve-WjssBitReadReply -Header $h9w -Body $b1 -Count 8 -ExpectFc 2
    if ($dw.ok -eq $true) { $bad += 'bitread: a reply with the wrong function code was accepted' }
    $done += 'bit-read framing (12-byte request, FC02/FC01 reply, exception, truncated reply, wrong function code)'

    # scrub test: the send file must not contain the address it was given. The address is assembled at
    # run time (RFC 5737 documentation space) so this file never contains a literal endpoint.
    $probe = '203.0.113.' + '9'
    $fake = Save-WjssSend -Name 'selftest' -Lines @('  host=' + $probe, '  plain=ok') -To $tmp
    $written = [string](Get-Content -LiteralPath $fake -Raw)
    if ($written.Contains($probe)) { $bad += 'scrub: the address reached the file' }
    if ($written -notmatch '<IP>') { $bad += 'scrub: no replacement marker was written' }
    $done += 'IP scrubber runs before the send file is written'

    # export round-trip: generated file text must equal the definition, and carry the guard
    Export-WjssStageFiles -To $tmp | Out-Null
    foreach ($n in $script:StageNames) {
        $f = Join-Path $tmp ('stage-' + $n + '.ps1')
        if (-not (Test-Path -LiteralPath $f)) { $bad += ('export: missing stage-' + $n + '.ps1'); continue }
        $txt = [string](Get-Content -LiteralPath $f -Raw)
        if ($txt -notmatch 'Test-Path function:') { $bad += ('export: stage-' + $n + ' lost the guard line') }
        try { [void][scriptblock]::Create($txt) }
        catch { $bad += ('export: stage-' + $n + '.ps1 does not parse on this machine: ' + $_.Exception.Message) }
        if ($txt -notmatch 'Invoke-WjssStage|Save-WjssSend') { $bad += ('export: stage-' + $n + ' body is empty') }
        $a = @((([string]$script:Stages[$n]) -split "`r?`n") | Where-Object { ([string]$_).Trim().Length -gt 0 })
        $b = @(($txt -split "`r?`n") | Where-Object { ([string]$_).Trim().Length -gt 0 })
        if ($a.Count -lt 1) { $bad += ('export: stage-' + $n + ' definition has no body line') }
        elseif ($b.Count -lt 3) { $bad += ('export: stage-' + $n + '.ps1 has no body') }
        elseif ([string]$a[0].Trim() -ne [string]$b[2].Trim()) { $bad += ('export: stage-' + $n + ' first body line differs from the definition') }
    }
    $done += ('exporter round-trip (' + [string]$script:StageNames.Count + ' files parsed by this PowerShell, guard line, body matches the definition)')

    # every WJSS_* name this driver sets must exist in the Product source
    $all = @()
    foreach ($n in $script:StageNames) { $all += [string]$script:Stages[$n] }
    if ($haveSelf) { $all += (Get-Content -LiteralPath $self -Raw) }
    $used = @{}
    foreach ($m in [regex]::Matches([string]($all -join [Environment]::NewLine), 'WJSS_[A-Z0-9_]+')) { $used[$m.Value] = $true }
    $corpus = ''
    $roots = @((Join-Path $script:Repo 'packages'), (Join-Path $script:Repo 'apps\runtime'),
               (Join-Path $script:Repo 'apps\motion-composition'), (Join-Path $script:Repo 'adapters\motion-library'))
    foreach ($r in $roots) {
        if (-not (Test-Path -LiteralPath $r)) { $bad += ('source tree missing, cannot audit: ' + $r); continue }
        foreach ($fi in @(Get-ChildItem -Path $r -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })) {
            $corpus += [string](Get-Content -LiteralPath $fi.FullName -Raw)
        }
    }
    if ($corpus.Length -lt 100000) { $bad += ('the Product source corpus is too small to audit against (' + [string]$corpus.Length + ' chars) - wrong repo path?') }

    $missing = @()
    foreach ($k in @($used.Keys)) {
        if (-not $corpus.Contains('"' + $k + '"')) { $missing += $k }
    }
    if ($missing.Count -gt 0) { $bad += ('environment names not present in Product source: ' + ($missing -join ', ')) }
    $done += 'every WJSS_* name used (' + [string]$used.Count + ') exists in the Product source tree'

    # every value this driver ASSERTS must be a literal that exists in the Product source, so a stale or
    # invented expectation is caught here, on the same machine that will run the stages
    $badv = @()
    $nva = 0
    foreach ($n in $script:StageNames) {
        foreach ($line in (([string]$script:Stages[$n]) -split "`n")) {
            $mm = [regex]::Match($line, "^\s*'([a-z0-9_.]+)'\s*=\s*'([^']+)'")
            if (-not $mm.Success) { continue }
            $v = [string]$mm.Groups[2].Value
            if ($v.Substring(0, 1) -eq '~' -or $v.Substring(0, 1) -eq '^') { continue }
            if ($v -notmatch '^[A-Za-z0-9_\-\+\.]{4,}$') { continue }
            if ($v -ceq 'True' -or $v -ceq 'False') { continue }
            $nva += 1
            if (-not $corpus.Contains($v)) { $badv += ($n + ':' + [string]$mm.Groups[1].Value + '=' + $v) }
        }
    }
    if ($badv.Count -gt 0) { $bad += ('asserted values that are not literals in the Product source: ' + ($badv -join ', ')) }
    $done += 'every asserted code (' + [string]$nva + ') exists verbatim in the Product source corpus'

    # build freshness gate must be able to speak
    $b = Test-WjssBuild
    if ([string]$b -notmatch 'HEAD') { $bad += 'Test-WjssBuild returned nothing usable' }
    $done += 'build freshness check: ' + [string]$b

    foreach ($d in $done) { Write-Host ('  ok   ' + $d) -ForegroundColor DarkGray }
    if ($bad.Count -gt 0) {
        foreach ($m in $bad) { Write-Host ('  FAIL ' + $m) -ForegroundColor Red }
        throw ('STOP: driver self-test failed with ' + [string]$bad.Count + ' problem(s). Do not run stages; send this to the assistant.')
    }
    Write-Host ('SELFCHECK: PASS (driver ' + $script:WjssDriverVersion + ', ' + [string]$done.Count + ' groups)') -ForegroundColor Green
    $script:SelfTestPassed = $true
    Write-Host ('  self-test scratch: ' + $tmp + ' (evidence folder stays clean)') -ForegroundColor DarkGray
}

# The motion-side values a Cleaning Path stage needs are snapshotted NOW, at load, because every stage
# starts by emptying the process environment on purpose. A value this window was given before the driver was
# loaded is therefore kept for the stages that require it; a value it was never given stays missing and is
# asked for (or refused) by the stage, never invented here.
[void](Save-WjssMotionEnv)

Write-Host ('WJSS field driver ' + $script:WjssDriverVersion + ' loaded. Next: Test-WjssDriver, then Export-WjssStageFiles.') -ForegroundColor Green
if ($script:Motion.Count -gt 0) {
    Write-Host ('  motion-side values carried from this window: ' + [string]$script:Motion.Count + ' of ' + [string]$script:MotionVarNames.Count + ' (the rest are asked for by the D-sim/D-dry stage)') -ForegroundColor DarkGray
}
Write-Host ('  stages: ' + ($script:StageNames -join ', ')) -ForegroundColor DarkGray
