param([Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Label,[switch]$SynchronousControl,
      [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunLabel=$Label)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$log = Join-Path $project "Logs/ProductionMigration/$RunLabel-reward-player.log"
if (Test-Path -LiteralPath $log) { throw 'Use a fresh benchmark label.' }
$exe = Join-Path $project "Builds/$Label/Windrunner.exe"
$process = Start-Process -FilePath $exe -ArgumentList @('-batchmode','-windrunnerRewardBenchmark','-logFile',('"{0}"' -f $log)) -WindowStyle Hidden -PassThru
try {
    if (!$process.WaitForExit(120000)) { Stop-Process -Id $process.Id; throw 'Benchmark timed out. Inspect its player log.' }
    if ($process.ExitCode -ne 0) { throw "Player exited with $($process.ExitCode)." }
    $lines = Get-Content -LiteralPath $log
    $lines | Select-String '^REWARD_(PROFILE|TRAVERSAL)'
    if (@($lines | Select-String '^REWARD_PROFILE ').Count -ne 7 -or !($lines | Select-String '^REWARD_TRAVERSAL ') -or ($lines | Select-String 'Exception:|Could not save on exit:')) {
        throw 'Incomplete or failed benchmark.'
    }
    foreach ($mode in 'burst','single','spread','main-only','populated-burst','populated-single','populated-spread') {
        $row = @($lines | Select-String "^REWARD_PROFILE $mode n=40 writes=(\d+) ")
        $perSample = if ($mode -eq 'main-only') {0} elseif ($mode.EndsWith('spread') -or ($SynchronousControl -and $mode.EndsWith('burst'))) {4} else {1}
        if ($row.Count -ne 1 -or [int]$row[0].Matches[0].Groups[1].Value -ne 40*$perSample) { throw "Unexpected durable writes for $mode" }
    }
    $fixture = ($lines | Select-String '^Reward shutdown fixture (.+) ([0-9a-f]{32}) ([0-9a-f]{32}) (0|13)$').Matches
    if ($fixture.Count -ne 1) { throw 'Missing pre-quit revision.' }
    $root = [IO.Path]::GetFullPath($fixture[0].Groups[1].Value)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if ([IO.Path]::GetDirectoryName($root) -ne $temp -or [IO.Path]::GetFileName($root) -notmatch '^Windrunner-rewards-[0-9a-f]{32}$') {
        throw 'Unexpected disposable fixture directory.'
    }
    $id = $fixture[0].Groups[2].Value
    $revision = $fixture[0].Groups[3].Value
    $snapshots = [ordered]@{}
    foreach ($name in 'session.json','session.json.bak') {
        $envelope = Get-Content -LiteralPath (Join-Path $root "$id/$name") -Raw -Encoding UTF8 | ConvertFrom-Json
        $hash = [Security.Cryptography.SHA256]::Create()
        try { $digest = [Convert]::ToBase64String($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($envelope.body))) } finally { $hash.Dispose() }
        $record = $envelope.body | ConvertFrom-Json
        $reserve = @($record.progression.daggers.reserves | Where-Object pairId -eq farseer)
        $expected = if ($name -eq "session.json") {13} else {[double]$fixture[0].Groups[4].Value}
        if ($envelope.version -ne 1 -or $envelope.checksum -cne $digest -or $record.slotId -ne $id -or $record.world -ne 'zone-01' -or
            $record.progression.daggers.equipped -ne 'farseer' -or ($expected -gt 0 -and ($reserve.Count -ne 1 -or $reserve[0].amount -ne $expected)) -or ($expected -eq 0 -and $reserve.Count -ne 0) -or
            (($name -eq 'session.json.bak') -ne ($record.revision -eq $revision))) {
            throw "Invalid quit snapshot in $name. Fixture retained."
        }
        $snapshots[$name] = [ordered]@{revision=$record.revision; checksum=$digest; reserve=$expected}
    }
    [ordered]@{slot=$id; beforeQuit=$revision; world='zone-01'; equipped='farseer'; validated=$snapshots} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project "Logs/ProductionMigration/$RunLabel-quit-check.json")
    Write-Output 'Graceful quit published a new revision and retained the verified preceding revision as backup.'
    Remove-Item -LiteralPath $root -Recurse -Force
}
finally { $process.Dispose() }
