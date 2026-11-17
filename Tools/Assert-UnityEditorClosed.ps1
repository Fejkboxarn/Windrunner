$ErrorActionPreference = 'Stop'
$broker = Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe'
foreach ($process in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") {
    if ($process.ExecutablePath -match '[\\/]Unity Hub[\\/]resources[\\/]unity.exe$' -and $process.CommandLine -match '\sserve\s*$') { continue }
    if ($process.ExecutablePath -eq [IO.Path]::GetFullPath($broker) -and $process.CommandLine -match '\s--internal-auth-broker-serve\s*$') { continue }
    throw "Unity process $($process.ProcessId) may be an Editor. Close the Editor before running project validation."
}
