# Builds the mod and installs it into the Bannerlord Modules folder.
# Usage: powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release",
    [string]$GameFolder = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
# The local deploy wears its own identity - Id "ImmersiveAI.Dev", shown as "Immersive AI (dev)" -
# so it can sit beside the Steam Workshop copy in the launcher and be picked deliberately.
# Enable only ONE of the two at a time: same behaviors, same config folder - both at once
# would double-register everything. (package.ps1 keeps the real "ImmersiveAI" identity.)
$moduleDir = Join-Path $GameFolder "Modules\ImmersiveAI.Dev"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"

# A pre-dev-identity deploy under the plain name collides with the Workshop copy in the
# launcher (two entries, same Id, launcher's pick wins) - clear it if it lingers.
$staleDir = Join-Path $GameFolder "Modules\ImmersiveAI"
if (Test-Path $staleDir) {
    try {
        Remove-Item $staleDir -Recurse -Force
        Write-Host "Removed the old local Modules\ImmersiveAI (the Workshop copy owns that identity now)."
    } catch {
        Write-Warning "Could not remove the old Modules\ImmersiveAI (game running?) - remove it by hand or the launcher lists the mod twice."
    }
}

dotnet build (Join-Path $repoRoot "src\ImmersiveAI.Module\ImmersiveAI.Module.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

New-Item -ItemType Directory -Force $binDir | Out-Null
$manifest = Get-Content (Join-Path $repoRoot "module\SubModule.xml") -Raw
$manifest = $manifest -replace '<Id value="ImmersiveAI" />', '<Id value="ImmersiveAI.Dev" />'
$manifest = $manifest -replace '<Name value="Immersive AI" />', '<Name value="Immersive AI (dev)" />'
Set-Content (Join-Path $moduleDir "SubModule.xml") $manifest -Encoding utf8

$outDir = Join-Path $repoRoot "src\ImmersiveAI.Module\bin\$Configuration"
Copy-Item (Join-Path $outDir "ImmersiveAI.dll") $binDir -Force
Copy-Item (Join-Path $outDir "ImmersiveAI.Core.dll") $binDir -Force
Copy-Item (Join-Path $outDir "Newtonsoft.Json.dll") $binDir -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $outDir "0Harmony.dll") $binDir -Force -ErrorAction SilentlyContinue
# MIT obliges the notice to travel with the DLL it covers.
Copy-Item (Join-Path $repoRoot "lib\0Harmony.LICENSE.txt") $binDir -Force -ErrorAction SilentlyContinue

# --- The voice host: RETIRED 2026.09.24 ------------------------------------------------------
# The mod no longer carries a speech engine. The voices are spoken by claude-voice, a separate app
# the Voices page installs in one click (see src\ImmersiveAI.Module\Voice\ClaudeVoiceClient.cs for
# why). What an older deploy left behind is swept here, so the dev install matches what ships:
# a host stranded by a crashed game first (it holds its own exe open), then its folder, then the
# single-file bundle that pre-2026.08.17 deploys dropped into bin.
Get-Process -Name "ImmersiveAI.VoiceHost" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$oldHostDir = Join-Path $moduleDir "VoiceHost"
if (Test-Path $oldHostDir) { Remove-Item $oldHostDir -Recurse -Force -ErrorAction SilentlyContinue }
Remove-Item (Join-Path $binDir "ImmersiveAI.VoiceHost.exe") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $binDir "ImmersiveAI.VoiceHost.pdb") -Force -ErrorAction SilentlyContinue

# GUI assets (prefab overrides such as the portrait map notice, the chat window) ride along with
# the module. Copy the folder's CONTENTS into an ensured destination: Copy-Item with a folder
# source and an existing folder destination would nest a GUI\GUI inside it instead of updating.
$guiSource = Join-Path $repoRoot "module\GUI"
if (Test-Path $guiSource) {
    $guiDest = Join-Path $moduleDir "GUI"
    New-Item -ItemType Directory -Force $guiDest | Out-Null
    Copy-Item (Join-Path $guiSource "*") $guiDest -Recurse -Force
}

# The voices that ship WITH the mod, so a fresh install is never a shelf with nothing on it. Laid
# onto the player's own shelf once each by VoiceSeeds; their folder is theirs from then on. Same
# contents-into-ensured-destination trap as GUI above.
$voicesSource = Join-Path $repoRoot "module\Voices"
if (Test-Path $voicesSource) {
    $voicesDest = Join-Path $moduleDir "Voices"
    # Cleared first, unlike GUI: a voice that MOVED (female\sibylla -> female\other\sibylla when
    # the shelf grew a culture rung) would otherwise be left behind at the old path as well, and
    # the seeder would honestly lay the same voice out twice under two names. This folder is ours
    # and rebuilt every deploy; the player's own shelf lives in Configs and is never touched.
    if (Test-Path $voicesDest) { Remove-Item $voicesDest -Recurse -Force }
    New-Item -ItemType Directory -Force $voicesDest | Out-Null
    Copy-Item (Join-Path $voicesSource "*") $voicesDest -Recurse -Force
    $shipped = @(Get-ChildItem $voicesSource -Recurse -Filter "voice.json" -ErrorAction SilentlyContinue).Count
    Write-Host "Voices shipped with the mod: $shipped"
}

Write-Host "Deployed to $moduleDir as 'Immersive AI (dev)' - enable it (and disable the Workshop one) in the launcher."
