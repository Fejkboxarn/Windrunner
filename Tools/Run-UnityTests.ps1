param(
    [ValidateSet('EditMode', 'PlayMode')][string]$Platform = 'EditMode',
    [string]$Label = 'regression',
    [switch]$Rendered,
    [string]$Filter = ''
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw 'A Unity Editor is running. Close it before starting project validation.'
}
$version = ((Get-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion:').Line -split ': ')[1]
$editor = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (!(Test-Path -LiteralPath $editor)) { throw "Matching Unity Editor missing: $editor" }
$results = Join-Path $project 'Logs/ProductionMigration'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$xml = Join-Path $results "$Label-$Platform.xml"
$log = Join-Path $results "$Label-$Platform.log"
if ((Test-Path -LiteralPath $xml) -or (Test-Path -LiteralPath $log)) { throw 'Use a fresh label to avoid overwriting validation evidence.' }
$arguments = @('-batchmode', '-projectPath', "`"$project`"", '-runTests', '-testPlatform', $Platform, '-testResults', "`"$xml`"", '-logFile', "`"$log`"")
if (!$Rendered) { $arguments += '-nographics' }
if ($Filter) { $arguments += @('-testFilter', $Filter) }
else { $arguments += @('-testCategory', '!Profiling') }
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if (!(Test-Path -LiteralPath $xml)) { throw "Unity exited $($process.ExitCode) without test results. Inspect $log" }
[xml]$report = Get-Content -LiteralPath $xml
$run = $report.'test-run'
Write-Output "Unity exit=$($process.ExitCode); result=$($run.result); total=$($run.total); passed=$($run.passed); failed=$($run.failed); skipped=$($run.skipped); XML=$xml; log=$log"
if ($process.ExitCode -ne 0 -or $run.result -ne 'Passed' -or [int]$run.total -eq 0) { throw 'Unity validation did not pass.' }
