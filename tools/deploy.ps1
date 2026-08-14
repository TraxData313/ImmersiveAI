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

# --- The voice host ---------------------------------------------------------------------------
# ImmersiveAI.VoiceHost is a SEPARATE net8.0 process, not a reference: the TTS engine's failure
# mode is GGML_ASSERT -> abort(), which the game's runtime cannot catch. Out here a bad synthesis
# costs a session's voices; in-process it would cost the campaign. It has to travel WITH the
# module, since the game spawns it by path.
#
# Published as a SINGLE FILE, always. This bin folder is on the game's assembly search path, and
# a scatter of net8.0 runtime DLLs (System.Private.CoreLib and friends) lying beside a net472
# ImmersiveAI.dll is a loader accident waiting to happen. One .exe is inert there; the game only
# ever loads DLLs from this folder.
#
# FRAMEWORK-DEPENDENT on purpose - it needs the .NET 8 runtime present on the player's machine.
# Measured, not guessed: 0.15 MB this way against 33 MB self-contained-and-compressed (64 MB
# uncompressed), on a mod whose whole download is 1.3 MB today. The trade reads clearly once you
# remember what voices already cost: they are gated behind a hand-installed, multi-gigabyte local
# TTS engine with its own CUDA siblings and a settings.properties to fill in. Nobody reaches this
# exe without having installed heavier things by hand first, so the runtime prerequisite is nearly
# free for the few who want voices - while a self-contained copy would put 33 MB into EVERY
# player's download, on EVERY update, for a feature most of them never switch on. And a missing
# runtime is the politest failure available to us: the exe exits at once without ever writing its
# "ready" line, so the mod hears silence on the pipe and simply keeps speaking in text.
$voiceHostProj = Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\ImmersiveAI.VoiceHost.csproj"
if (Test-Path $voiceHostProj) {
    # A host stranded by a crashed game holds its own exe open and would block the copy below.
    # It is an orphan with nothing left to talk to either way.
    Get-Process -Name "ImmersiveAI.VoiceHost" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    $voiceOut = Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\bin\publish\$Configuration"
    dotnet publish $voiceHostProj -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -o $voiceOut
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "The voice host did not build - deploying without it. The mod runs fine; the NPCs simply keep their voices to themselves."
    } else {
        # The game spawns it BY NAME, so a renamed exe is a silent no-voices bug - say so loudly.
        $hostExe = Join-Path $voiceOut "ImmersiveAI.VoiceHost.exe"
        if (Test-Path $hostExe) {
            Copy-Item $hostExe $binDir -Force
            # Dev builds keep the pdb: it is what turns a host stack trace into line numbers.
            Copy-Item (Join-Path $voiceOut "ImmersiveAI.VoiceHost.pdb") $binDir -Force -ErrorAction SilentlyContinue
            # Anything the host bundles that obliges a notice travels with it, same habit as Harmony.
            Copy-Item (Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\THIRD-PARTY-NOTICES.txt") $binDir -Force -ErrorAction SilentlyContinue
            Write-Host "Voice host installed (framework-dependent - needs the .NET 8 runtime at play time)."
        } else {
            $made = (Get-ChildItem $voiceOut -Filter "*.exe" -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }) -join ", "
            if (-not $made) { $made = "(no exe at all)" }
            Write-Warning "Published the voice host but found no ImmersiveAI.VoiceHost.exe - the game spawns it by that exact name. Produced: $made"
        }
    }
} else {
    Write-Host "No voice host project yet - deploying without voices."
}

# GUI assets (prefab overrides such as the portrait map notice, the chat window) ride along with
# the module. Copy the folder's CONTENTS into an ensured destination: Copy-Item with a folder
# source and an existing folder destination would nest a GUI\GUI inside it instead of updating.
$guiSource = Join-Path $repoRoot "module\GUI"
if (Test-Path $guiSource) {
    $guiDest = Join-Path $moduleDir "GUI"
    New-Item -ItemType Directory -Force $guiDest | Out-Null
    Copy-Item (Join-Path $guiSource "*") $guiDest -Recurse -Force
}

Write-Host "Deployed to $moduleDir as 'Immersive AI (dev)' - enable it (and disable the Workshop one) in the launcher."
