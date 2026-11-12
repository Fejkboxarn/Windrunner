param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Label,
      [ValidateRange(5,60)][int]$Seconds = 15,
      [switch]$Rendered)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $project "Builds/$Label/Windrunner.exe"
$evidenceLabel = if ($Rendered) { "$Label-rendered" } else { $Label }
$log = Join-Path $project "Logs/ProductionMigration/$evidenceLabel-player.log"
$report = Join-Path $project "Logs/ProductionMigration/$evidenceLabel-player.json"
if (!(Test-Path -LiteralPath $executable)) { throw 'Candidate executable is missing.' }
if ((Test-Path -LiteralPath $log) -or (Test-Path -LiteralPath $report)) { throw 'Smoke evidence already exists.' }
$arguments = @('-batchmode','-logFile',"`"$log`"")
if (!$Rendered) { $arguments += '-nographics' }
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
try {
    $exited = $process.WaitForExit($Seconds * 1000)
    $process.Refresh()
    $text = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
    $errors = @($text -split "`n" | Where-Object { $_ -match 'Exception:|\bError:|Crash!!!|Failed to load' })
    $result = [ordered]@{
        label = $evidenceLabel; seconds = $Seconds; survived = !$exited
        rendered = [bool]$Rendered
        totalProcessCpuSeconds = $process.TotalProcessorTime.TotalSeconds
        peakWorkingSetBytes = $process.PeakWorkingSet64
        privateBytesAtEnd = $process.PrivateMemorySize64
        hasPlayerInitialization = $text -match 'Initialize engine version:'
        errors = $errors; termination = 'Harness stops only its launched process after observation'
        scope = 'Process startup/idle only, including startup CPU and memory; no frame/GPU budget, gameplay input, pixel or graceful-shutdown validation'
    }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $report
    $result | ConvertTo-Json -Depth 4
    if ($exited -or !$result.hasPlayerInitialization -or $errors.Count -gt 0) { throw 'Player startup smoke failed; inspect evidence.' }
}
finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
    $process.Dispose()
}
