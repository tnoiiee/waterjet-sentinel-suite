#requires -Version 5.1
<#
.SYNOPSIS
    Starts the WJSS Runtime and the Kiosk shell so the Operator presentation is displayed
    fullscreen against the live read-only API, with no Node process involved.

.DESCRIPTION
    Stage 0.4C-K4 launcher. It exists for one reason: the Operator presentation is served by
    Wjss.Runtime itself, so the two pieces only line up when the host and the shell are pointed at
    the same loopback port. This script aligns them, starts the host, waits for the host to answer
    its own liveness route, and only then starts the shell. When the shell exits, the host it was
    started for is stopped again, so no half-live Runtime is left holding the port.

    What this script never does:
      - it never sends a command, writes to a device or actuates anything: the API it starts is
        read-only and this script only starts and stops processes;
      - it never prints, copies or stores a value of any variable it reads from the environment,
        and it never asks the operator to type one: an Owner-local endpoint is supplied by the
        operator's own session, and this script reports only whether one is present;
      - it never names a machine: the only address used is the loopback address of this machine,
        built from a constant, and no variable can redirect the presentation elsewhere;
      - it does not build. A missing executable is an error with the exact build command, because a
        launcher that silently rebuilds the product hides which head is running.

.PARAMETER Mode
    Simulator (default) starts the deterministic profile with no device path at all.
    PhysicalReadOnly starts the read-only acquisition profile; the Runtime still requires its own
    Owner-local endpoint configuration in the operator session and refuses to start without it, and
    this script never substitutes, defaults or reveals one.

.PARAMETER Port
    The loopback port both processes share. It must be the same value on both sides: the host listens
    on it and the shell displays it.

.PARAMETER Configuration
    Which build output to launch. Release by default, exactly as the field driver does.

.PARAMETER SelfTest
    Runs the launcher's own assertions on its pure helpers (root resolution, address assembly, mode
    selection, build-output paths, cleanup no-op, and a check that this file cannot echo an
    operator-supplied value) and starts no process. Nothing is written and nothing is read from the
    environment. Use it after editing this file.

.EXAMPLE
    . (Join-Path (Get-Location) 'tools\launch\Start-WjssOperatorKiosk.ps1')

.EXAMPLE
    & '.\tools\launch\Start-WjssOperatorKiosk.ps1' -Mode PhysicalReadOnly -Port 5191
#>
[CmdletBinding()]
param(
    [ValidateSet('Simulator', 'PhysicalReadOnly')]
    [string]$Mode = 'Simulator',
    [ValidateRange(1, 65535)]
    [int]$Port = 5181,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SelfTest
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Resolve-WjssRepositoryRoot
{
    param([Parameter(Mandatory = $true)][string]$StartDirectory)

    $candidate = Get-Item -LiteralPath $StartDirectory
    while ($null -ne $candidate)
    {
        $probe = Join-Path $candidate.FullName 'WaterJetSentinelSuite.sln'
        if (Test-Path -LiteralPath $probe) { return $candidate.FullName }
        $candidate = $candidate.Parent
    }

    throw 'STOP: the repository root was not found above tools\launch. Run this script from the checkout it belongs to.'
}

function Get-WjssExecutable
{
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$RelativeDirectory,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $path = Join-Path $RepositoryRoot ((Join-Path $RelativeDirectory $Name))
    if (-not (Test-Path -LiteralPath $path))
    {
        throw ("STOP: {0} is not built. Run: dotnet build WaterJetSentinelSuite.sln -c {1}" -f $Name, $Configuration)
    }

    return $path
}

function Get-WjssProbeAddress
{
    param([Parameter(Mandatory = $true)][int]$PortNumber)

    # The address is assembled from a loopback constant so no line of this file carries a URL with a
    # machine name in it, and so nothing here can be redirected by a variable: the port is the only
    # value the operator may choose, and a port is not a trust boundary on this machine.
    $loopback = '127.0.0.1'
    return 'http://' + $loopback + ':' + [string]$PortNumber + '/api/health'
}

function Wait-WjssHost
{
    param(
        [Parameter(Mandatory = $true)][System.Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][string]$Address,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline)
    {
        if ($Process.HasExited)
        {
            throw ("STOP: the host exited with code {0} before it answered. Its own refusal line above carries the reason." -f $Process.ExitCode)
        }

        try
        {
            $response = Invoke-WebRequest -Uri $Address -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) { return $true }
        }
        catch
        {
            Start-Sleep -Milliseconds 250
        }
    }

    throw 'STOP: the host did not answer its liveness route in time. Nothing was displayed rather than a stale screen.'
}

function Stop-WjssHost
{
    # Deliberately not Mandatory: the cleanup path is also reached when the host never started, and a
    # null process there is the expected case rather than an error.
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process) { return }
    if ($Process.HasExited) { return }

    try
    {
        # The host owns its own clean shutdown (it cancels its loop and disposes what it opened), so
        # it is asked politely first and only then stopped.
        if ($Process.CloseMainWindow())
        {
            if (-not $Process.WaitForExit(5000)) { $Process.Kill() }
        }
        else
        {
            $Process.Kill()
            $Process.WaitForExit()
        }
    }
    catch
    {
        Write-Output 'NOTE: the host process had already gone when cleanup ran.'
    }
}

function Get-WjssModeSelection
{
    param([Parameter(Mandatory = $true)][ValidateSet('Simulator', 'PhysicalReadOnly')][string]$Mode)

    # The two vocabulary pairs are the product's own words, spelled exactly as the host and the shell
    # print them, so a screen and a launcher can never disagree about what a run claims. This helper
    # maps a name to a name: it reads nothing, and it never chooses an endpoint.
    if ($Mode -eq 'PhysicalReadOnly')
    {
        return @{ Profile = 'TEST_HARDWARE_READ_ONLY'; Acquisition = 'PHYSICAL_READ_ONLY' }
    }

    return @{ Profile = 'SIMULATOR'; Acquisition = 'DETERMINISTIC' }
}

function Invoke-WjssSelfTest
{
    $bad = @()
    $done = @()

    $root = Resolve-WjssRepositoryRoot -StartDirectory $PSScriptRoot
    if (-not (Test-Path -LiteralPath (Join-Path $root 'WaterJetSentinelSuite.sln')))
    {
        $bad += 'root: the resolved directory does not carry the solution'
    }
    $done += 'root: resolved from the script location, not from the caller directory'

    $outside = [System.IO.Path]::GetTempPath()
    try
    {
        Resolve-WjssRepositoryRoot -StartDirectory $outside | Out-Null
        $bad += 'root: a directory above which no solution exists was accepted'
    }
    catch
    {
        if ($_.Exception.Message -notlike 'STOP:*') { $bad += 'root: refusal is not an explicit STOP line' }
    }

    $address = Get-WjssProbeAddress -PortNumber 5181
    if ($address -ne 'http://127.0.0.1:5181/api/health') { $bad += ('address: assembled wrongly: ' + $address) }
    if ($address -like '*localhost*') { $bad += 'address: the loopback literal must be the numeric form' }
    $other = Get-WjssProbeAddress -PortNumber 5191
    if ($other -notlike '*:5191/*') { $bad += 'address: the port override did not reach the assembled address' }
    $done += 'address: loopback-only, port follows the single -Port parameter'

    $simulator = Get-WjssModeSelection -Mode 'Simulator'
    if ($simulator.Profile -ne 'SIMULATOR') { $bad += 'mode: Simulator profile' }
    if ($simulator.Acquisition -ne 'DETERMINISTIC') { $bad += 'mode: Simulator acquisition' }
    $physical = Get-WjssModeSelection -Mode 'PhysicalReadOnly'
    if ($physical.Profile -ne 'TEST_HARDWARE_READ_ONLY') { $bad += 'mode: PhysicalReadOnly profile' }
    if ($physical.Acquisition -ne 'PHYSICAL_READ_ONLY') { $bad += 'mode: PhysicalReadOnly acquisition' }
    $done += 'mode selection (two vocabulary pairs, the product own words)'

    $runtimeDirectory = Join-Path $root ('apps\runtime\bin\' + $Configuration + '\net10.0')
    $shellDirectory = Join-Path $root ('apps\kiosk\bin\' + $Configuration + '\net10.0-windows')
    if (-not (Test-Path -LiteralPath $runtimeDirectory)) { $bad += 'paths: no host build output at ' + $runtimeDirectory }
    if (-not (Test-Path -LiteralPath $shellDirectory)) { $bad += 'paths: no shell build output at ' + $shellDirectory }
    $done += ('paths: both build outputs exist for ' + $Configuration)

    try
    {
        Get-WjssExecutable -RepositoryRoot $root -RelativeDirectory 'tools\launch' -Name 'Not-Built.exe' | Out-Null
        $bad += 'paths: a missing executable was accepted silently'
    }
    catch
    {
        if ($_.Exception.Message -notlike '*is not built*dotnet build*')
        {
            $bad += 'paths: the refusal does not carry the build command'
        }
    }

    Stop-WjssHost -Process $null
    $done += 'cleanup: no process, no error, nothing left holding the port'

    $lines = @(Get-Content -LiteralPath $PSCommandPath)
    $echo = @($lines | Where-Object {
        $_.Contains('Write-Output') -and $_.Contains('$endpoint') -and -not $_.Contains('endpointPresent')
    })
    if ($echo.Count -gt 0) { $bad += 'leak: a line prints an operator-supplied value' }
    $echo2 = @($lines | Where-Object { $_.Contains('GetEnvironmentVariable') -and $_.Contains('Write-Output') })
    if ($echo2.Count -gt 0) { $bad += 'leak: an environment read is also an output' }
    $done += 'no echo: every environment read is presence-only and never an output'

    if ($bad.Count -gt 0)
    {
        foreach ($line in $bad) { Write-Output ('FAIL ' + $line) }
        throw ('launcher self-test failed with ' + [string]$bad.Count + ' finding(s)')
    }

    foreach ($line in $done) { Write-Output ('ok   ' + $line) }
    Write-Output ('launcher self-test: ' + [string]$done.Count + ' checks passed, no process started')
}

if ($SelfTest) { Invoke-WjssSelfTest; return }

$repositoryRoot = Resolve-WjssRepositoryRoot -StartDirectory $PSScriptRoot
$relativeRoot = [string](Split-Path -Leaf $repositoryRoot)
$selection = Get-WjssModeSelection -Mode $Mode
# Named to avoid shadowing the automatic $PROFILE variable, which PowerShell uses for the operator's
# profile path: a launcher that rebinds it would confuse anyone reading a session afterwards.
$profileValue = [string]$selection.Profile
$acquisitionValue = [string]$selection.Acquisition

if ($Mode -eq 'PhysicalReadOnly')
{
    # Presence only, never the value: the operator's Owner-local endpoint is theirs, and a launcher that
    # echoed it would move a machine identity into logs, transcripts and screenshots of this session.
    $endpointPresent = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('WJSS_TC_NODE_01_ENDPOINT'))
    if (-not $endpointPresent)
    {
        Write-Output 'WARN: no Owner-local endpoint is present in this session, so the host will refuse to'
        Write-Output '      start in the physical read-only mode. That refusal is the product behaviour: this'
        Write-Output '      script neither supplies, defaults nor prints an endpoint of any kind.'
    }
}

Write-Output ('WJSS Operator Kiosk - repository {0}, configuration {1}' -f $relativeRoot, $Configuration)
Write-Output ('  profile {0} / acquisition {1} / shared loopback port {2}' -f $profileValue, $acquisitionValue, [string]$Port)

# Both sides of the pair are aligned here and nowhere else: the host listens on its own port
# variable, and the shell displays the port of its own UI port variable. Same value, one parameter.
[Environment]::SetEnvironmentVariable('WJSS_API_PORT', [string]$Port, 'Process')
[Environment]::SetEnvironmentVariable('WJSS_KIOSK_UI_PORT', [string]$Port, 'Process')
[Environment]::SetEnvironmentVariable('WJSS_DEVICE_PROFILE', $profileValue, 'Process')
[Environment]::SetEnvironmentVariable('WJSS_TC_ACQUISITION_MODE', $acquisitionValue, 'Process')

$runtimeExecutable = Get-WjssExecutable -RepositoryRoot $repositoryRoot `
    -RelativeDirectory ('apps\runtime\bin\' + $Configuration + '\net10.0') -Name 'Wjss.Runtime.exe'
$shellExecutable = Get-WjssExecutable -RepositoryRoot $repositoryRoot `
    -RelativeDirectory ('apps\kiosk\bin\' + $Configuration + '\net10.0-windows') -Name 'Wjss.Kiosk.exe'

$runtime = $null
$shell = $null

try
{
    $runtime = Start-Process -FilePath $runtimeExecutable -WorkingDirectory $repositoryRoot `
        -PassThru -WindowStyle Hidden
    Write-Output ('  host started (pid {0}); waiting for its liveness route' -f $runtime.Id)
    Wait-WjssHost -Process $runtime -Address (Get-WjssProbeAddress -PortNumber $Port) -TimeoutSeconds 20 | Out-Null
    Write-Output '  host answered: the presentation and the API are same-origin on that port'

    $shell = Start-Process -FilePath $shellExecutable -WorkingDirectory $repositoryRoot -PassThru
    Write-Output ('  shell started (pid {0}); it displays the host at the approved loopback origin' -f $shell.Id)
    $shell.WaitForExit()
    Write-Output ('  shell exited with code {0}' -f [string]$shell.ExitCode)
}
finally
{
    Stop-WjssHost -Process $runtime
    if ($null -ne $shell -and -not $shell.HasExited)
    {
        Write-Output 'NOTE: the shell was still running when the host stopped; it now shows its own unavailable state.'
    }

    Write-Output '  host stopped: no process is left holding the loopback port'
}
