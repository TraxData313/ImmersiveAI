# Builds the mod and assembles a CLEAN, reproducible module layout for the Steam Workshop
# (or any manual distribution) under dist\ImmersiveAI - exactly what deploy.ps1 puts into the
# game, but from scratch every time, so no stale file from an old build can ride along.
# Also drops a versioned zip beside it, reading the version from module\SubModule.xml.
# Usage: powershell -ExecutionPolicy Bypass -File tools\package.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$moduleDir = Join-Path $distRoot "ImmersiveAI"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"

# A clean slate is the whole point of packaging.
if (Test-Path $moduleDir) { Remove-Item $moduleDir -Recurse -Force }

dotnet build (Join-Path $repoRoot "src\ImmersiveAI.Module\ImmersiveAI.Module.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

New-Item -ItemType Directory -Force $binDir | Out-Null
Copy-Item (Join-Path $repoRoot "module\SubModule.xml") $moduleDir -Force

$outDir = Join-Path $repoRoot "src\ImmersiveAI.Module\bin\$Configuration"
Copy-Item (Join-Path $outDir "ImmersiveAI.dll") $binDir -Force
Copy-Item (Join-Path $outDir "ImmersiveAI.Core.dll") $binDir -Force
Copy-Item (Join-Path $outDir "Newtonsoft.Json.dll") $binDir -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $outDir "0Harmony.dll") $binDir -Force -ErrorAction SilentlyContinue
# MIT obliges the notice to travel with the DLL it covers.
Copy-Item (Join-Path $repoRoot "lib\0Harmony.LICENSE.txt") $binDir -Force -ErrorAction SilentlyContinue

# --- The voice host ---------------------------------------------------------------------------
# The separate net8.0 TTS process, published FRAMEWORK-DEPENDENT and SINGLE-FILE. The full
# reasoning for both choices lives in deploy.ps1; the short of it is that one inert .exe cannot
# confuse the game's assembly loader the way a heap of net8.0 DLLs in this folder could, and that
# 33 MB of bundled runtime in every player's download buys nothing for a feature already gated
# behind a hand-installed, multi-gigabyte local engine. Players without the .NET 8 runtime simply
# get no voices, and the mod says so politely.
# Differences from deploy.ps1, both deliberate: no pdb ships, and a voice host that EXISTS but
# fails to build stops the packaging dead. A release that quietly lost its voices is a defect, and
# a clean-slate packager is exactly where that must be caught.
$voiceHostProj = Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\ImmersiveAI.VoiceHost.csproj"
$voiceHostShipped = $false
if (Test-Path $voiceHostProj) {
    # Clean slate here too - a stale exe from an older build must never ride along.
    $voiceOut = Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\bin\publish\package-$Configuration"
    if (Test-Path $voiceOut) { Remove-Item $voiceOut -Recurse -Force }

    dotnet publish $voiceHostProj -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o $voiceOut
    if ($LASTEXITCODE -ne 0) { throw "The voice host failed to build - refusing to package a release without it." }

    # The game spawns it BY NAME; a renamed exe would ship as a silent no-voices bug.
    $hostExe = Join-Path $voiceOut "ImmersiveAI.VoiceHost.exe"
    if (-not (Test-Path $hostExe)) { throw "Published the voice host but no ImmersiveAI.VoiceHost.exe came out - the game spawns it by that exact name." }
    Copy-Item $hostExe $binDir -Force
    # Anything the host bundles that obliges a notice travels with it, same habit as Harmony.
    Copy-Item (Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\THIRD-PARTY-NOTICES.txt") $binDir -Force -ErrorAction SilentlyContinue
    $voiceHostShipped = $true
}

# GUI assets - contents-into-ensured-destination, same trap-avoidance as deploy.ps1.
$guiSource = Join-Path $repoRoot "module\GUI"
if (Test-Path $guiSource) {
    $guiDest = Join-Path $moduleDir "GUI"
    New-Item -ItemType Directory -Force $guiDest | Out-Null
    Copy-Item (Join-Path $guiSource "*") $guiDest -Recurse -Force
}

# The voices that ship with the mod - see deploy.ps1. ONE RULE, and it is a legal one rather than a
# technical one: a voice folder carries the clip it was cloned from, so shipping one hands every
# player a copy of that. Public-domain / CC0 source audio only, never a real person's voice without
# their blessing. module\Voices\README.txt says the same where it will actually be read.
$voicesSource = Join-Path $repoRoot "module\Voices"

# Voices that must NEVER leave this machine, by folder name. EMPTY today, deliberately: the three
# names that lived here (sibylla, achilles, max) were early clones of real people, since RECREATED
# whole from CC0 source audio (kyutai/tts-voices) - Anton confirmed the rework and gave the
# all-clear on 2026.08.16, and module\Voices\README.txt states the practice. The list STAYS as the
# mechanism: any future voice cloned from someone who did not consent goes here (or wears the
# Source mark below), and it stops the release dead rather than warning - the embedding IS the
# voice, so packaging one distributes it. deploy.ps1 deliberately does NOT check: the local
# install is exactly where an unshippable development clone belongs.
$neverShip = @()

# A durable mark inside voice.json, for the voices a NAME list cannot keep up with. The 91 cloned
# from Bannerlord's own dialogue VO wear it: no game audio is redistributed (the folders hold only
# embeddings), but they are still clones of named actors who never agreed to it - the same rule the
# Alba/Pitt voices fall under, and the one module\Voices\README.txt states. It survives renaming
# both the folder and the display name, which a name list does not.
$neverShipMark = "NOT FOR RELEASE"

if (Test-Path $voicesSource) {
    # Folder name AND the name/id written inside, so renaming the folder does not slip one past.
    $blocked = @(Get-ChildItem $voicesSource -Recurse -Directory -ErrorAction SilentlyContinue | Where-Object {
        $metaPath = Join-Path $_.FullName "voice.json"
        if (-not (Test-Path $metaPath)) { return $false }

        $words = @($_.Name)
        $marked = $false
        try {
            $meta = Get-Content $metaPath -Raw | ConvertFrom-Json
            if ($meta.Name) { $words += $meta.Name }
            if ($meta.Id)   { $words += $meta.Id }
            if ($meta.Source -and $meta.Source -like "*$neverShipMark*") { $marked = $true }
        } catch { }   # an unreadable voice.json is judged on its folder name alone

        if ($marked) { return $true }

        $hit = $false
        foreach ($w in $words) {
            $plain = ($w -replace '[^a-zA-Z]', '').ToLowerInvariant()
            foreach ($banned in $neverShip) { if ($plain -like "*$banned*") { $hit = $true } }
        }
        return $hit
    })
    if ($blocked.Count -gt 0) {
        $names = ($blocked | ForEach-Object { $_.Name }) -join ", "
        throw "Refusing to package: module\Voices holds voice(s) marked never-ship - $names. Move them out of the repo (they are fine to deploy locally), or take the name off `$neverShip in this script if it is genuinely a different voice."
    }

    $voicesDest = Join-Path $moduleDir "Voices"
    New-Item -ItemType Directory -Force $voicesDest | Out-Null
    Copy-Item (Join-Path $voicesSource "*") $voicesDest -Recurse -Force
    $shippedVoices = @(Get-ChildItem $voicesSource -Recurse -Filter "voice.json" -ErrorAction SilentlyContinue).Count
} else {
    $shippedVoices = 0
}

# The version stamp comes from the manifest, so the zip name always tells the truth.
$version = "unversioned"
try {
    [xml]$manifest = Get-Content (Join-Path $repoRoot "module\SubModule.xml")
    $v = $manifest.Module.Version.value
    if ($v) { $version = $v -replace '[^\w\.\-]', '' }
} catch { }

$zipPath = Join-Path $distRoot "ImmersiveAI_$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $moduleDir -DestinationPath $zipPath

Write-Host "Packaged $version to $moduleDir"
# The release dance wants this visible: a package is either voiced or it is not.
if ($voiceHostShipped) {
    Write-Host "Voice host: included (framework-dependent - players need the .NET 8 runtime for voices)."
} else {
    Write-Host "Voice host: NOT included (no project in this tree) - this build ships without voices."
}
Write-Host "Voices shipped with the mod: $shippedVoices"
Write-Host "Zip: $zipPath"
Write-Host "Workshop upload: point the uploader at the dist\ImmersiveAI folder."
