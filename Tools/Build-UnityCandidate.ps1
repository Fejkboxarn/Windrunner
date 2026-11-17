param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Label = 'candidate', [switch]$ProgressionFixture, [switch]$MainMenu, [switch]$MovementGym, [switch]$Development)
$ErrorActionPreference = 'Stop'
if (([int]$MainMenu.IsPresent + [int]$MovementGym.IsPresent + [int]$ProgressionFixture.IsPresent) -gt 1) { throw 'Select one candidate scene set.' }
$project = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Assert-UnityEditorClosed.ps1')
$version = ((Get-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion:').Line -split ': ')[1]
$editor = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
$log = Join-Path $project "Logs/ProductionMigration/$Label-build.log"
$report = Join-Path $project "Logs/ProductionMigration/$Label-build.json"
if ((Test-Path -LiteralPath $log) -or (Test-Path -LiteralPath $report) -or (Test-Path -LiteralPath (Join-Path $project "Builds/$Label"))) { throw 'Use a fresh label to preserve existing evidence/artifacts.' }
$arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', "`"$project`"", '-buildTarget', 'Win64', '-executeMethod', 'AirflowPrototype.Editor.WindrunnerCandidateBuild.BuildWindows', '-windrunnerBuildLabel', $Label, '-logFile', "`"$log`"")
if ($ProgressionFixture) { $arguments += '-windrunnerProgressionFixture' }
if ($MainMenu) { $arguments += '-windrunnerMainMenu' }
if ($MovementGym) { $arguments += '-windrunnerMovementGym' }
if ($Development) { $arguments += '-windrunnerDevelopment' }
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if (!(Test-Path -LiteralPath $report)) { throw "Build exited $($process.ExitCode) without report; inspect $log" }
$result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
$result | Format-List
if ($process.ExitCode -ne 0 -or $result.result -ne 'Succeeded') { throw 'Candidate build failed.' }
