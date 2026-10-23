# Scenariotests voor apply-update.ps1 (story 13.6).
# Gebruik: powershell -ExecutionPolicy Bypass -File tools/updater/test-apply-update.ps1
# Exitcode = aantal mislukte scenario's.

$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'apply-update.ps1'
$failures = 0

function New-Scenario([string]$name) {
    $root = Join-Path ([IO.Path]::GetTempPath()) ("apply-update-test-" + $name + "-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $install = Join-Path $root 'install'
    $staging = Join-Path $root 'staging'
    New-Item -ItemType Directory -Force $install, $staging | Out-Null
    Set-Content -Path (Join-Path $install 'FietsApp.exe') -Value 'oud' -Encoding ASCII
    Set-Content -Path (Join-Path $staging 'FietsApp.exe') -Value 'nieuw' -Encoding ASCII
    [pscustomobject]@{ Root = $root; Install = $install; Staging = $staging; Log = (Join-Path $root 'update.log') }
}

# Een proces dat even blijft leven, zodat het script moet wachten.
function Start-Dummy([int]$seconds) {
    Start-Process powershell -ArgumentList '-NoProfile', '-Command', "Start-Sleep $seconds" -WindowStyle Hidden -PassThru
}

function Invoke-Apply($s, [int]$processId, [int]$timeout = 60) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $script -ProcessId $processId -InstallDir $s.Install `
        -StagingDir $s.Staging -ExeName 'FietsApp.exe' -LogPath $s.Log -WaitTimeoutSeconds $timeout | Out-Null
    $LASTEXITCODE
}

function Read-Exe($s) { (Get-Content (Join-Path $s.Install 'FietsApp.exe') -Raw).Trim() }

function Report([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { Write-Output "PASS $name" }
    else { Write-Output "FAIL $name $detail"; $script:failures++ }
}

function Run([string]$name, [scriptblock]$body) {
    try { & $body }
    catch { Report $name $false $_.Exception.Message }
}

Run 'succes' {
    $s = New-Scenario 'succes'
    $p = Start-Dummy 2
    $code = Invoke-Apply $s $p.Id
    $ok = $code -eq 0 -and (Read-Exe $s) -eq 'nieuw' -and -not (Test-Path "$($s.Install).old") -and -not (Test-Path $s.Staging)
    Report 'succes' $ok "exit=$code"
}

Run 'vergrendelde map' {
    $s = New-Scenario 'lock'
    $lockPath = Join-Path $s.Install 'lock.txt'
    Set-Content $lockPath 'x'
    $lock = [IO.File]::Open($lockPath, 'Open', 'Read', 'None')
    try {
        $p = Start-Dummy 1
        $code = Invoke-Apply $s $p.Id
    }
    finally { $lock.Dispose() }
    $log = if (Test-Path $s.Log) { Get-Content $s.Log -Raw } else { '' }
    $marker = Join-Path $s.Staging 'apply-failed.txt'
    $count = if (Test-Path $marker) { (Get-Content $marker -Raw).Trim() } else { '' }
    $ok = $code -eq 1 -and (Read-Exe $s) -eq 'oud' -and (Test-Path (Join-Path $s.Staging 'FietsApp.exe')) -and $log -match 'mislukt' -and $count -eq '1'
    Report 'vergrendelde map' $ok "exit=$code teller=$count"
}

Run 'tweede mislukking telt op' {
    $s = New-Scenario 'count'
    Set-Content (Join-Path $s.Staging 'apply-failed.txt') '1'
    $p = Start-Dummy 120
    try { $code = Invoke-Apply $s $p.Id 2 }
    finally { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    $count = (Get-Content (Join-Path $s.Staging 'apply-failed.txt') -Raw).Trim()
    Report 'tweede mislukking telt op' ($code -eq 1 -and $count -eq '2') "exit=$code teller=$count"
}

# De app start het script met zijn eigen werkmap; staat die in de installatiemap, dan mag dat het hernoemen niet blokkeren.
Run 'werkmap in installatie' {
    $s = New-Scenario 'cwd'
    $p = Start-Dummy 1
    $args = "-NoProfile -ExecutionPolicy Bypass -File `"$script`" -ProcessId $($p.Id) -InstallDir `"$($s.Install)`" " +
            "-StagingDir `"$($s.Staging)`" -ExeName FietsApp.exe -LogPath `"$($s.Log)`""
    $proc = Start-Process powershell -ArgumentList $args -WorkingDirectory $s.Install -WindowStyle Hidden -Wait -PassThru
    $ok = $proc.ExitCode -eq 0 -and (Read-Exe $s) -eq 'nieuw'
    Report 'werkmap in installatie' $ok "exit=$($proc.ExitCode)"
}

# Installatie op een andere schijf dan %LOCALAPPDATA%: met subst krijgt dezelfde map een eigen stationsletter.
Run 'ander volume' {
    $s = New-Scenario 'volume'
    $letter = [char[]](90..68) | Where-Object { -not (Test-Path "$($_):\") } | Select-Object -First 1
    subst "$($letter):" $s.Root | Out-Null
    try {
        $remote = [pscustomobject]@{ Install = "$($letter):\install"; Staging = $s.Staging; Log = $s.Log }
        $p = Start-Dummy 1
        $code = Invoke-Apply $remote $p.Id
        $ok = $code -eq 0 -and (Read-Exe $s) -eq 'nieuw' -and -not (Test-Path "$($s.Install).old") -and -not (Test-Path "$($s.Install).new")
    }
    finally { subst "$($letter):" /d | Out-Null }
    Report 'ander volume' $ok "exit=$code"
}

Run 'proces blijft draaien' {
    $s = New-Scenario 'wait'
    $p = Start-Dummy 120
    try { $code = Invoke-Apply $s $p.Id 3 }
    finally { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    $ok = $code -eq 1 -and (Read-Exe $s) -eq 'oud' -and (Test-Path $s.Staging)
    Report 'proces blijft draaien' $ok "exit=$code"
}

Run 'oude .old aanwezig' {
    $s = New-Scenario 'old'
    $old = "$($s.Install).old"
    New-Item -ItemType Directory -Force $old | Out-Null
    Set-Content (Join-Path $old 'x.txt') 'rest'
    $p = Start-Dummy 1
    $code = Invoke-Apply $s $p.Id
    $ok = $code -eq 0 -and (Read-Exe $s) -eq 'nieuw' -and -not (Test-Path $old)
    Report 'oude .old aanwezig' $ok "exit=$code"
}

Run 'brug draait' {
    $s = New-Scenario 'bridge'
    $bridgeDir = Join-Path $s.Install 'Bridge'
    New-Item -ItemType Directory -Force $bridgeDir | Out-Null
    $bridgeExe = Join-Path $bridgeDir 'FietsBridge.exe'
    Copy-Item (Join-Path $env:WINDIR 'System32\PING.EXE') $bridgeExe
    $bridge = Start-Process $bridgeExe -ArgumentList '-n', '60', '127.0.0.1' -WindowStyle Hidden -PassThru
    try {
        $p = Start-Dummy 1
        $code = Invoke-Apply $s $p.Id
        $bridge.Refresh()
        $stopped = $bridge.HasExited
    }
    finally { Stop-Process -Id $bridge.Id -Force -ErrorAction SilentlyContinue }
    $ok = $code -eq 0 -and $stopped -and (Read-Exe $s) -eq 'nieuw'
    Report 'brug draait' $ok "exit=$code gestopt=$stopped"
}

exit $failures
