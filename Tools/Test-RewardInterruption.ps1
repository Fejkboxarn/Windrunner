param([Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Label)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $project "Builds/$Label/Windrunner.exe"
$results=@()
foreach ($point in 'pending','published','debit') {
    $log=Join-Path $project "Logs/ProductionMigration/$Label-crash-$point.log"
    if(Test-Path -LiteralPath $log){throw 'Use a fresh label'}
    $p=Start-Process -FilePath $exe -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-windrunnerRewardBenchmark',"-windrunnerRewardCrash=$point",'-logFile',('"{0}"' -f $log))
    try {
        $limit=[DateTime]::UtcNow.AddSeconds(60)
        $marker=$null
        while(!$p.HasExited -and [DateTime]::UtcNow -lt $limit -and !$marker) {
            if(Test-Path -LiteralPath $log){$marker=Select-String -LiteralPath $log -Pattern "^Reward crash fixture (.+) ([0-9a-f]{32}) $point$"}
            if(!$marker){Start-Sleep -Milliseconds 100}
        }
        if(!$marker -or $p.HasExited){throw "Crash fixture failed to reach $point"}
        Stop-Process -Id $p.Id
        $p.WaitForExit()
        $root=[IO.Path]::GetFullPath($marker.Matches[0].Groups[1].Value)
        $id=$marker.Matches[0].Groups[2].Value
        if([IO.Path]::GetDirectoryName($root) -ne [IO.Path]::GetTempPath().TrimEnd('\') -or [IO.Path]::GetFileName($root) -notmatch '^Windrunner-rewards-[0-9a-f]{32}$'){throw 'Unexpected fixture path'}
        $envelope=Get-Content -LiteralPath (Join-Path $root "$id/session.json") -Encoding utf8 -Raw | ConvertFrom-Json
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$digest=[Convert]::ToBase64String($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($envelope.body)))}finally{$sha.Dispose()}
        $record=$envelope.body | ConvertFrom-Json
        $balance=@($record.progression.daggers.reserves | Where-Object pairId -eq farseer)
        $expected=if($point -eq 'published'){12}else{0}
        if($envelope.version -ne 1 -or $digest -cne $envelope.checksum -or $record.slotId -ne $id -or $record.displayName -ne 'Disposable benchmark' -or $record.progression.daggers.equipped -ne 'farseer' -or
            @($record.progression.persistentFacts | Where-Object occurrenceId -eq 'durable-before-crash').Count -ne 1 -or
            ($expected -gt 0 -and ($balance.Count -ne 1 -or $balance[0].amount -ne $expected)) -or ($expected -eq 0 -and $balance.Count -ne 0)){throw "Crash snapshot incorrect at $point"}
        $results += [ordered]@{point=$point; slot=$id; revision=$record.revision; checksum=$digest; durableReserve=$expected; equipped='farseer'; fact='durable-before-crash'}
        Write-Output "$point passed. Durable reserve $expected, equipped pair and committed fact verified."
        Remove-Item -LiteralPath $root -Recurse -Force
    }
    finally {if(!$p.HasExited){Stop-Process -Id $p.Id};$p.Dispose()}
}
ConvertTo-Json -InputObject $results -Depth 3 | Set-Content -LiteralPath (Join-Path $project "Logs/ProductionMigration/$Label-interruption-check.json")
