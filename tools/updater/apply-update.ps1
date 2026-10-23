# Zet een klaargezette update van FietsApp op zijn plek (story 13.6).
# De app start dit script uit de staging-map van de nieuwe versie bij het afsluiten. Het wacht tot de app
# en de brug dicht zijn, verwisselt de installatiemap en zet bij een fout de oude versie terug.
# Exitcode 0 = bijgewerkt, 1 = niet bijgewerkt (de oude versie staat er nog). Bij 1 telt apply-failed.txt
# in de staging-map op; na drie mislukte pogingen biedt de app een handmatige download aan.

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
$LogPath = [IO.Path]::GetFullPath($LogPath)
$oldDir = "$InstallDir.old"
$newDir = "$InstallDir.new"

# Een werkmap in de installatiemap (zo start de app ons vaak) houdt die map vast en blokkeert het hernoemen.
Set-Location ([IO.Path]::GetTempPath())
[Environment]::CurrentDirectory = [IO.Path]::GetTempPath()

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

# Stopt met exitcode 1 en houdt bij hoe vaak deze versie al niet geinstalleerd kon worden.
function Fail([string]$message) {
    Write-Log "Update mislukt: $message"
    try {
        if (Test-Path $StagingDir) {
            $marker = Join-Path $StagingDir 'apply-failed.txt'
            $count = 0
            if (Test-Path $marker) { [int]::TryParse((Get-Content $marker -Raw).Trim(), [ref]$count) | Out-Null }
            Set-Content -Path $marker -Value ($count + 1) -Encoding ASCII
        }
    }
    catch { Write-Log "  teller bijwerken mislukt: $($_.Exception.Message)" }
    exit 1
}

Write-Log "Update van '$InstallDir' met '$StagingDir' begint."

# 1. Wachten tot de app dicht is.
$deadline = (Get-Date).AddSeconds($WaitTimeoutSeconds)
while (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
    if ((Get-Date) -gt $deadline) { Fail "de app (proces $ProcessId) is na $WaitTimeoutSeconds s nog niet dicht." }
    Start-Sleep -Milliseconds 250
}

# 2. Processen uit de installatiemap stoppen: de brug (en een eventuele tweede FietsApp uit dezelfde map).
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
foreach ($leftover in $oldDir, $newDir) {
    if ((Test-Path $leftover) -and -not (Retry { Remove-Item -Recurse -Force $leftover })) {
        Fail "oude map '$leftover' kan niet weg."
    }
}

# 4. Staat de installatie op een andere schijf, dan eerst kopieren naar <installatie>.new; zo is stap 6
#    altijd een hernoeming en raakt een mislukte kopie de werkende versie niet.
$sameVolume = [IO.Path]::GetPathRoot($InstallDir) -eq [IO.Path]::GetPathRoot($StagingDir)
$source = $StagingDir
if (-not $sameVolume) {
    if (-not (Retry { Copy-Item -Path $StagingDir -Destination $newDir -Recurse } 3)) {
        Remove-Item -Recurse -Force $newDir -ErrorAction SilentlyContinue
        Fail "kopieren naar '$newDir' lukte niet."
    }
    $source = $newDir
}

# 5. Huidige versie opzij zetten.
if (-not (Retry { Rename-Item -Path $InstallDir -NewName (Split-Path -Leaf $oldDir) })) {
    if (-not $sameVolume) { Remove-Item -Recurse -Force $newDir -ErrorAction SilentlyContinue }
    Fail "'$InstallDir' is in gebruik; de oude versie blijft staan."
}

# 6. Nieuwe versie op zijn plek zetten; bij een fout de oude terug.
if (-not (Retry { Move-Item -Path $source -Destination $InstallDir } 3)) {
    Write-Log "Nieuwe versie kon niet geplaatst worden, rollback naar de oude versie."
    if (-not (Retry { Rename-Item -Path $oldDir -NewName (Split-Path -Leaf $InstallDir) })) {
        Write-Log "ROLLBACK MISLUKT: de vorige versie staat in '$oldDir'; zet die map handmatig terug naar '$InstallDir'."
    }
    Fail "nieuwe versie niet geplaatst."
}
if (-not $sameVolume) { Remove-Item -Recurse -Force $StagingDir -ErrorAction SilentlyContinue }
Remove-Item -Force (Join-Path $InstallDir 'apply-failed.txt') -ErrorAction SilentlyContinue

# 7. Opruimen en eventueel herstarten.
Remove-Item -Recurse -Force $oldDir -ErrorAction SilentlyContinue
Write-Log "Update geslaagd."

if ($Restart) {
    $exe = Join-Path $InstallDir $ExeName
    Write-Log "Start $exe."
    Start-Process -FilePath $exe -WorkingDirectory $InstallDir
}
exit 0
