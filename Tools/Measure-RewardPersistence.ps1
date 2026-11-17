param([Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Label)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$log = Join-Path $project "Logs/ProductionMigration/$Label-reward-player.log"
if (Test-Path -LiteralPath $log) { throw 'Use a fresh benchmark label.' }
$exe = Join-Path $project "Builds/$Label/Windrunner.exe"
$process = Start-Process -FilePath $exe -ArgumentList @('-batchmode','-windrunnerRewardBenchmark','-logFile',('"{0}"' -f $log)) -WindowStyle Hidden -PassThru
try {
    if (!$process.WaitForExit(120000)) { Stop-Process -Id $process.Id; throw 'Benchmark timed out. Inspect its player log.' }
    if ($process.ExitCode -ne 0) { throw "Player exited with $($process.ExitCode)." }
    $lines = Get-Content -LiteralPath $log
    $lines | Select-String '^REWARD_(PROFILE|TRAVERSAL)'
    if (@($lines | Select-String '^REWARD_PROFILE ').Count -ne 3 -or !($lines | Select-String '^REWARD_TRAVERSAL ') -or ($lines | Select-String 'Exception:|Could not save on exit:')) {
        throw 'Incomplete or failed benchmark.'
    }
    $fixture = ($lines | Select-String '^Reward shutdown fixture (.+) ([0-9a-f]{32}) ([0-9a-f]{32})$').Matches
    if ($fixture.Count -ne 1) { throw 'Missing pre-quit revision.' }
    $root = [IO.Path]::GetFullPath($fixture[0].Groups[1].Value)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if ([IO.Path]::GetDirectoryName($root) -ne $temp -or [IO.Path]::GetFileName($root) -notmatch '^Windrunner-rewards-[0-9a-f]{32}$') {
        throw 'Unexpected disposable fixture directory.'
    }
    $id = $fixture[0].Groups[2].Value
    $revision = $fixture[0].Groups[3].Value
    foreach ($name in 'session.json','session.json.bak') {
        $envelope = Get-Content -LiteralPath (Join-Path $root "$id/$name") -Raw -Encoding UTF8 | ConvertFrom-Json
        $hash = [Security.Cryptography.SHA256]::Create()
        try { $digest = [Convert]::ToBase64String($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($envelope.body))) } finally { $hash.Dispose() }
        $record = $envelope.body | ConvertFrom-Json
        $reserve = @($record.progression.daggers.reserves | Where-Object pairId -eq farseer)
        if ($envelope.version -ne 1 -or $envelope.checksum -cne $digest -or $record.slotId -ne $id -or $record.world -ne 'zone-01' -or
            $record.progression.daggers.equipped -ne 'farseer' -or $reserve.Count -ne 1 -or $reserve[0].amount -ne 13 -or
            (($name -eq 'session.json.bak') -ne ($record.revision -eq $revision))) {
            throw "Invalid quit snapshot in $name. Fixture retained."
        }
    }
    Write-Output 'Graceful quit published a new revision and retained the preceding verified 13-unit reserve as backup.'
    Remove-Item -LiteralPath $root -Recurse -Force
}
finally { $process.Dispose() }
