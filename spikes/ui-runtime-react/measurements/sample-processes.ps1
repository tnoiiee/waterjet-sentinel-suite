# WJSS Stage 0.2.1A — Owner-local Windows process sampler (built-in cmdlets only).
# SYNTHETIC SPIKE MEASUREMENT TOOL. Samples node (harness) and msedge processes.
#
# Usage (separate PowerShell window, while the soak runs):
#   powershell -ExecutionPolicy Bypass -File .\measurements\sample-processes.ps1 -Minutes 10 -IntervalSeconds 5 -Label smoke
# Output: ..\results\raw\processes-<label>-<timestamp>.csv  (git-ignored raw result)
param(
  [int]$Minutes = 10,
  [int]$IntervalSeconds = 5,
  [string]$Label = 'smoke'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rawDir = Join-Path $root 'results\raw'
New-Item -ItemType Directory -Force -Path $rawDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $rawDir "processes-$Label-$stamp.csv"
$cores = [Environment]::ProcessorCount
$prev = @{}
$end = (Get-Date).AddMinutes($Minutes)
"Timestamp,Group,ProcessCount,CpuPercent,WorkingSetMb,PrivateMb,Handles" | Out-File -FilePath $out -Encoding utf8
while ((Get-Date) -lt $end) {
  $now = Get-Date
  foreach ($group in @('node', 'msedge')) {
    $procs = @(Get-Process -Name $group -ErrorAction SilentlyContinue)
    if ($procs.Count -eq 0) { continue }
    $cpuSeconds = ($procs | ForEach-Object { $_.TotalProcessorTime.TotalSeconds } | Measure-Object -Sum).Sum
    $cpuPct = ''
    if ($prev.ContainsKey($group)) {
      $dt = ($now - $prev[$group].At).TotalSeconds
      if ($dt -gt 0) { $cpuPct = [math]::Round((($cpuSeconds - $prev[$group].Cpu) / $dt / $cores) * 100, 2) }
    }
    $prev[$group] = @{ At = $now; Cpu = $cpuSeconds }
    $ws = [math]::Round((($procs | Measure-Object -Property WorkingSet64 -Sum).Sum) / 1MB, 1)
    $pm = [math]::Round((($procs | Measure-Object -Property PrivateMemorySize64 -Sum).Sum) / 1MB, 1)
    $h = ($procs | Measure-Object -Property HandleCount -Sum).Sum
    "$($now.ToString('o')),$group,$($procs.Count),$cpuPct,$ws,$pm,$h" | Out-File -FilePath $out -Append -Encoding utf8
  }
  Start-Sleep -Seconds $IntervalSeconds
}
Write-Host "written $out"
# Note: 'node' may include unrelated Node processes and 'msedge' includes all Edge processes.
# Close other Edge windows and Node tools before sampling for a clean attribution.
