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
# It lives in a VoiceHost FOLDER AT THE MODULE ROOT, and is published as ORDINARY FILES (changed
# 2026.08.17). It used to be a single-file bundle dropped straight into bin\Win64_Shipping_Client,
# for a good reason that has not gone away: that bin folder is on the game's assembly search path,
# and a scatter of net8.0 files lying beside a net472 ImmersiveAI.dll is a loader accident waiting
# to happen. What changed is the price of the fix. Nexus quarantined BOTH releases that carried the
# bundle (v3.0.0 and v3.1.0, the only two that ever had an exe in them); their own rules name "any
# kind of self-extracting file", and a single-file publish is exactly that - the managed DLLs are
# appended to the apphost and unpacked at run time, which every scanner reads as a packer.
# A folder outside bin answers both at once: the game never looks there, so the loader is safe, and
# a plain apphost with two DLLs beside it looks like the ordinary program it is.
#
# SELF-CONTAINED since 2026.08.21 (Anton: "click-agree-click-done"). It was framework-dependent
# until then, on an argument that has since expired: voices used to be gated behind a hand-installed
# multi-gigabyte engine, so a player reaching this exe had already installed heavier things by hand
# and one more prerequisite was nearly free. The in-game download button killed that premise - the
# engine now installs itself with one click, which leaves the .NET 8 runtime as the ONLY thing a
# player must go and fetch from a website, needing admin, to hear a voice. Worse, its failure is
# silent: the exe exits before writing its "ready" line and the mod simply keeps speaking in text.
# So we pay the bytes (measured at package time and printed) to delete the last manual step.
$voiceHostProj = Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\ImmersiveAI.VoiceHost.csproj"
if (Test-Path $voiceHostProj) {
    # A host stranded by a crashed game holds its own exe open and would block the copy below.
    # It is an orphan with nothing left to talk to either way.
    Get-Process -Name "ImmersiveAI.VoiceHost" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    $voiceOut = Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\bin\publish\$Configuration"
    if (Test-Path $voiceOut) { Remove-Item $voiceOut -Recurse -Force -ErrorAction SilentlyContinue }
    dotnet publish $voiceHostProj -c $Configuration -r win-x64 --self-contained true -o $voiceOut
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "The voice host did not build - deploying without it. The mod runs fine; the NPCs simply keep their voices to themselves."
    } else {
        # The game spawns it BY NAME, so a renamed exe is a silent no-voices bug - say so loudly.
        $hostExe = Join-Path $voiceOut "ImmersiveAI.VoiceHost.exe"
        if (Test-Path $hostExe) {
            # Clean slate: the host's own DLL names change with its dependencies, and a file left
            # from an older publish is loaded just as readily as one this build produced.
            $hostDir = Join-Path $moduleDir "VoiceHost"
            if (Test-Path $hostDir) { Remove-Item $hostDir -Recurse -Force -ErrorAction SilentlyContinue }
            New-Item -ItemType Directory -Force $hostDir | Out-Null
            # Contents-into-ensured-destination, the same trap deploy.ps1 avoids everywhere else.
            Copy-Item (Join-Path $voiceOut "*") $hostDir -Recurse -Force
            # Anything the host bundles that obliges a notice travels with it, same habit as Harmony.
            # (Dev builds keep the pdb the publish already produced - it is what turns a host stack
            # trace into line numbers.)
            # createdump.exe is the runtime's crash-dump helper, never invoked by us. Dropping it
            # leaves ONE executable in the package for a scanner to weigh, instead of two.
            Remove-Item (Join-Path $hostDir "createdump.exe") -Force -ErrorAction SilentlyContinue
            Copy-Item (Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\THIRD-PARTY-NOTICES.txt") $hostDir -Force -ErrorAction SilentlyContinue

            # A single-file bundle from a pre-2026.08.17 deploy still sits in bin and is still a
            # perfectly loadable host. Discovery now prefers the module root precisely so an
            # upgrade cannot go on spawning it, but leaving it there would also leave the thing
            # Nexus objects to lying in the folder we tell players is clean.
            Remove-Item (Join-Path $binDir "ImmersiveAI.VoiceHost.exe") -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $binDir "ImmersiveAI.VoiceHost.pdb") -Force -ErrorAction SilentlyContinue

            Write-Host "Voice host installed to VoiceHost\ (self-contained - no .NET runtime needed)."
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
