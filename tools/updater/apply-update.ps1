# Zet een klaargezette update van FietsApp op zijn plek (story 13.6).
# De app start dit script uit de staging-map van de nieuwe versie bij het afsluiten. Het wacht tot de app
# en de brug dicht zijn, verwisselt de installatiemap en zet bij een fout de oude versie terug.
# Exitcode 0 = bijgewerkt, 1 = niet bijgewerkt (de oude versie staat er nog).

param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$InstallDir,
    [Parameter(Mandatory = $true)][string]$StagingDir,
    [Parameter(Mandatory = $true)][string]$ExeName,
    [Parameter(Mandatory = $true)][string]$LogPath,
    [switch]$Restart,
    [int]$WaitTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$InstallDir = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\')
$StagingDir = [IO.Path]::GetFullPath($StagingDir).TrimEnd('\')
$oldDir = "$InstallDir.old"

function Write-Log([string]$message) {
    try {
        $dir = Split-Path -Parent $LogPath
        if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
        Add-Content -Path $LogPath -Value ("{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $message) -Encoding UTF8
    }
    catch { }
}

function Retry([scriptblock]$action, [int]$attempts = 10) {
    for ($i = 1; $i -le $attempts; $i++) {
        try { & $action; return $true }
        catch {
            if ($i -eq $attempts) { Write-Log "  laatste poging mislukt: $($_.Exception.Message)"; return $false }
            Start-Sleep -Seconds 1
        }
    }
}

Write-Log "Update van '$InstallDir' met '$StagingDir' begint."

# 1. Wachten tot de app dicht is.
$deadline = (Get-Date).AddSeconds($WaitTimeoutSeconds)
while (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
    if ((Get-Date) -gt $deadline) {
        Write-Log "Update mislukt: de app (proces $ProcessId) is na $WaitTimeoutSeconds s nog niet dicht."
        exit 1
    }
    Start-Sleep -Milliseconds 250
}

# 2. Processen uit de installatiemap stoppen (de brug).
$prefix = $InstallDir + '\'
$running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $path = $null
    try { $path = $_.Path } catch { }
    $path -and $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
})
foreach ($p in $running) {
    Write-Log "Stop $($p.ProcessName) ($($p.Id))."
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    try { $p.WaitForExit(10000) | Out-Null } catch { }
}

# 3. Resten van een eerdere update opruimen.
if (Test-Path $oldDir) {
    if (-not (Retry { Remove-Item -Recurse -Force $oldDir })) {
        Write-Log "Update mislukt: oude map '$oldDir' kan niet weg."
        exit 1
    }
}

# 4. Huidige versie opzij zetten.
if (-not (Retry { Rename-Item -Path $InstallDir -NewName (Split-Path -Leaf $oldDir) })) {
    Write-Log "Update mislukt: '$InstallDir' is in gebruik; de oude versie blijft staan."
    exit 1
}

# 5. Nieuwe versie op zijn plek zetten; bij een fout de oude terug.
$sameVolume = [IO.Path]::GetPathRoot($InstallDir) -eq [IO.Path]::GetPathRoot($StagingDir)
$placed = if ($sameVolume) {
    Retry { Move-Item -Path $StagingDir -Destination $InstallDir } 3
}
else {
    Retry { Copy-Item -Path $StagingDir -Destination $InstallDir -Recurse } 1
}
if (-not $placed) {
    Write-Log "Update mislukt: nieuwe versie kon niet geplaatst worden, rollback naar de oude versie."
    if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir -ErrorAction SilentlyContinue }
    Rename-Item -Path $oldDir -NewName (Split-Path -Leaf $InstallDir)
    exit 1
}
if (-not $sameVolume) { Remove-Item -Recurse -Force $StagingDir -ErrorAction SilentlyContinue }

# 6. Opruimen en eventueel herstarten.
Remove-Item -Recurse -Force $oldDir -ErrorAction SilentlyContinue
Write-Log "Update geslaagd."

if ($Restart) {
    $exe = Join-Path $InstallDir $ExeName
    Write-Log "Start $exe."
    Start-Process -FilePath $exe -WorkingDirectory $InstallDir
}
exit 0
