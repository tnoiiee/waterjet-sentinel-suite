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

.PARAMETER Probe
    Runs the live HTTP assertions against a host that is already listening on the shared loopback
    port and starts nothing: the document and every allow-listed module must answer 200 with their
    own content type, an unknown or traversed module name must answer 404, and the shell liveness
    route must answer 200 with the ALIVE status. These are the answers a test project cannot assert
    here, because reaching a live listener would need a test-only package and this repository pins
    its dependency set centrally with a locked restore. Run it from a second session while a host is
    listening, or let a normal launch run it on the host it started: the same assertions, against a
    process the script does not need to own.

.PARAMETER SelfTest
    Runs the launcher's own assertions on its pure helpers (root resolution, address assembly, mode
    selection, build-output paths, the probe's served-and-refused surface, the presentation copy source
    and the copied Ui/ in the build output, cleanup no-op, and a check that this file cannot echo an
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
    [switch]$SelfTest,
    [switch]$Probe
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

function Get-WjssServedAssets
{
    # The names the host is willing to serve, with the content type each must come back with. It is a
    # copy of the host's own frozen whitelist on purpose: if the two drift, the probe stops agreeing
    # with the server, and that is the failure this script is meant to catch before an operator does.
    return @(
        @{ Path = 'global.css'; Status = 200; ContentType = 'text/css' },
        @{ Path = 'app.mjs'; Status = 200; ContentType = 'text/javascript' },
        @{ Path = 'feed.mjs'; Status = 200; ContentType = 'text/javascript' },
        @{ Path = 'topology.mjs'; Status = 200; ContentType = 'text/javascript' },
        @{ Path = 'chartKit.mjs'; Status = 200; ContentType = 'text/javascript' },
        @{ Path = 'shellBar.mjs'; Status = 200; ContentType = 'text/javascript' },
        @{ Path = 'tabs.mjs'; Status = 200; ContentType = 'text/javascript' },
        @{ Path = 'wallMap.mjs'; Status = 200; ContentType = 'text/javascript' },
        @{ Path = 'sensorDetail.mjs'; Status = 200; ContentType = 'text/javascript' },
        @{ Path = 'thermal.mjs'; Status = 200; ContentType = 'text/javascript' },
        @{ Path = 'dispatchPanel.mjs'; Status = 200; ContentType = 'text/javascript' },
        @{ Path = 'opsPanel.mjs'; Status = 200; ContentType = 'text/javascript' }
    )
}

function Get-WjssRefusedAssetPaths
{
    # An unknown name, a nested name and an encoded traversal are one answer for the host: 404. The
    # traversal forms are what a filter-based design would get wrong, so they are probed by name.
    return @(
        'unknown.mjs',
        'index.html',
        'App.mjs',
        'nested/app.mjs',
        '%2e%2e%2f%2e%2e%2fpackages.lock.json',
        '..%2f..%2fapps%2fui%2fREADME.md'
    )
}

function Invoke-WjssUiProbe
{
    param(
        [Parameter(Mandatory = $true)][string]$OriginRoot,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $problems = @()

    try
    {
        $document = Invoke-WebRequest -Uri $OriginRoot -UseBasicParsing -TimeoutSec 5
        if ($document.StatusCode -ne 200) { $problems += ('document: HTTP ' + [string]$document.StatusCode + ' instead of 200') }
        if ($document.Headers['Content-Type'] -notlike '*text/html*')
        {
            $problems += ('document: content type ' + [string]$document.Headers['Content-Type'])
        }
        if ($document.Content -notmatch 'type="module"[^>]*src="/ui/app\.mjs"')
        {
            $problems += 'document: the module element that boots the presentation is not in the served body'
        }
    }
    catch
    {
        $problems += ('document: no answer from ' + $OriginRoot + ' - ' + $_.Exception.Message)
    }

    foreach ($asset in Get-WjssServedAssets)
    {
        try
        {
            $response = Invoke-WebRequest -Uri ($OriginRoot + 'ui/' + $asset.Path) -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ne $asset.Status)
            {
                $problems += ($asset.Path + ': HTTP ' + [string]$response.StatusCode + ' instead of ' + [string]$asset.Status)
            }
            if ($response.Headers['Content-Type'] -notlike ('*' + $asset.ContentType + '*'))
            {
                $problems += ($asset.Path + ': content type ' + [string]$response.Headers['Content-Type'] + ', expected ' + $asset.ContentType)
            }
            if ($response.RawContentLength -lt 1) { $problems += ($asset.Path + ': served empty') }
        }
        catch
        {
            $problems += ($asset.Path + ': ' + $_.Exception.Message)
        }
    }

    foreach ($path in Get-WjssRefusedAssetPaths)
    {
        try
        {
            $response = Invoke-WebRequest -Uri ($OriginRoot + 'ui/' + $path) -UseBasicParsing -TimeoutSec 5
            $problems += ($path + ': HTTP ' + [string]$response.StatusCode + ' where only 404 is admissible')
        }
        catch
        {
            # A refused name must be refused by the whitelist, not by a transport accident, so a
            # connection-level failure is reported as itself rather than silently read as a 404. The
            # status is taken from the response only when the raised exception carries one, which is
            # what differs between the two shell generations, and Set-StrictMode makes a property
            # tested for rather than assumed.
            $status = 0
            $raised = $_.Exception
            if ($raised.PSObject.Properties.Name -contains 'Response' -and $null -ne $raised.Response)
            {
                $status = [int]$raised.Response.StatusCode
            }
            if ($status -ne 404)
            {
                $problems += ($path + ': HTTP ' + [string]$status + ' where only 404 is admissible')
            }
        }
    }

    try
    {
        $health = Invoke-WebRequest -Uri ($OriginRoot + 'api/health') -UseBasicParsing -TimeoutSec 5
        if ($health.StatusCode -ne 200) { $problems += ('api/health: HTTP ' + [string]$health.StatusCode + ' instead of 200') }
        $body = $health.Content | ConvertFrom-Json
        if ([string]$body.status -ne 'ALIVE') { $problems += ('api/health: status ' + [string]$body.status + ' is not ALIVE') }
        if ([string]$body.stageMarker -eq '') { $problems += 'api/health: no stage marker' }
    }
    catch
    {
        $problems += ('api/health: ' + $_.Exception.Message)
    }

    if ($problems.Count -gt 0)
    {
        foreach ($line in $problems) { Write-Output ('FAIL ' + $Label + ': ' + $line) }
        throw ('the live probe found ' + [string]$problems.Count + ' finding(s) on ' + $Label)
    }

    $served = 1 + (Get-WjssServedAssets).Count
    $refused = (Get-WjssRefusedAssetPaths).Count
    Write-Output ('ok   ' + $Label + ': ' + [string]$served + ' routes served, ' +
        [string]$refused + ' refused, liveness ALIVE')
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

    $served = Get-WjssServedAssets
    if ($served.Count -ne 12) { $bad += ('probe surface: ' + [string]$served.Count + ' assets, expected the 12 the host whitelists') }
    foreach ($asset in $served)
    {
        if ($asset.Status -ne 200) { $bad += ('probe surface: ' + $asset.Path + ' is not expected to answer 200') }
        if ($asset.ContentType -notin @('text/css', 'text/javascript'))
        {
            $bad += ('probe surface: ' + $asset.Path + ' carries an unexpected content type')
        }
    }
    if ((Get-WjssRefusedAssetPaths) -notcontains 'index.html')
    {
        $bad += 'probe surface: the document is not in the refused set, so the asset route could serve it twice'
    }
    $done += 'probe surface (12 served assets, 6 refused names, one answer each)'

    $uiSource = Join-Path $root 'apps\ui\public'
    if (-not (Test-Path -LiteralPath (Join-Path $uiSource 'index.html')))
    {
        $bad += ('source: no presentation document at ' + $uiSource + ', so the host content item can match nothing')
    }
    # The two ends of the copy, stated without a compiler because both are directory listings: the
    # source the build item points at, and the output the host reads at run time. This is exactly the
    # pair that was broken once, and a launcher can see it.
    foreach ($asset in $served)
    {
        if (-not (Test-Path -LiteralPath (Join-Path $uiSource $asset.Path)))
        {
            $bad += ('source: ' + $asset.Path + ' is expected by the probe but is not in ' + $uiSource)
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $runtimeDirectory 'Ui\index.html')))
    {
        $bad += ('output: the built host at ' + $runtimeDirectory + ' carries no Ui\index.html, so GET / answers 500')
    }
    $done += 'copy source and build output (13 presentation files, Ui/index.html in the host output)'

    $lines = @(Get-Content -LiteralPath $PSCommandPath)
    $writeOutNeedle = 'Write-' + 'Output'
    $envReadNeedle = 'GetEnvironment' + 'Variable'
    $echo = @($lines | Where-Object {
        $_.Contains($writeOutNeedle) -and $_.Contains('$endpoint') -and -not $_.Contains('endpointPresent')
    })
    if ($echo.Count -gt 0) { $bad += 'leak: a line prints an operator-supplied value' }
    $echo2 = @($lines | Where-Object { $_.Contains($envReadNeedle) -and $_.Contains($writeOutNeedle) })
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

if ($Probe)
{
    # A probe reads the host that is already up; it neither starts nor stops a process, so it can be run
    # beside a live kiosk without disturbing the screen.
    $origin = 'http://' + '127.0.0.1' + ':' + [string]$Port + '/'
    Invoke-WjssUiProbe -OriginRoot $origin -Label ('port ' + [string]$Port)
    return
}

$runtimeExecutable = Get-WjssExecutable -RepositoryRoot $repositoryRoot `
    -RelativeDirectory ('apps\runtime\bin\' + $Configuration + '\net10.0') -Name 'Wjss.Runtime.exe'
$shellExecutable = Get-WjssExecutable -RepositoryRoot $repositoryRoot `
    -RelativeDirectory ('apps\kiosk\bin\' + $Configuration + '\net10.0-windows') -Name 'Wjss.Kiosk.exe'

$runtime = $null
$shell = $null

try
{
    # Pre-flight, before anything is started: the routes read the copied tree, so a build output that
    # carries the host without it can only answer 500 and 404. Stopping here says so in one sentence
    # instead of leaving an operator to read a blank screen. (The build itself refuses to produce this
    # state since the VerifyOperatorUiOutput target was added; this line is the runner's copy of that.)
    $uiOutputDirectory = Join-Path (Split-Path -Parent $runtimeExecutable) 'Ui'
    if (-not (Test-Path -LiteralPath (Join-Path $uiOutputDirectory 'index.html')))
    {
        $stopLine = 'STOP: the host output has no Ui\index.html, so GET / would answer 500 and every /ui/ asset 404. '
        throw ($stopLine + 'Rebuild the solution: the build now fails fast when that copy matches no file.')
    }

    $runtime = Start-Process -FilePath $runtimeExecutable -WorkingDirectory $repositoryRoot `
        -PassThru -WindowStyle Hidden
    Write-Output ('  host started (pid {0}); waiting for its liveness route' -f $runtime.Id)
    Wait-WjssHost -Process $runtime -Address (Get-WjssProbeAddress -PortNumber $Port) -TimeoutSeconds 20 | Out-Null
    Write-Output '  host answered: the presentation and the API are same-origin on that port'
    Invoke-WjssUiProbe -OriginRoot ('http://' + '127.0.0.1' + ':' + [string]$Port + '/') -Label 'launched host'

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
