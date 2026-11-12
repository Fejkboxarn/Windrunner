param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Label,
      [ValidateRange(5,60)][int]$Seconds = 15)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $project "Builds/$Label/Windrunner.exe"
$log = Join-Path $project "Logs/ProductionMigration/$Label-player.log"
$report = Join-Path $project "Logs/ProductionMigration/$Label-player.json"
if (!(Test-Path -LiteralPath $executable)) { throw 'Candidate executable is missing.' }
if ((Test-Path -LiteralPath $log) -or (Test-Path -LiteralPath $report)) { throw 'Smoke evidence already exists.' }
$process = Start-Process -FilePath $executable -ArgumentList @('-batchmode','-nographics','-logFile',"`"$log`"") -WindowStyle Hidden -PassThru
try {
    $exited = $process.WaitForExit($Seconds * 1000)
    $text = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
    $errors = @($text -split "`n" | Where-Object { $_ -match 'Exception:|\bError:|Crash!!!|Failed to load' })
    $result = [ordered]@{
        label = $Label; seconds = $Seconds; survived = !$exited
        hasPlayerInitialization = $text -match 'Initialize engine version:'
        errors = $errors; termination = 'Harness stops only its launched process after observation'
        scope = 'Headless process startup only; not rendered gameplay or graceful-shutdown validation'
    }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $report
    $result | ConvertTo-Json -Depth 4
    if ($exited -or !$result.hasPlayerInitialization -or $errors.Count -gt 0) { throw 'Player startup smoke failed; inspect evidence.' }
}
finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
    $process.Dispose()
}
